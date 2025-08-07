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

        public MainPageModel(RecordRepository recordRepository, DatabaseService databaseService, SeedDataService seedDataService)
        {
            _recordRepository = recordRepository;
            _databaseService = databaseService;
            _seedDataService = seedDataService;


        }

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
            //System.Diagnostics.Debug.WriteLine("点击了账单按钮！");
            await Shell.Current.GoToAsync("ViewRecordPage");
        }

        [RelayCommand]
        private async Task ViewPersonal()
        {
            //System.Diagnostics.Debug.WriteLine("点击了按钮！");
            await Shell.Current.GoToAsync("personalinfopage");
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
            await _databaseService.InitAsync();

            if (!Preferences.Default.ContainsKey("is_seeded"))
            {
                await _seedDataService.LoadSeedDataAsync();
                Preferences.Default.Set("is_seeded", true);
                Preferences.Default.Set(nameof(MonthlyBugget), 0.0);
            }

            await LoadFinancialData();
            //await _recordRepository.DeleteAllAsync(); // ← 添加这行
            //Preferences.Default.Remove("is_seeded");  // 再次允许导入一次
            MonthlyBugget = Preferences.Default.Get(nameof(MonthlyBugget), 0.0);

        }

        public async Task LoadFinancialData()
        {
            var allRecords = await _recordRepository.ListAsync();

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

            //System.Diagnostics.Debug.WriteLine($"Record总数: {allRecords.Count}");
            //System.Diagnostics.Debug.WriteLine($"本月记录: {monthly.Count}");
            //System.Diagnostics.Debug.WriteLine($"今日记录: {TodayRecords?.Count}");
            //System.Diagnostics.Debug.WriteLine("=== 所有记录时间（含毫秒）===");

            foreach (var record in allRecords)
            {
                System.Diagnostics.Debug.WriteLine(record.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            }
            System.Diagnostics.Debug.WriteLine("系统 DateTime.Today 是：" + DateTime.Today.ToString("yyyy-MM-dd"));


        }


        partial void OnMonthlyBuggetChanged(double value)
        {
            Preferences.Default.Set(nameof(MonthlyBugget), value);
        }

    }

    public record MonthlySummaryItem(string Label, decimal Amount);

    public class CategorySummaryItem
    {
        public string Category { get; set; }
        public decimal Amount { get; set; }
    }
}
