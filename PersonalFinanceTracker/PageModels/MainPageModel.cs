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
        public async Task Appearing()
        {
            await _databaseService.InitAsync();
            await LoadFinancialData();
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
        }
    }

    public record MonthlySummaryItem(string Label, decimal Amount);
}
