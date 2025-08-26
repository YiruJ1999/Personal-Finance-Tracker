using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Microsoft.Maui.ApplicationModel;
using PersonalFinanceTracker.Data;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Popups;
using PersonalFinanceTracker.Services;
using PersonalFinanceTracker.Resources.Strings;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace PersonalFinanceTracker.PageModels
{
    // DTO for chart points ("YYYY-MM", value)
    public class ChartPoint
    {
        public string Key { get; set; } = string.Empty;
        public double Value { get; set; }
    }


    public partial class AccountPageModel : ObservableObject
    {
        private readonly DatabaseService _dbService;
        private readonly AccountRepository _accountRepository;
        private readonly RecordRepository _recordRepository;
        private readonly BookRepository _bookRepository;

        private bool _isLoading;
        private bool _isNavigatingToDetail;
        private bool _isOpeningAddAccountPopup;

        // Observable properties for UI
        [ObservableProperty] private decimal totalAssets;
        [ObservableProperty] private List<ChartPoint> last4MonthsTrend = new();
        [ObservableProperty] private double yMin;
        [ObservableProperty] private double yMax;
        [ObservableProperty] private double yInterval;
        [ObservableProperty] private string newAccountName = string.Empty;
        [ObservableProperty] private decimal newAccountOpeningBalance;
        [ObservableProperty] private Account? selectedAccount;
        [ObservableProperty] private decimal editedBalance;

        // Collection bound to the accounts list
        public ObservableCollection<Account> Accounts { get; } = new();

        private const string PrefKeyCurrentBookId = "current_book_id"; // Id-based
        private const string PrefKeyCurrentBook = "current_book";    // legacy name

        public AccountPageModel(
            DatabaseService dbService,
            AccountRepository accountRepository,
            RecordRepository recordRepository,
            BookRepository bookRepository)
        {
            _dbService = dbService ?? throw new ArgumentNullException(nameof(dbService));
            _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
            _recordRepository = recordRepository ?? throw new ArgumentNullException(nameof(recordRepository));
            _bookRepository = bookRepository ?? throw new ArgumentNullException(nameof(bookRepository));
            CurrencyManager.CurrencyChanged += OnCurrencyChanged;
        }

        // Load page data: sync from records, then list accounts, totals and last-4-months trend.
        [RelayCommand]
        public async Task LoadAsync()
        {
            if (_isLoading) return;
            _isLoading = true;

            try
            {
                await EnsureDatabaseReadyAsync();
                await TrySyncAccountsFromBooksAsync();

                // Fetch heavy stuff off UI thread
                var list = await FetchAccountsAsync();
                var total = list.Sum(a => a.Balance);
                var trendDict = await FetchLast4MonthsTotalsAsync();

                // Build view models for chart + axis range
                var trendPoints = BuildTrendPoints(trendDict);
                ComputeAxisRange(trendPoints);

                // Apply to UI in one batch
                await ApplyStateAsync(list, total, trendPoints);
            }
            finally
            {
                _isLoading = false;
            }

            Debug.WriteLine($"[AccountPage] loaded {Accounts.Count} accounts, total={TotalAssets}");
        }

        // Ensure DB + tables exist
        private async Task EnsureDatabaseReadyAsync()
        {
            await _dbService.InitAsync();
            await _accountRepository.EnsureDatabaseInitializedAsync();
        }

        // Sync account snapshots from books (non-blocking failure)
        private async Task TrySyncAccountsFromBooksAsync()
        {
            try
            {
                await _accountRepository.SyncAccountsFromBooksAsync(null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[AccountPage.Load] Sync failed: " + ex.Message);
            }
        }

        // Query accounts
        private Task<List<Account>> FetchAccountsAsync()
            => _accountRepository.ListAsync();

        // Get last-4-month total assets (with a fallback if empty)
        private async Task<Dictionary<string, decimal>> FetchLast4MonthsTotalsAsync()
        {
            var data = await _accountRepository.GetLast4MonthsTotalAssetsAsync();
            if (data != null && data.Count > 0) return data;

            // Fallback demo data (optional)
            return new Dictionary<string, decimal>
            {
                ["2025-05"] = 1000,
                ["2025-06"] = 2000,
                ["2025-07"] = 1800,
                ["2025-08"] = 2300,
            };
        }

        // Build chart points sorted by key
        private List<ChartPoint> BuildTrendPoints(Dictionary<string, decimal> trendDict)
        {
            return trendDict
                .OrderBy(kv => kv.Key)
                .Select(kv => new ChartPoint { Key = kv.Key, Value = Math.Round((double)kv.Value, 2) })
                .ToList();
        }

        // Compute a nice axis range so the line does not hug X-axis
        private void ComputeAxisRange(List<ChartPoint> points)
        {
            if (points == null || points.Count == 0)
            {
                YMin = 0; YMax = 1; YInterval = 0.2;
                return;
            }

            var ys = points.Select(p => p.Value).Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).ToList();
            if (ys.Count == 0)
            {
                YMin = 0; YMax = 1; YInterval = 0.2;
                return;
            }

            var min = ys.Min();
            var max = ys.Max();

            if (Math.Abs(max - min) < 1e-9)
            {
                // Flat line: add ±10% padding
                var pad = Math.Max(1, Math.Abs(max) * 0.1);
                YMin = Math.Max(0, min - pad);
                YMax = max + pad;
            }
            else
            {
                var range = max - min;
                var step = NiceStep(range / 5.0); // target ~5 ticks

                YMax = Math.Ceiling(max / step) * step;
                var minCandidate = Math.Floor(min / step) * step;

                // Assets usually non-negative; clamp to zero
                YMin = Math.Max(0, minCandidate);

                if (YMin > 0 && YMin < step * 0.5) YMin = 0;
            }

            var roughInterval = (YMax - YMin) / 4.0;
            YInterval = NiceStep(Math.Max(roughInterval, 1e-6));
        }

        // "1–2–5" nice step
        private static double NiceStep(double rough)
        {
            if (rough <= 0) return 1;
            var exp = Math.Floor(Math.Log10(rough));
            var frac = rough / Math.Pow(10, exp);
            double niceFrac = (frac <= 1) ? 1 : (frac <= 2) ? 2 : (frac <= 5) ? 5 : 10;
            return niceFrac * Math.Pow(10, exp);
        }

        // Apply all UI-bound properties on UI thread in one go
        private Task ApplyStateAsync(List<Account> list, decimal total, List<ChartPoint> trendPoints)
        {
            return MainThread.InvokeOnMainThreadAsync(() =>
            {
                Accounts.Clear();
                foreach (var a in list) Accounts.Add(a);

                TotalAssets = total;
                Last4MonthsTrend = trendPoints;

                if (SelectedAccount is not null)
                    EditedBalance = SelectedAccount.Balance;
            });
        }


        // Add a new account using bound fields NewAccountName / NewAccountOpeningBalance.
        [RelayCommand]
        private async Task AddAccountAsync()
        {
            if (string.IsNullOrWhiteSpace(NewAccountName))
                return;

            await _accountRepository.AddAccountAsync(NewAccountName.Trim(), NewAccountOpeningBalance);
            await LoadAsync();

            // Reset inputs
            NewAccountName = string.Empty;
            NewAccountOpeningBalance = 0m;
        }

        // Delete the currently selected account (keeps records unless user chooses otherwise).
        [RelayCommand]
        private async Task DeleteAccountAsync()
        {
            if (SelectedAccount is null)
                return;

            // Ask whether to also delete records
            var choice = await Application.Current.MainPage.DisplayActionSheet(
                "是否同时删除该账户的所有明细记录？", "取消", null,
                "仅删除账户（保留明细）",
                "删除账户并删除全部明细");
            if (choice is null || choice == "取消") return;

            bool alsoDelete = choice == "删除账户并删除全部明细";
            await _accountRepository.DeleteAccountAsync(SelectedAccount.Id, alsoDelete);
            await LoadAsync();
        }

        // Update balance by writing an adjustment record (Id-based).
        [RelayCommand]
        private async Task UpdateBalanceAsync()
        {
            if (SelectedAccount is null)
                return;

            await _dbService.InitAsync();

            var current = SelectedAccount.Balance;
            var target = EditedBalance;
            var delta = target - current;

            if (delta == 0m)
            {
                await LoadAsync();
                return;
            }

            var type = delta > 0 ? "收入" : "支出";
            var amount = Math.Abs(delta);

            var bookId = await GetOrCreateCurrentBookIdAsync();

            var record = new Record
            {
                Type = type,
                Amount = amount,
                Category = "余额调整",
                Note = "账户列表页手动调整",
                Timestamp = DateTime.Now,
                AccountId = SelectedAccount.Id // Id-based
            };

            await _recordRepository.SaveAsync(bookId, record);
            await LoadAsync();
        }

        // Keep EditedBalance in sync with the selected account.
        partial void OnSelectedAccountChanged(Account? value)
        {
            EditedBalance = value?.Balance ?? 0m;
        }

        // Navigate to account detail page. Pass accountId in the route.
        [RelayCommand]
        private async Task OpenAccountDetailAsync()
        {
            if (_isNavigatingToDetail) return;
            _isNavigatingToDetail = true;

            try
            {
                var account = SelectedAccount;
                if (account is null) return;

                System.Diagnostics.Debug.WriteLine($"[NAV] OpenAccountDetail fired, accId={account.Id}");
                EditedBalance = account.Balance;

                await Shell.Current.GoToAsync("accountDetail", new Dictionary<string, object>
                {
                    ["id"] = account.Id
                });
                SelectedAccount = null;
            }
            finally
            {
                _isNavigatingToDetail = false;
            }
        }


        // Show Add Account popup(name + optional opening balance).
        [RelayCommand]
        private async Task ShowAddAccountPopupAsync()
        {
            if (_isOpeningAddAccountPopup) return;
            _isOpeningAddAccountPopup = true;

            try
            {
                var name = await Application.Current.MainPage.DisplayPromptAsync(
                    "添加账户", "请输入账户名称：", "保存", "取消", placeholder: "例如：现金/银行卡");
                if (string.IsNullOrWhiteSpace(name)) return;

                var openingText = await Application.Current.MainPage.DisplayPromptAsync(
                    "期初余额", "可选：输入期初余额（留空则为 0）", "确定", "跳过", keyboard: Keyboard.Numeric);

                decimal opening = 0m;
                if (!string.IsNullOrWhiteSpace(openingText) &&
                    decimal.TryParse(openingText, System.Globalization.NumberStyles.Number,
                                     System.Globalization.CultureInfo.CurrentCulture, out var val))
                {
                    opening = val;
                }

                var acc = await _accountRepository.AddAccountAsync(name.Trim(), opening);

                // If you also want AccountPage to refresh even when you're already on it:
                await LoadAsync();

                // And additionally notify other pages (optional but nice)
                CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default
                    .Send(new AccountChangedMessage(acc.Id));
            }
            finally
            {
                _isOpeningAddAccountPopup = false;
            }
        }

        // Show delete confirmation with an option to also delete all related records.
        [RelayCommand]
        private async Task ShowDeleteAccountPopupAsync(int accountId)
        {
            if (accountId == 0 )
                return;

            var confirm = await Application.Current.MainPage.DisplayAlert(
                "删除账户", $"确定要删除账户“{accountId}”？", "删除", "取消");
            if (!confirm) return;

            var choice = await Application.Current.MainPage.DisplayActionSheet(
                "是否同时删除该账户的所有明细记录？", "取消", null,
                "仅删除账户（保留明细）",
                "删除账户并删除全部明细");

            if (choice is null || choice == "取消") return;

            bool alsoDelete = choice == "删除账户并删除全部明细";
            await _accountRepository.DeleteAccountAsync(accountId, alsoDelete);
            await LoadAsync();
        }



        // -----------------------
        // Helpers
        // -----------------------
        private async Task<int> GetOrCreateCurrentBookIdAsync()
        {
            if (Preferences.Default.ContainsKey(PrefKeyCurrentBookId))
            {
                var id = Preferences.Default.Get(PrefKeyCurrentBookId, 0);
                if (id > 0) return id;
            }

            var legacyName = Preferences.Default.Get(PrefKeyCurrentBook, "默认");
            var book = await _bookRepository.EnsureBookAsync(string.IsNullOrWhiteSpace(legacyName) ? "默认" : legacyName.Trim());
            Preferences.Default.Set(PrefKeyCurrentBookId, book.Id);
            return book.Id;
        }

        // Fire a lightweight UI refresh so all currency strings get re-rendered
        private async void OnCurrencyChanged(object? sender, EventArgs e) // ADD
        {
            await MainThread.InvokeOnMainThreadAsync(async () => await LoadAsync());
        }

        // (optional) call this when the VM is disposed/removed to avoid leaks
        public void UnsubscribeCurrency() // ADD (optional)
        {
            CurrencyManager.CurrencyChanged -= OnCurrencyChanged;
        }
    }
}
