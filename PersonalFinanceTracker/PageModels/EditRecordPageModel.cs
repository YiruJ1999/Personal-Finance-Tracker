// Personal Finance Tracker
// File: PersonalFinanceTracker/PageModels/EditRecordPageModel.cs
// Purpose: Coordinates page state, commands, navigation, and data loading for a MAUI page.

namespace PersonalFinanceTracker.PageModels
{
    public partial class EditRecordPageModel : ObservableObject, IQueryAttributable
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
            SelectedDate = DateTime.Now;

            // Listen for language changes to update category names
            LanguageManager.LanguageChanged += LanguageChangedHandler;
        }

        // ---------- shared bindable states ----------
        [ObservableProperty] private bool isExpenseSelected;
        [ObservableProperty] private ObservableCollection<CategoryModel> categories = new();
        [ObservableProperty] private CategoryModel selectedCategory;
        [ObservableProperty] private string amount;
        [ObservableProperty] private string note;
        [ObservableProperty] private DateTime selectedDate;
        [ObservableProperty] private ObservableCollection<Account> accounts = new();
        [ObservableProperty] private Account selectedAccount;
        public bool IsIncomeSelected => !IsExpenseSelected;
        private int _categoryColumns = 4;
        public int CategoryColumns { get => _categoryColumns; set { _categoryColumns = value; OnPropertyChanged(); } }

        // Track current ids
        private int _bookId;
        private int _recordId;

        partial void OnIsExpenseSelectedChanged(bool value)
        {
            OnPropertyChanged(nameof(IsIncomeSelected));
        }

        public async void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            await _dbService.InitAsync();

            // 1) get ids
            _bookId = query.TryGetValue("bookId", out var b) && int.TryParse(b?.ToString(), out var bid) ? bid : 0;
            _recordId = query.TryGetValue("recordId", out var r) && int.TryParse(r?.ToString(), out var rid) ? rid : 0;
            if (_recordId <= 0) { await Shell.Current.DisplayAlert("Error", "recordId missing", "OK"); return; }

            // 2) load record
            var rec = await _recordRepository.GetByIdAsync(_bookId, _recordId);
            if (rec is null) { await Shell.Current.DisplayAlert("Error", "Record not found", "OK"); await Shell.Current.GoToAsync(".."); return; }

            // 3) set type first -> build category list accordingly
            IsExpenseSelected = string.Equals(rec.Type, "支出", StringComparison.OrdinalIgnoreCase);
            RefreshCategories(preserveSelection: false);           // rebuild list by type

            // 4) select category by name (fallback to first)
            SelectedCategory = Categories.FirstOrDefault(c => string.Equals(c.Name, rec.Category, StringComparison.OrdinalIgnoreCase))
                               ?? Categories.FirstOrDefault();
            System.Diagnostics.Debug.WriteLine($"[EditVM] Categories.Count = {Categories?.Count}");
            foreach (var c in Categories?.Take(3) ?? Enumerable.Empty<CategoryModel>())
                System.Diagnostics.Debug.WriteLine($"[EditVM] Cat: {c?.Name}, Icon={c?.Icon}");

            // 5) load accounts and select
            await LoadAccountsAsync();
            SelectedAccount = Accounts.FirstOrDefault(a => a.Id == rec.AccountId) ?? Accounts.FirstOrDefault();

            // 6) amount / note / date
            Amount = rec.Amount.ToString("0.##");
            Note = rec.Note ?? string.Empty;
            SelectedDate = rec.Timestamp.Kind == DateTimeKind.Utc ? rec.Timestamp.ToLocalTime() : rec.Timestamp;
        }

        private void RefreshCategories(bool preserveSelection)
        {
            var prevIcon = preserveSelection ? SelectedCategory?.Icon : null;
            Categories = new ObservableCollection<CategoryModel>(
                IsExpenseSelected ? CategoryData.GetExpenseCategories() : CategoryData.GetIncomeCategories()
            );
            if (preserveSelection && !string.IsNullOrEmpty(prevIcon))
                SelectedCategory = Categories.FirstOrDefault(c => c.Icon == prevIcon) ?? SelectedCategory;
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

        public void Detach()
        {
            LanguageManager.LanguageChanged -= LanguageChangedHandler;
        }
        private void LanguageChangedHandler(object? sender, EventArgs e)
        {
            MainThread.BeginInvokeOnMainThread(() => RefreshCategories(preserveSelection: true));
        }

    }
}

