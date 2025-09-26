namespace PersonalFinanceTracker.PageModels
{
    public partial class ViewRecordPageModel : ObservableObject
    {
        private readonly DatabaseService _dbService;
        private readonly RecordRepository _recordRepository;
        private readonly BookRepository _bookRepository;

        // Legacy name-based preference key (kept for backward compatibility)
        private const string PrefKeyCurrentBookName = "current_book";
        // New id-based preference key (source of truth)
        private const string PrefKeyCurrentBookId = "current_book_id";

        // Paging state
        private int _currentPage = 1;
        private bool _isLoading = false;
        private int _pageSize = 15; // default page size

        public ViewRecordPageModel(
            DatabaseService dbService,
            RecordRepository recordRepository,
            BookRepository bookRepository)
        {
            _dbService = dbService;
            _recordRepository = recordRepository;
            _bookRepository = bookRepository;

            Records = new ObservableCollection<Record>();

            // When a record is saved anywhere, refresh this list
            WeakReferenceMessenger.Default.Register<RecordSavedMessage>(this, async (_, __) =>
            {
                await ResetAndReloadAsync();
            });
        }

        // -------- Bindable state --------

        // Display name of the active book (for UI only).
        [ObservableProperty]
        private string currentBook = "默认";

        // ID of the active book (the real key used by repositories).
        [ObservableProperty]
        private int currentBookId;


        // Paged records bound to the UI.
        [ObservableProperty]
        private ObservableCollection<Record> records;

        // Months for the picker.
        [ObservableProperty] private ObservableCollection<MonthOption> months = new();
        [ObservableProperty] private MonthOption? selectedMonth;

        private void EnsureMonthsInitialized()
        {
            if (Months.Count > 0) return;

            var now = DateTime.Now;
            var firstOfThisMonth = new DateTime(now.Year, now.Month, 1);

            for (int i = 0; i < 24; i++)
            {
                var start = firstOfThisMonth.AddMonths(-i);
                Months.Add(new MonthOption(start.Year, start.Month));
            }

            SelectedMonth = Months[0]; 
        }
        private static void GetMonthRange(MonthOption m, out DateTime start, out DateTime end)
        {
            start = new DateTime(m.Year, m.Month, 1, 0, 0, 0, DateTimeKind.Local);
            end = start.AddMonths(1);
        }

        partial void OnSelectedMonthChanged(MonthOption? oldValue, MonthOption? newValue)
        {
            if (newValue != null)
                MainThread.BeginInvokeOnMainThread(async () => await ResetAndReloadAsync());
        }

        // -------- Commands / Lifecycle --------

        /// <summary>
        /// Page appearing: ensure DB, resolve current bookId (migrating legacy name if needed),
        /// then load the first page.
        /// </summary>
        [RelayCommand]
        public async Task Appearing()
        {
            EnsureMonthsInitialized();
            await _dbService.InitAsync();

            // Resolve book id (migrate from legacy name if needed)
            var (bookId, displayName) = await EnsureCurrentBookAsync(); 
            CurrentBookId = bookId;
            CurrentBook = displayName;

            await ResetAndReloadAsync();
        }

        /// <summary>
        /// Load next page (infinite scroll / "Load more" pattern).
        /// </summary>
        [RelayCommand]
        public async Task LoadNextPage()
        {
            if (_isLoading) return;
            if (CurrentBookId <= 0)
            {
                await AppShell.DisplaySnackbarAsync("No valid book selected");
                return;
            }

            _isLoading = true;
            try
            {
                GetMonthRange(SelectedMonth, out var start, out var end);
                var page = await _recordRepository.GetRecordsPagedAsync(CurrentBookId, _currentPage, _pageSize, start, end);
                if (page != null && page.Count > 0)
                {
                    foreach (var r in page)
                        Records.Add(r);

                    // Advance page only when we received some data
                    _currentPage++;
                }
            }
            catch (Exception ex)
            {
                await AppShell.DisplaySnackbarAsync($"Failed to load more:{ex.Message}");
            }
            finally
            {
                _isLoading = false;
            }
        }

        // -------- Helpers --------

        /// <summary>
        /// Reset paging to page 1 and reload first page.
        /// </summary>
        private async Task ResetAndReloadAsync()
        {
            _currentPage = 1;

            if (CurrentBookId <= 0 || SelectedMonth == null)
                return;

            try
            {
                GetMonthRange(SelectedMonth, out var start, out var end);
                // fetch first page without touching Records
                var firstPage = await _recordRepository.GetRecordsPagedAsync(CurrentBookId, 1, _pageSize, start, end);

                // replace the collection in one shot (no intermediate empty state)
                Records = new ObservableCollection<Record>(firstPage ?? new List<Record>());

                // advance page index if we did get data
                _currentPage = (firstPage != null && firstPage.Count > 0) ? 2 : 1;
            }
            catch (Exception ex)
            {
                await AppShell.DisplaySnackbarAsync($"Failed to load more:{ex.Message}");
            }
        }

        /// <summary>
        /// Optional: load the whole list at once (avoid for very large datasets).
        /// </summary>
        public async Task LoadFinancialData()
        {
            try
            {
                if (CurrentBookId <= 0)
                {
                    await AppShell.DisplaySnackbarAsync("No valid book was selected.");
                    return;
                }

                var list = await _recordRepository.ListAsync(CurrentBookId);
                Records = new ObservableCollection<Record>(list);
            }
            catch (Exception ex)
            {
                await AppShell.DisplaySnackbarAsync($"Failed to load more:{ex.Message}");
            }
        }

        /// <summary>
        /// Resolve or create the current book by ID. If only legacy name exists, migrate and persist the id.
        /// Returns the bookId and outputs the display name.
        /// </summary>
        private async Task<(int bookId, string displayName)> EnsureCurrentBookAsync()
        {
            const string PrefKeyCurrentBookId = "current_book_id";
            const string PrefKeyCurrentBook = "current_book";

            var id = Preferences.Default.Get(PrefKeyCurrentBookId, 0);
            if (id > 0)
            {
                var name = Preferences.Default.Get(PrefKeyCurrentBook, "默认");
                return (id, name);
            }

            // legacy name -> ensure/create book -> persist id+name
            var legacyName = Preferences.Default.Get(PrefKeyCurrentBook, "默认");
            var book = await _bookRepository.EnsureBookAsync(
                string.IsNullOrWhiteSpace(legacyName) ? "默认" : legacyName.Trim());

            Preferences.Default.Set(PrefKeyCurrentBookId, book.Id);
            Preferences.Default.Set(PrefKeyCurrentBook, book.Name);

            return (book.Id, book.Name);
        }

        [RelayCommand]
        private async Task OpenRecordDetail(Record? record)
        {
            if (record == null) return;

            // navigate to record detail with Shell route and query
            var query = new Dictionary<string, object>
            {
                ["recordId"] = record.Id,
                ["bookId"] = CurrentBookId
            };
            await Shell.Current.GoToAsync(nameof(RecordDetailPage), true, query);
        }



    }

}
