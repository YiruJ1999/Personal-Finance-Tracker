// Personal Finance Tracker
// File: PersonalFinanceTracker/PageModels/MainPageModel.cs
// Purpose: Coordinates page state, commands, navigation, and data loading for a MAUI page.

using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Storage;                 // Preferences
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Services;
using PersonalFinanceTracker.Data;
using PersonalFinanceTracker.Resources.Strings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;

namespace PersonalFinanceTracker.PageModels
{
    public partial class MainPageModel : ObservableObject
    {
        private readonly RecordRepository _recordRepository;
        private readonly DatabaseService _databaseService;
        private readonly SeedDataService _seedDataService;
        private readonly BookRepository _bookRepository;
        private readonly IServiceProvider _sp;

        // Legacy name-based preference key (kept for migration)
        private const string PrefKeyCurrentBookName = "current_book";
        // New id-based preference key
        private const string PrefKeyCurrentBookId = "current_book_id";
        private const string DefaultBookDisplayName = "默认";

        private decimal _lastIncome;
        private decimal _lastExpense;
        private decimal _lastBudget;

        public MainPageModel(
            RecordRepository recordRepository,
            DatabaseService databaseService,
            SeedDataService seedDataService,
            BookRepository bookRepository,
            IServiceProvider sp)
        {
            _recordRepository = recordRepository;
            _databaseService = databaseService;
            _seedDataService = seedDataService;
            _bookRepository = bookRepository;
            _sp = sp;
            CurrencyManager.CurrencyChanged += OnCurrencyChanged;
            LanguageManager.LanguageChanged += (_, __) =>
            {
                OnPropertyChanged(nameof(CurrentBookText));
                RebuildMonthlySummaryItems();              
                OnPropertyChanged(nameof(MonthlySummaryData));
                if (TodayRecords != null)
                    TodayRecords = TodayRecords.ToList();
            };
        }

        // -------- Observable properties --------

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CurrentBookText))]
        private string currentBook;  // display name of current book

        public string CurrentBookText =>
        string.IsNullOrWhiteSpace(CurrentBook)
            ? string.Empty
            : $"{AppResources.Label_CurrentBook}{CurrentBook}";


        [ObservableProperty]
        private int currentBookId;   // id of current book (source of truth)

        [ObservableProperty]
        private List<Record> todayRecords;

        [ObservableProperty]
        private List<MonthlySummaryItem> monthlySummaryData;

        [ObservableProperty]
        private double monthlyBugget;

        [ObservableProperty]
        private List<CategorySummaryItem> monthlyCategoryChartData;

        [ObservableProperty]
        private bool isRefreshing;

        // -------- Commands --------

        [RelayCommand]
        private async Task Refresh()
        {
            try
            {
                IsRefreshing = true;
                await LoadFinancialData();
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        [RelayCommand]
        private async Task AddRecord()
        {
            await Shell.Current.GoToAsync("addrecord");
        }

        [RelayCommand]
        private async Task ViewRecord()
        {
            await Shell.Current.GoToAsync("ViewRecordPage");
        }

        [RelayCommand]
        private async Task ViewPersonal()
        {
            await Shell.Current.GoToAsync("personalinfopage");
        }

        [RelayCommand]
        private async Task GoToBook()
        {
            await Shell.Current.GoToAsync("bookpage");
        }

        [RelayCommand]
        private async Task ViewAccount()
        {
            await Shell.Current.GoToAsync("ViewAccountPage");
        }

        [RelayCommand]
        public async Task Appearing()
        {

            // 1) Init DB connection
            await _databaseService.InitAsync();

            // 2) (Optional, DEBUG only) one-time forced reseed for developers
            //    Toggle this to true ONLY when you want to rebuild the DB from seed once.
#if DEBUG
            const bool ForceReseed = false; // set to true temporarily when you need a clean reseed
            if (ForceReseed)
            {
                // Clear DB and reset the seed flag
                await _databaseService.ClearDatabaseAsync();
                Preferences.Default.Set("is_seeded", false);
            }
#endif

            // 3) One-time seed guarded by the preference flag
            if (!Preferences.Default.Get("is_seeded", false))
            {
                try
                {
                    await _seedDataService.LoadSeedDataAsync();

                    Preferences.Default.Set("is_seeded", true);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Seed] failed: {ex}");
                    await Shell.Current.DisplayAlert("Init Error", "Failed to load initial data.", "OK");
                    return;
                }
            }

            // 4) Load data for current book (accounts, etc.)
            await LoadFinancialData();

            // 5) Load budget (prefer id-based; fallback legacy once)
            var curId = await EnsureCurrentBookIdAsync();
            MonthlyBugget = Preferences.Default.Get(
                BudgetKeyById(curId),
                Preferences.Default.Get(
                    BudgetKeyByLegacyName(Preferences.Default.Get(PrefKeyCurrentBookName, DefaultBookDisplayName)),
                    0.0));

            // 6) Update display name for UI
            CurrentBook = Preferences.Default.Get(PrefKeyCurrentBookName, DefaultBookDisplayName);
        }


        [RelayCommand]
        private async Task BuggetTapped()
        {
            var popup = _sp.GetRequiredService<BuggetPopup>();
            var result = await Shell.Current.ShowPopupAsync(popup);

            if (result is string amountStr && double.TryParse(amountStr, out var amount))
            {
                MonthlyBugget = amount;
            }
        }

        // Switch current book at runtime (bind this to a Picker using names if needed)
        [RelayCommand]
        private async Task ChangeBook(string newBookName)
        {
            // Resolve or create the book by display name
            var display = string.IsNullOrWhiteSpace(newBookName) ? DefaultBookDisplayName : newBookName.Trim();
            var book = await _bookRepository.EnsureBookAsync(display);

            // Persist both id (new) and name (legacy)
            Preferences.Default.Set(PrefKeyCurrentBookId, book.Id);
            Preferences.Default.Set(PrefKeyCurrentBookName, book.Name);

            // Update UI state
            CurrentBookId = book.Id;
            CurrentBook = book.Name;

            // Reload
            await LoadFinancialData();
        }

        // -------- Data loading (ID-based) --------

        public async Task LoadFinancialData()
        {
            var bookId = await EnsureCurrentBookIdAsync();
            var allRaw = await _recordRepository.ListAsync(bookId);

            // Normalize legacy timestamps to UTC BEFORE filtering
            var allRecords = allRaw.Select(r =>
            {
                // NOTE: Record is a class, so mutate its Timestamp in-place for consistency.
                // If you prefer immutable, create a shallow copy with the new timestamp.
                r.Timestamp = NormalizeToUtc(r.Timestamp);
                return r;
            }).ToList();

            // --- Build local day boundaries then convert to UTC for querying ---
            var localTodayStart = DateTime.Today;                // local start of "today"
            var localTomorrowStart = localTodayStart.AddDays(1); // exclusive

            var utcTodayStart = TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(localTodayStart, DateTimeKind.Local));
            var utcTomorrowStart = TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(localTomorrowStart, DateTimeKind.Local));

            TodayRecords = allRecords
                .Where(r => r.Timestamp >= utcTodayStart && r.Timestamp < utcTomorrowStart)
                .OrderByDescending(r => r.Timestamp)
                .ToList();

            // --- Month boundaries in local time, then convert to UTC ---
            var localMonthStart = new DateTime(localTodayStart.Year, localTodayStart.Month, 1);
            var localMonthEndExclusive = localMonthStart.AddMonths(1);

            var utcMonthStart = TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(localMonthStart, DateTimeKind.Local));
            var utcMonthEndExclusive = TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(localMonthEndExclusive, DateTimeKind.Local));

            var monthly = allRecords
                .Where(r => r.Timestamp >= utcMonthStart && r.Timestamp < utcMonthEndExclusive)
                .ToList();

            var income = monthly.Where(r => r.Type == "收入").Sum(r => r.Amount);
            var expense = monthly.Where(r => r.Type == "支出").Sum(r => r.Amount);

            _lastIncome = income;
            _lastExpense = expense;
            _lastBudget = (decimal)MonthlyBugget;

            RebuildMonthlySummaryItems();

            MonthlyCategoryChartData = monthly
                .GroupBy(r => r.Category)
                .Select(g => new CategorySummaryItem
                {
                    Category = g.Key,
                    Amount = (double)g.Sum(r => r.Type == "支出" ? -r.Amount : r.Amount)
                })
                .OrderByDescending(x => Math.Abs(x.Amount))
                .ToList();

            // Diagnostics: count & sample
            System.Diagnostics.Debug.WriteLine($"[Main] all={allRecords.Count}, today={TodayRecords.Count}, monthly={monthly.Count}");
            System.Diagnostics.Debug.WriteLine($"[Main] utcTodayStart={utcTodayStart:o}, utcTomorrowStart={utcTomorrowStart:o}");
            if (allRecords.Count > 0)
                System.Diagnostics.Debug.WriteLine($"[Main] sample ts={allRecords[0].Timestamp:o}, kind={allRecords[0].Timestamp.Kind}");
        }



        // Persist MonthlyBugget whenever changed (store by bookId; also update legacy once for backward-compat)
        partial void OnMonthlyBuggetChanged(double value)
        {
            var id = Preferences.Default.Get(PrefKeyCurrentBookId, 0);
            if (id > 0) Preferences.Default.Set(BudgetKeyById(id), value);

            // update legacy name-key only if it exists to avoid overwriting other books
            var legacyName = Preferences.Default.Get(PrefKeyCurrentBookName, DefaultBookDisplayName);
            Preferences.Default.Set(BudgetKeyByLegacyName(legacyName), value);

            // Refresh summary to reflect new budget
            _lastBudget = (decimal)value;
            _ = Refresh();
        }

        // -------- Helpers --------

        // Ensure we have a valid current book id; migrate from legacy name if needed
        private async Task<int> EnsureCurrentBookIdAsync()
        {
            var id = Preferences.Default.Get(PrefKeyCurrentBookId, 0);
            if (id > 0)
            {
                CurrentBookId = id;
                CurrentBook = Preferences.Default.Get(PrefKeyCurrentBookName, DefaultBookDisplayName);
                return id;
            }

            // Fallback to legacy name, then ensure/create a Book row and persist the id
            var legacyName = Preferences.Default.Get(PrefKeyCurrentBookName, DefaultBookDisplayName);
            var book = await _bookRepository.EnsureBookAsync(
                string.IsNullOrWhiteSpace(legacyName) ? DefaultBookDisplayName : legacyName.Trim());

            Preferences.Default.Set(PrefKeyCurrentBookId, book.Id);
            Preferences.Default.Set(PrefKeyCurrentBookName, book.Name);

            CurrentBookId = book.Id;
            CurrentBook = book.Name;
            return book.Id;
        }

        private static string BudgetKeyById(int bookId) => $"monthlybugget_{bookId}";
        private static string BudgetKeyByLegacyName(string bookName) => $"monthlybugget_{bookName}";
        private async void OnCurrencyChanged(object? sender, EventArgs e)
        {
            // Ensure UI updates happen on UI thread
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await Refresh(); 
            });
        }
        public void UnsubscribeCurrency() // ADD (optional)
        {
            CurrencyManager.CurrencyChanged -= OnCurrencyChanged;
        }

        partial void OnCurrentBookChanged(string value)
        {
            System.Diagnostics.Debug.WriteLine(value);
            OnPropertyChanged(nameof(CurrentBookText));
        }

        // Rebuild monthly summary with localized labels
        private void RebuildMonthlySummaryItems()
        {
            MonthlySummaryData = new List<MonthlySummaryItem>
            {
        new(AppResources.Label_MonthIncome,  _lastIncome),
        new(AppResources.Label_MonthExpense, _lastExpense),
        new(AppResources.Label_BalanceDiff,  _lastIncome - _lastExpense),
        new(AppResources.Label_MonthBudget,  (decimal) _lastBudget),
            };
        }

        // Normalize DateTime to UTC assuming Unspecified means local wall-clock time.
        private static DateTime NormalizeToUtc(DateTime dt)
        {
            if (dt.Kind == DateTimeKind.Utc)
                return dt;

            if (dt.Kind == DateTimeKind.Local)
                return dt.ToUniversalTime();

            // Unspecified: treat as LOCAL first, then convert to UTC.
            return DateTime.SpecifyKind(dt, DateTimeKind.Local).ToUniversalTime();
        }

    }

    public record MonthlySummaryItem(string Label, decimal Amount);

    public class CategorySummaryItem
    {
        public string Category { get; set; } = string.Empty;
        public double Amount { get; set; }
    }
}

