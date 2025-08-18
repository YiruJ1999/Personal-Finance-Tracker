using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using PersonalFinanceTracker.Data;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Popups;
using PersonalFinanceTracker.Services;
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
        }

        // Load page data: sync from records, then list accounts, totals and last-4-months trend.
        [RelayCommand]
        public async Task LoadAsync()
        {
            if (_isLoading) return;
            _isLoading = true;

            try
            {
                await _dbService.InitAsync();
                await _accountRepository.EnsureDatabaseInitializedAsync();

                // Do not block UI if sync throws due to any legacy table mismatch
                try
                {
                    await _accountRepository.SyncAccountsFromBooksAsync(null);
                }
                catch (Exception syncEx)
                {
                    System.Diagnostics.Debug.WriteLine("[AccountPage.Load] Sync failed: " + syncEx.Message);
                }

                // Fetch data off the UI thread
                var list = await _accountRepository.ListAsync();
                var total = list.Sum(a => a.Balance);
                var trendData = await _accountRepository.GetLast4MonthsTotalAssetsAsync();
                if (trendData == null || trendData.Count == 0)
                {
                    trendData = new Dictionary<string, decimal>
                    {
                        ["2025-05"] = 1000,
                        ["2025-06"] = 2000,
                        ["2025-07"] = 1800,
                        ["2025-08"] = 2300,
                    };
                }

                // IMPORTANT: update ObservableCollection and bindable props on the main thread
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    Accounts.Clear();
                    foreach (var a in list) Accounts.Add(a);

                    TotalAssets = total;

                    Last4MonthsTrend = trendData
                        .OrderBy(kv => kv.Key)
                        .Select(kv => new ChartPoint { Key = kv.Key, Value = Math.Round((double)kv.Value, 2) })
                        .ToList();

                    if (SelectedAccount is not null)
                        EditedBalance = SelectedAccount.Balance;

                    System.Diagnostics.Debug.WriteLine($"[AccountPage] loaded {Accounts.Count} accounts (UI updated)");
                });
            }
            finally
            {
                _isLoading = false;
            }
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
        private async Task OpenAccountDetailAsync(int accountId)
        {
            // Prevent double navigation (fast taps, re-entrancy)
            if (_isNavigatingToDetail) return;
            _isNavigatingToDetail = true;

            try
            {
                var selected = Accounts.FirstOrDefault(a => a.Id == accountId);
                if (selected is null) return;

                SelectedAccount = selected;
                EditedBalance = selected.Balance;

                await Shell.Current.GoToAsync("accountDetail" + $"?id={selected.Id}");
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
    }
}
