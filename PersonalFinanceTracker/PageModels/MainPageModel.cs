using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Services;

namespace PersonalFinanceTracker.PageModels
{
    public partial class MainPageModel : ObservableObject
    {
        private readonly RecordRepository _recordRepository;
        private readonly DatabaseService _databaseService;
        private readonly SeedDataService _seedDataService;
        private readonly IServiceProvider _sp;

        // Key for persisting current book name
        private const string PrefKeyCurrentBook = "current_book";
        private const string defaultBookName = "Default";

        public MainPageModel(RecordRepository recordRepository, DatabaseService databaseService, SeedDataService seedDataService, IServiceProvider sp)
        {
            _recordRepository = recordRepository;
            _databaseService = databaseService;
            _seedDataService = seedDataService;
            _sp = sp;
        }

        // -------- Observable properties --------

        [ObservableProperty]
        private string currentBook;  // The active book name used by repositories

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
            System.Diagnostics.Debug.WriteLine("点击了账户按钮！");
            await Shell.Current.GoToAsync("ViewAccountPage");
        }

        [RelayCommand]
        public async Task Appearing()
        {
            System.Diagnostics.Debug.WriteLine("MainPageModel Appearing");
            // Initialize DB connection (generic; no Record-specific logic here)
            await _databaseService.InitAsync();

            // Un-comment the next lines if you want to reset the seed state
             //Preferences.Default.Remove("is_seeded");
             //await _databaseService.ClearDatabaseAsync();

            // Seed once per app (optionally per book; see note below)
            if (!Preferences.Default.ContainsKey("is_seeded"))
            {
                // If your SeedDataService should seed per book, prefer:
                // await _seedDataService.LoadSeedDataAsync(CurrentBook);
                await _seedDataService.LoadSeedDataAsync();

                Preferences.Default.Set("is_seeded", true);
                Preferences.Default.Set(
                    $"monthlybugget_{Preferences.Default.Get(PrefKeyCurrentBook, defaultBookName)}"
                    , 0.0);
            }

            // Load page data using the active book
            await LoadFinancialData();

            MonthlyBugget = Preferences.Default.Get(
                $"monthlybugget_{Preferences.Default.Get(PrefKeyCurrentBook, defaultBookName)}"
                , 0.0);
            CurrentBook = Preferences.Default.Get(PrefKeyCurrentBook, defaultBookName);
        }

        // Switch current book at runtime (bind this to a Picker if needed)
        [RelayCommand]
        private async Task ChangeBook(string newBook)
        {
            // Persist and reload data for the selected book
            CurrentBook = string.IsNullOrWhiteSpace(newBook) ? defaultBookName : newBook.Trim();
            Preferences.Default.Set(PrefKeyCurrentBook, CurrentBook);
            await LoadFinancialData();
        }

        // -------- Data loading --------

        public async Task LoadFinancialData()
        {
            // IMPORTANT: repository calls now require the book name
            var allRecords = await _recordRepository.ListAsync(CurrentBook);

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
            };

            MonthlyCategoryChartData = monthly
                .Where(r => r.Type == "收入")
                .GroupBy(r => r.Category)
                .Select(g => new CategorySummaryItem
                {
                    Category = g.Key,
                    Amount = g.Sum(r => r.Amount)
                }).ToList();

            foreach (var record in allRecords)
            {
                System.Diagnostics.Debug.WriteLine(record.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            }
            System.Diagnostics.Debug.WriteLine("系统 DateTime.Today 是：" + DateTime.Today.ToString("yyyy-MM-dd"));
        }

        // Persist MonthlyBugget whenever changed
        partial void OnMonthlyBuggetChanged(double value)
        {
            Preferences.Default.Set(
                $"monthlybugget_{Preferences.Default.Get(PrefKeyCurrentBook,defaultBookName)}"
                , value);
        }

        [RelayCommand]
        private async Task BuggetTapped()
        {

            var popup = _sp.GetRequiredService<BuggetPopup>();
            var result = await Shell.Current.ShowPopupAsync(popup);

            if (result is string amountStr && decimal.TryParse(amountStr, out var amount))
            {
                MonthlyBugget = (double)amount;
            }
        }
    }

    public record MonthlySummaryItem(string Label, decimal Amount);

    public class CategorySummaryItem
    {
        public string Category { get; set; }
        public decimal Amount { get; set; }
    }
}
