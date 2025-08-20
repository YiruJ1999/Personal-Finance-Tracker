using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Storage;                 // Preferences
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Services;
using PersonalFinanceTracker.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
        }

        // -------- Observable properties --------

        [ObservableProperty]
        private string currentBook;  // display name of current book

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
        private bool _isLoaded; // to prevent double-loading on page reappearing

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
            // 0) Prevent double-loading when page reappears
            if (_isLoaded) return;

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

            _isLoaded = true;
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

            // Fetch all records for the current book id
            var allRecords = await _recordRepository.ListAsync(bookId);

            var today = DateTime.Today;
            TodayRecords = allRecords
                .Where(r => r.Timestamp.Date == today)
                .OrderByDescending(r => r.Timestamp)
                .ToList();

            var monthStart = new DateTime(today.Year, today.Month, 1);
            var monthly = allRecords
                .Where(r => r.Timestamp >= monthStart && r.Timestamp <= today)
                .ToList();

            var income = monthly.Where(r => r.Type == "收入").Sum(r => r.Amount);
            var expense = monthly.Where(r => r.Type == "支出").Sum(r => r.Amount);

            MonthlySummaryData = new List<MonthlySummaryItem>
            {
                new("本月收入", income),
                new("本月支出", expense),
                new("收支差额", income - expense),
                new("本月预算", (decimal) MonthlyBugget)
            };

            // Category chart for income (adjust to your needs)
            MonthlyCategoryChartData = monthly
                .Where(r => r.Type == "收入")
                .GroupBy(r => r.Category)
                .Select(g => new CategorySummaryItem
                {
                    Category = g.Key,
                    Amount = g.Sum(r => r.Amount)
                }).ToList();
        }

        // Persist MonthlyBugget whenever changed (store by bookId; also update legacy once for backward-compat)
        partial void OnMonthlyBuggetChanged(double value)
        {
            var id = Preferences.Default.Get(PrefKeyCurrentBookId, 0);
            if (id > 0) Preferences.Default.Set(BudgetKeyById(id), value);

            // update legacy name-key only if it exists to avoid overwriting other books
            var legacyName = Preferences.Default.Get(PrefKeyCurrentBookName, DefaultBookDisplayName);
            Preferences.Default.Set(BudgetKeyByLegacyName(legacyName), value);
        }

        // -------- Helpers --------

        // Ensure we have a valid current book id; migrate from legacy name if needed
        private async Task<int> EnsureCurrentBookIdAsync()
        {
            var id = Preferences.Default.Get(PrefKeyCurrentBookId, 0);
            if (id > 0)
            {
                CurrentBookId = id;
                // Keep display name updated for UI
                var name = Preferences.Default.Get(PrefKeyCurrentBookName, DefaultBookDisplayName);
                CurrentBook = name;
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
    }

    public record MonthlySummaryItem(string Label, decimal Amount);

    public class CategorySummaryItem
    {
        public string Category { get; set; }
        public decimal Amount { get; set; }
    }
}
