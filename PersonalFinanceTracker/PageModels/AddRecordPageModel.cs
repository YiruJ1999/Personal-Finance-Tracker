using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Services;
using PersonalFinanceTracker.Data;
using PersonalFinanceTracker.Messages;
using Microsoft.Maui.Storage;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System;

namespace PersonalFinanceTracker.PageModels
{
    public partial class AddRecordPageModel : ObservableObject
    {
        private readonly DatabaseService _dbService;
        private readonly RecordRepository _recordRepository;
        private readonly AccountRepository _accountRepository;

        // The same key used elsewhere to persist the active book
        private const string PrefKeyCurrentBook = "current_book";

        public AddRecordPageModel(
            DatabaseService dbService,
            RecordRepository recordRepository,
            AccountRepository accountRepository)
        {
            _dbService = dbService;
            _recordRepository = recordRepository;
            _accountRepository = accountRepository;

            // default to expense
            IsExpenseSelected = true;
            Categories = new ObservableCollection<CategoryModel>(CategoryData.GetExpenseCategories());
            SelectedDate = DateTime.Now;

            // init current book (keep canonical "Default" to match book_Default)
            CurrentBook = Preferences.Default.Get(PrefKeyCurrentBook, "Default");

            // fire-and-forget load accounts
            _ = LoadAccountsAsync();
        }

        // ----- state -----
        [ObservableProperty] private string currentBook;
        [ObservableProperty] private bool isExpenseSelected;
        [ObservableProperty] private ObservableCollection<CategoryModel> categories;
        [ObservableProperty] private CategoryModel selectedCategory;
        [ObservableProperty] private string amount;      // user input string
        [ObservableProperty] private string note;        // optional
        [ObservableProperty] private DateTime selectedDate;

        public bool IsIncomeSelected => !IsExpenseSelected;
        partial void OnIsExpenseSelectedChanged(bool value)
        {
            OnPropertyChanged(nameof(IsIncomeSelected));
        }

        // accounts
        [ObservableProperty] private ObservableCollection<Account> accounts;
        [ObservableProperty] private Account selectedAccount;

        [RelayCommand]
        public async Task Appearing()
        {
            await _dbService.InitAsync();
            CurrentBook = Preferences.Default.Get(PrefKeyCurrentBook, "Default");
            await EnsureDefaultAccountsAndReloadAsync();
        }

        // ----- category toggle -----
        [RelayCommand]
        private void SelectExpense()
        {
            IsExpenseSelected = true;
            Categories = new ObservableCollection<CategoryModel>(CategoryData.GetExpenseCategories());
            SelectedCategory = null; // reset selection to avoid stale category
        }

        [RelayCommand]
        private void SelectIncome()
        {
            IsExpenseSelected = false;
            Categories = new ObservableCollection<CategoryModel>(CategoryData.GetIncomeCategories());
            SelectedCategory = null;
        }

        // ----- add new account (bind to a small "+" button) -----
        [RelayCommand]
        private async Task AddAccount(string? name)
        {
            // Fallback default name when user leaves it empty
            var finalName = string.IsNullOrWhiteSpace(name) ? "默认" : name.Trim();

            await _accountRepository.AddAccountAsync(finalName);
            await LoadAccountsAsync();

            // Select the account we just added
            SelectedAccount = Accounts.FirstOrDefault(a =>
                a.Name.Equals(finalName, StringComparison.OrdinalIgnoreCase));
        }

        // ----- save -----
        [RelayCommand]
        private async Task Save()
        {
            await _dbService.InitAsync();

            // 1) Amount: default to 0 when empty/invalid, using current culture
            var amountText = (Amount ?? string.Empty).Trim();
            if (!decimal.TryParse(amountText, NumberStyles.Number, CultureInfo.CurrentCulture, out var amt))
                amt = 0m;

            // 2) Category: if not selected, pick the first available in current list
            if (SelectedCategory == null && Categories != null && Categories.Count > 0)
                SelectedCategory = Categories[0];

            // 3) Note: never null
            var finalNote = Note ?? string.Empty;

            // 4) Date: if not set, fallback to now (local)
            var ts = SelectedDate == default
                ? DateTime.Now
                : DateTime.SpecifyKind(SelectedDate, DateTimeKind.Local);

            // 5) Account: ensure at least one exists, and fallback to "默认"
            if (SelectedAccount == null && Accounts?.Count > 0)
                SelectedAccount = Accounts[0];
            var finalAccountName = SelectedAccount?.Name ?? "默认";

            // 6) Type: default to "支出" when not set
            var finalType = IsExpenseSelected ? "支出" : "收入";

            // Build record (Record has string Account property)
            var record = new Record
            {
                Amount = amt,
                Category = SelectedCategory?.Name ?? "未分类",
                Note = finalNote,
                Timestamp = ts,
                Type = finalType,
                Account = finalAccountName  // normalized again in repository
            };

            // Write to the current book/table via repository
            await _recordRepository.SaveAsync(CurrentBook, record);

            // Notify and navigate back
            WeakReferenceMessenger.Default.Send(new RecordSavedMessage());
            await Shell.Current.GoToAsync("..");

            // Reset UI
            Amount = string.Empty;
            Note = string.Empty;
            SelectedCategory = null;
            IsExpenseSelected = true;
            Categories = new ObservableCollection<CategoryModel>(CategoryData.GetExpenseCategories());
            SelectedDate = DateTime.Now;

            System.Diagnostics.Debug.WriteLine($"[BeforeSave] SelectedAccount={SelectedAccount?.Name}");
            System.Diagnostics.Debug.WriteLine($"[Save] Account to write = {finalAccountName}");
        }

        // ----- helpers -----
        private async Task LoadAccountsAsync(string? preserveSelectionByName = null)
        {
            await _accountRepository.EnsureDatabaseInitializedAsync();

            var list = await _accountRepository.ListAsync();
            Accounts = new ObservableCollection<Account>(list);

            var target = preserveSelectionByName ?? SelectedAccount?.Name;

            // Restore selection by name (case-insensitive, trimmed)
            if (!string.IsNullOrWhiteSpace(target))
                SelectedAccount = Accounts.FirstOrDefault(a =>
                    string.Equals(a.Name?.Trim(), target.Trim(), StringComparison.OrdinalIgnoreCase));

            // If still null, select first
            if (SelectedAccount == null && Accounts.Count > 0)
                SelectedAccount = Accounts[0];
        }

        private async Task EnsureDefaultAccountsAndReloadAsync()
        {
            await _accountRepository.EnsureDatabaseInitializedAsync();
            var list = await _accountRepository.ListAsync();

            if (list == null || list.Count == 0)
            {
                // Seed three common accounts (you can adjust as needed)
                await _accountRepository.AddAccountAsync("现金");
                await _accountRepository.AddAccountAsync("储蓄卡");
                await _accountRepository.AddAccountAsync("信用卡");
            }

            await LoadAccountsAsync(SelectedAccount?.Name);
        }
    }
}
