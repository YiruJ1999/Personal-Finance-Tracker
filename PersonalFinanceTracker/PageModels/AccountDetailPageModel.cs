using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Services;
using PersonalFinanceTracker.Data;
using PersonalFinanceTracker.Resources.Strings;
using System.Collections.ObjectModel;
using System.Linq;
using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;

namespace PersonalFinanceTracker.PageModels
{
    // Optional: uncomment if you want Shell to set AccountId automatically via query "?id=123"
    // [QueryProperty(nameof(AccountId), "id")]
    public partial class AccountDetailPageModel : ObservableObject
    {
        private readonly AccountRepository _accountRepo;
        private readonly RecordRepository _recordRepo;
        private readonly BookRepository _bookRepo;

        private const string PrefKeyCurrentBookId = "current_book_id";
        private const string PrefKeyCurrentBook = "current_book";

        // ---- runtime data comes as a property (set by the Page after navigation) ----
        [ObservableProperty]
        private int accountId; // when set, we'll trigger InitAsync below

        [ObservableProperty] private string accountName;
        public ObservableCollection<MonthOption> AvailableMonths { get; } = new();
        public ObservableCollection<Record> MonthlyRecords { get; } = new();

        private MonthOption _selectedMonth;
        public MonthOption SelectedMonth
        {
            get => _selectedMonth;
            set
            {
                if (SetProperty(ref _selectedMonth, value))
                    _ = LoadMonthlyRecordsAsync();
            }
        }

        [ObservableProperty] private decimal totalAmount;
        public string TotalAmountText =>
        string.Format("{0}{1:C}", AppResources.Label_TotalAmount, TotalAmount);
        public IAsyncRelayCommand EditAccountCommand { get; }

        // DI-friendly ctor: ONLY services/ repositories, no primitive runtime values
        public AccountDetailPageModel(
            AccountRepository accountRepo,
            RecordRepository recordRepo,
            BookRepository bookRepo)
        {
            _accountRepo = accountRepo ?? throw new ArgumentNullException(nameof(accountRepo));
            _recordRepo = recordRepo ?? throw new ArgumentNullException(nameof(recordRepo));
            _bookRepo = bookRepo ?? throw new ArgumentNullException(nameof(bookRepo));

            EditAccountCommand = new AsyncRelayCommand(EditAccountAsync);
            CurrencyManager.CurrencyChanged += OnCurrencyChanged;
            LanguageManager.LanguageChanged += (_, __) =>
            {
                OnPropertyChanged(nameof(TotalAmountText));
            };
        }

        // When AccountId changes (set by the page), load everything
        partial void OnAccountIdChanged(int value)
        {
            if (value > 0)
                _ = InitAsync();
        }

        public async Task InitAsync()
        {
            System.Diagnostics.Debug.WriteLine($"[Detail] AccountId={AccountId}");

            BuildMonthOptions();

            // Resolve and show account display name
            var acc = (await _accountRepo.ListAsync()).FirstOrDefault(a => a.Id == AccountId);
            AccountName = acc?.Name ?? $"Account #{AccountId}";

            // Select current month
            var now = DateTime.Now;
            SelectedMonth = AvailableMonths.First(m => m.Year == now.Year && m.Month == now.Month);

            // Load header and current month records
            await LoadHeaderAsync();
            await LoadMonthlyRecordsAsync();
        }

        private void BuildMonthOptions()
        {
            if (AvailableMonths.Count > 0) return; // prevent duplicates
            var now = DateTime.Now;
            for (int i = 0; i < 18; i++)
            {
                var dt = new DateTime(now.Year, now.Month, 1).AddMonths(-i);
                AvailableMonths.Add(new MonthOption(dt.Year, dt.Month));
            }
        }

        // Ensure Account table reflects latest records, then read this account by Id
        private async Task LoadHeaderAsync()
        {
            await _accountRepo.SyncAccountsFromBooksAsync(bookId: null);
            var accounts = await _accountRepo.ListAsync();
            var self = accounts.FirstOrDefault(a => a.Id == AccountId);
            TotalAmount = self?.Balance ?? 0m;
        }

        // Load records for the selected month across all books for this AccountId
        private async Task LoadMonthlyRecordsAsync()
        {
            MonthlyRecords.Clear();
            if (SelectedMonth is null || AccountId <= 0) return;

            var start = new DateTime(SelectedMonth.Year, SelectedMonth.Month, 1, 0, 0, 0, DateTimeKind.Local);
            var end = start.AddMonths(1).AddTicks(-1);

            var records = await _accountRepo.ListRecordsForAccountAcrossBooksAsync(AccountId, start, end);
            foreach (var r in records)
                MonthlyRecords.Add(r);
        }

        // Edit balance by writing an adjustment record (by AccountId, into current bookId)
        private async Task EditAccountAsync()
        {
            string input = await Application.Current.MainPage.DisplayPromptAsync(
                "编辑此账户", "请输入新的余额：", "保存", "取消", keyboard: Keyboard.Numeric);

            if (string.IsNullOrWhiteSpace(input)) return;

            if (!decimal.TryParse(input.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out var newBalance))
            {
                await Application.Current.MainPage.DisplayAlert("提示", "请输入有效的金额。", "好的");
                return;
            }

            // 1) Read current balance after sync
            await _accountRepo.SyncAccountsFromBooksAsync(bookId: null);
            var accounts = await _accountRepo.ListAsync();
            var self = accounts.FirstOrDefault(a => a.Id == AccountId);
            var current = self?.Balance ?? 0m;

            // 2) Compute delta
            var delta = newBalance - current;
            if (delta == 0m)
            {
                await LoadHeaderAsync();
                return;
            }

            var type = delta > 0 ? "收入" : "支出";
            var amount = Math.Abs(delta);

            // 3) Resolve current bookId (migrate legacy name if needed)
            var bookId = await GetOrCreateCurrentBookIdAsync();

            // 4) Persist an adjustment record
            var adjustment = new Record
            {
                Type = type,
                Amount = amount,
                Category = "余额调整",
                Note = "账户详情页手动调整",
                Timestamp = DateTime.Now,
                AccountId = AccountId
            };

            await _recordRepo.SaveAsync(bookId, adjustment);

            // 5) Refresh UI
            await LoadHeaderAsync();
            await LoadMonthlyRecordsAsync();
            await Application.Current.MainPage.DisplayAlert("提示", "余额已更新", "好的");
        }

        // Resolve current bookId; if only legacy name exists, create/resolve then save id to Preferences.
        private async Task<int> GetOrCreateCurrentBookIdAsync()
        {
            if (Preferences.Default.ContainsKey(PrefKeyCurrentBookId))
            {
                var id = Preferences.Default.Get(PrefKeyCurrentBookId, 0);
                if (id > 0) return id;
            }

            var legacyName = Preferences.Default.Get(PrefKeyCurrentBook, "默认");
            var book = await _bookRepo.EnsureBookAsync(string.IsNullOrWhiteSpace(legacyName) ? "默认" : legacyName.Trim());
            Preferences.Default.Set(PrefKeyCurrentBookId, book.Id);
            return book.Id;
        }

        private async void OnCurrencyChanged(object? sender, EventArgs e) // ADD
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                // Keep current AccountId and SelectedMonth; just reload data
                await LoadHeaderAsync();
                await LoadMonthlyRecordsAsync();
            });
        }

        // (optional) call this when page/VM is disposed
        public void UnsubscribeCurrency() // ADD (optional)
        {
            CurrencyManager.CurrencyChanged -= OnCurrencyChanged;
        }

        // When TotalAmount changes, notify TotalAmountText to refresh
        partial void OnTotalAmountChanged(decimal value)
        {
            OnPropertyChanged(nameof(TotalAmountText));
        }

    }

    public record MonthOption(int Year, int Month)
    {
        public string Display => $"{Year}-{Month:00}";
    }
}
