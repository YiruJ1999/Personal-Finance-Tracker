using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Services;
using PersonalFinanceTracker.Data;
using PersonalFinanceTracker.Messages;
using PersonalFinanceTracker.Resources.Strings;
using Microsoft.Maui.Storage;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System;
using System.Threading.Tasks;

namespace PersonalFinanceTracker.PageModels
{
    public partial class AddRecordPageModel : ObservableObject
    {
        private readonly DatabaseService _dbService;
        private readonly RecordRepository _recordRepository;
        private readonly AccountRepository _accountRepository;
        private readonly BookRepository _bookRepository;

        private const string PrefKeyCurrentBookId = "current_book_id"; // new Id-based
        private const string PrefKeyCurrentBook = "current_book";    // legacy (for migration)

        public AddRecordPageModel(
            DatabaseService dbService,
            RecordRepository recordRepository,
            AccountRepository accountRepository,
            BookRepository bookRepository)
        {
            _dbService = dbService;
            _recordRepository = recordRepository;
            _accountRepository = accountRepository;
            _bookRepository = bookRepository;

            // default to expense
            IsExpenseSelected = true;
            Categories = new ObservableCollection<CategoryModel>(CategoryData.GetExpenseCategories());
            SelectedDate = DateTime.Now;

            // fire-and-forget load accounts
            _ = LoadAccountsAsync();

            // Listen for language changes to update category names
            LanguageManager.LanguageChanged += async (_, __) =>
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    RefreshCategories(preserveSelection: true);
                });
            };
        }

        // ----- state -----
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

        // layout
        private int _categoryColumns = 4;
        public int CategoryColumns
        {
            get => _categoryColumns;
            set { if (_categoryColumns != value) { _categoryColumns = value; OnPropertyChanged(); } }
        }


        [RelayCommand]
        public async Task Appearing()
        {
            await _dbService.InitAsync();
            await EnsureDefaultAccountsAndReloadAsync();
        }

        /// <summary>
        /// Rebuild Categories from CategoryData based on IsExpenseSelected.
        /// Optionally preserve current selection (matched by Icon).
        /// </summary>
        private void RefreshCategories(bool preserveSelection)
        {
            // Remember current selection (by Icon) to restore after rebuilding names
            var prevIcon = preserveSelection ? SelectedCategory?.Icon : null;

            if (IsExpenseSelected)
                Categories = new ObservableCollection<CategoryModel>(CategoryData.GetExpenseCategories());
            else
                Categories = new ObservableCollection<CategoryModel>(CategoryData.GetIncomeCategories());

            // Restore selection by Icon if possible
            if (!string.IsNullOrEmpty(prevIcon))
                SelectedCategory = Categories.FirstOrDefault(c => c.Icon == prevIcon);
        }

        // ----- category toggle -----
        [RelayCommand]
        private void SelectExpense()
        {
            IsExpenseSelected = true;
            RefreshCategories(preserveSelection: false);
            SelectedCategory = null;
        }

        [RelayCommand]
        private void SelectIncome()
        {
            IsExpenseSelected = false;
            RefreshCategories(preserveSelection: false);
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
                string.Equals(a.Name?.Trim(), finalName, StringComparison.OrdinalIgnoreCase));
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

            // 5) Account: ensure at least one exists
            if (SelectedAccount == null && Accounts?.Count > 0)
                SelectedAccount = Accounts[0];
            if (SelectedAccount == null)
            {
                await Shell.Current.DisplayAlert("提示", "请先创建一个账户。", "好的");
                return;
            }

            // 6) BookId: resolve (migrate legacy name if needed)
            var bookId = await GetOrCreateCurrentBookIdAsync();

            var finalType = IsExpenseSelected ? "支出" : "收入";

            // Build record (Id-based; Account name optional for display)
            var record = new Record
            {
                Amount = amt,
                Category = SelectedCategory?.Name ?? "未分类",
                Note = finalNote,
                Timestamp = ts,
                Type = finalType,
                AccountId = SelectedAccount.Id
            };

            await _recordRepository.SaveAsync(bookId, record);

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
        }



        // ----- helpers -----
        private async Task LoadAccountsAsync(string? preserveSelectionByName = null)
        {
            await _accountRepository.EnsureDatabaseInitializedAsync();

            var list = await _accountRepository.ListAsync();
            Accounts = new ObservableCollection<Account>(list);

            var target = preserveSelectionByName ?? SelectedAccount?.Name;

            // Restore selection by name (display only)
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
