using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;                 // for Preferences
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Services;
using PersonalFinanceTracker.Data;
using System.Collections.ObjectModel;
using System.Linq;
using System;
using System.Globalization;

namespace PersonalFinanceTracker.PageModels
{
    public partial class AccountDetailPageModel : ObservableObject
    {
        private readonly AccountRepository _accountRepo;
        private readonly RecordRepository _recordRepo;
        private const string PrefKeyCurrentBook = "current_book";

        // Title binding
        public string AccountName { get; }

        public ObservableCollection<MonthOption> AvailableMonths { get; } = new();
        public ObservableCollection<Record> MonthlyRecords { get; } = new();

        private MonthOption _selectedMonth;
        public MonthOption SelectedMonth
        {
            get => _selectedMonth;
            set
            {
                if (SetProperty(ref _selectedMonth, value))
                {
                    // Reload records when month changes
                    _ = LoadMonthlyRecordsAsync();
                }
            }
        }

        private decimal _totalAmount;
        public decimal TotalAmount
        {
            get => _totalAmount;
            set => SetProperty(ref _totalAmount, value);
        }

        public IAsyncRelayCommand EditAccountCommand { get; }

        public AccountDetailPageModel(
            string accountName,
            AccountRepository accountRepo,
            RecordRepository recordRepo)
        {
            AccountName = accountName ?? throw new ArgumentNullException(nameof(accountName));
            _accountRepo = accountRepo ?? throw new ArgumentNullException(nameof(accountRepo));
            _recordRepo = recordRepo ?? throw new ArgumentNullException(nameof(recordRepo));

            EditAccountCommand = new AsyncRelayCommand(EditAccountAsync);
        }

        public async Task InitAsync()
        {
            // Build months (last 18, newest first) and select current month
            BuildMonthOptions();
            var now = DateTime.Now;
            SelectedMonth = AvailableMonths.First(m => m.Year == now.Year && m.Month == now.Month);

            // Load header and current month records
            await LoadHeaderAsync();
            await LoadMonthlyRecordsAsync();
        }

        private void BuildMonthOptions()
        {
            var now = DateTime.Now;
            for (int i = 0; i < 18; i++)
            {
                var dt = new DateTime(now.Year, now.Month, 1).AddMonths(-i);
                AvailableMonths.Add(new MonthOption(dt.Year, dt.Month));
            }
        }


        // Load the latest balance for this account from the Account table.
        private async Task LoadHeaderAsync()
        {
            // Ensure Account table reflects the latest records
            await _accountRepo.SyncAccountsFromRecordsAsync();

            var accounts = await _accountRepo.GetAccountsWithBalancesAsync();
            // Case-insensitive match to avoid whitespace/case issues
            var self = accounts.FirstOrDefault(a =>
                a.Name.Equals(AccountName, StringComparison.OrdinalIgnoreCase));
            TotalAmount = self?.Balance ?? 0m;
        }

        // Load records for the selected month across all books for this account.
        private async Task LoadMonthlyRecordsAsync()
        {
            MonthlyRecords.Clear();
            if (SelectedMonth is null) return;

            var start = new DateTime(SelectedMonth.Year, SelectedMonth.Month, 1, 0, 0, 0, DateTimeKind.Local);
            var end = start.AddMonths(1).AddTicks(-1); // inclusive month end

            var records = await _accountRepo.ListRecordsForAccountAcrossBooksAsync(AccountName, start, end);
            foreach (var r in records)
                MonthlyRecords.Add(r);
        }


        // Edit balance by writing an adjustment record.
        private async Task EditAccountAsync()
        {
            string input = await Application.Current.MainPage.DisplayPromptAsync(
                "编辑此账户", "请输入新的余额：", "保存", "取消", keyboard: Keyboard.Numeric);

            if (string.IsNullOrWhiteSpace(input))
                return;

            if (!decimal.TryParse(input.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out var newBalance))
            {
                await Application.Current.MainPage.DisplayAlert("提示", "请输入有效的金额。", "好的");
                return;
            }

            // 1) Read current balance (after syncing from records to be accurate)
            await _accountRepo.SyncAccountsFromRecordsAsync();
            var accounts = await _accountRepo.GetAccountsWithBalancesAsync();
            var self = accounts.FirstOrDefault(a =>
                a.Name.Equals(AccountName, StringComparison.OrdinalIgnoreCase));
            var current = self?.Balance ?? 0m;

            // 2) Compute delta: positive => Income, negative => Expense
            var delta = newBalance - current;
            if (delta == 0m)
            {
                // Nothing to change; still refresh the header to be safe
                await LoadHeaderAsync();
                return;
            }

            var type = delta > 0 ? "收入" : "支出";
            var amount = Math.Abs(delta);

            // 3) Choose which book to write into (keep canonical default as "Default")
            string book;
            try
            {
                book = Preferences.Default.Get(PrefKeyCurrentBook, "Default");
            }
            catch
            {
                book = Preferences.Get(PrefKeyCurrentBook, "Default");
            }
            if (string.IsNullOrWhiteSpace(book))
                book = "Default";

            // 4) Persist an adjustment record so that future sync includes this change
            var adjustment = new Record
            {
                Type = type,
                Amount = amount,
                Category = "余额调整",
                Note = "账户详情页手动调整",
                Timestamp = DateTime.Now,
                Account = AccountName // repository will normalize if needed
            };

            await _recordRepo.SaveAsync(book, adjustment);

            // 5) Refresh header (sync -> read) and current month list
            await LoadHeaderAsync();
            await LoadMonthlyRecordsAsync();

            await Application.Current.MainPage.DisplayAlert("提示", "余额已更新", "好的");
        }
    }

    public record MonthOption(int Year, int Month)
    {
        public string Display => $"{Year}-{Month:00}";
    }
}
