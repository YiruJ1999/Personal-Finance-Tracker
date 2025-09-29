using System.Globalization;

namespace PersonalFinanceTracker.PageModels
{
    public partial class EditRecordPageModel : ObservableObject
    {
        private readonly DatabaseService _dbService;
        private readonly RecordRepository _recordRepository;
        private readonly AccountRepository _accountRepository;
        private readonly BookRepository _bookRepository;
        public EditRecordPageModel(
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

        // ---------- shared bindable states ----------
        [ObservableProperty] private bool isExpenseSelected;
        [ObservableProperty] private ObservableCollection<CategoryModel> categories;
        [ObservableProperty] private CategoryModel selectedCategory;
        [ObservableProperty] private string amount;
        [ObservableProperty] private string note;
        [ObservableProperty] private DateTime selectedDate;
        [ObservableProperty] private ObservableCollection<Account> accounts;
        [ObservableProperty] private Account selectedAccount;
        public bool IsIncomeSelected => !IsExpenseSelected;
        private int _categoryColumns = 4;
        public int CategoryColumns { get => _categoryColumns; set { _categoryColumns = value; OnPropertyChanged(); } }

        // Track current ids
        private int _bookId;
        private int _recordId;
        private const string PrefKeyCurrentBookId = "current_book_id"; // new Id-based
        private const string PrefKeyCurrentBook = "current_book";    // legacy (for migration)

        partial void OnIsExpenseSelectedChanged(bool value)
        {
            OnPropertyChanged(nameof(IsIncomeSelected));
        }

        // Receive route parameters and preload record content.
        public async void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            await _dbService.InitAsync();

            // Parse ids from query
            _bookId = query.TryGetValue("bookId", out var b) && int.TryParse(b?.ToString(), out var bid) ? bid : 0;
            _recordId = query.TryGetValue("recordId", out var r) && int.TryParse(r?.ToString(), out var rid) ? rid : 0;
            if (_recordId <= 0)
            {
                await Shell.Current.DisplayAlert("Error", "recordId is missing.", "OK");
                await Shell.Current.GoToAsync("..");
                return;
            }

            // Load target record
            var rec = await _recordRepository.GetByIdAsync(_bookId, _recordId);
            if (rec is null)
            {
                await Shell.Current.DisplayAlert("Error", "Record not found.", "OK");
                await Shell.Current.GoToAsync("..");
                return;
            }

            // Prefill type -> categories
            IsExpenseSelected = string.Equals(rec.Type, "支出", StringComparison.OrdinalIgnoreCase);
            RefreshCategories(preserveSelection: false);

            // Prefill category selection by name (or by icon if you prefer)
            SelectedCategory = Categories.FirstOrDefault(c => c.Name == rec.Category)
                               ?? Categories.FirstOrDefault();

            // Prefill account list and selection
            await LoadAccountsAsync();
            SelectedAccount = Accounts?.FirstOrDefault(a => a.Id == rec.AccountId) ?? Accounts?.FirstOrDefault();

            // Prefill amount / note / date
            Amount = rec.Amount.ToString("0.##");
            Note = rec.Note ?? string.Empty;
            SelectedDate = DateTime.SpecifyKind(rec.Timestamp, DateTimeKind.Local);
        }

        [RelayCommand]
        public async Task Appearing()
        {
            await _dbService.InitAsync();
            await EnsureDefaultAccountsAndReloadAsync();
        }


        // Rebuild Categories from CategoryData based on IsExpenseSelected.
        private void RefreshCategories(bool preserveSelection)
        {
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

        // ----- Update -----
        [RelayCommand]
        private async Task Update()
        {
            await _dbService.InitAsync();

            // Parse amount; default 0 if invalid
            var amt = 0m;
            if (!decimal.TryParse(Amount?.Trim() ?? string.Empty, out amt)) amt = 0m;

            // Ensure category & account
            if (SelectedCategory == null) SelectedCategory = Categories?.FirstOrDefault();
            if (SelectedAccount == null && Accounts?.Count > 0) SelectedAccount = Accounts[0];
            if (SelectedAccount == null)
            {
                await Shell.Current.DisplayAlert("提示", "请先创建一个账户。", "好的");
                return;
            }

            // Record to update
            var rec = await _recordRepository.GetByIdAsync(_bookId, _recordId);
            if (rec is null)
            {
                await Shell.Current.DisplayAlert("Error", "Record not found.", "OK");
                return;
            }

            // Map back
            rec.Amount = amt;
            rec.Category = SelectedCategory?.Name ?? "未分类";
            rec.Note = string.IsNullOrWhiteSpace(Note) ? null : Note.Trim();
            rec.Type = IsExpenseSelected ? "支出" : "收入";
            rec.AccountId = SelectedAccount.Id;
            rec.Timestamp = DateTime.SpecifyKind(SelectedDate == default ? DateTime.Now : SelectedDate, DateTimeKind.Local);

            await _recordRepository.SaveAsync(_bookId, rec);

            // Notify list/detail and go back
            WeakReferenceMessenger.Default.Send(new RecordSavedMessage());
            await Shell.Current.GoToAsync("..");
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
