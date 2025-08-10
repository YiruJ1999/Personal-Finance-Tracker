using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;                 
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Services;
using PersonalFinanceTracker.Data;
using System.Collections.ObjectModel;
using System.Linq;

namespace PersonalFinanceTracker.PageModels;

// NOTE: All comments are in English as requested.
public partial class AccountDetailPageModel : ObservableObject
{
    private readonly AccountRepository _accountRepo;
    private readonly RecordRepository _recordRepo;

    // Title will bind to this
    public string AccountName { get; }

    public ObservableCollection<MonthOption> AvailableMonths { get; } = new();
    public ObservableCollection<Record> MonthlyRecords { get; } = new();

    private MonthOption _selectedMonth;
    public MonthOption SelectedMonth
    {
        get => _selectedMonth;
        set
        {
            if (SetProperty(ref _selectedMonth, value))
            {
                // When month changes, reload records
                _ = LoadMonthlyRecordsAsync();
            }
        }
    }

    private decimal _totalAmount;
    public decimal TotalAmount
    {
        get => _totalAmount;
        set => SetProperty(ref _totalAmount, value);
    }

    public IAsyncRelayCommand EditAccountCommand { get; }

    public AccountDetailPageModel(
        string accountName,
        AccountRepository accountRepo,
        RecordRepository recordRepo)
    {
        // Inject dependencies and initial state
        AccountName = accountName;
        _accountRepo = accountRepo;
        _recordRepo = recordRepo;

        EditAccountCommand = new AsyncRelayCommand(EditAccountAsync);
    }

    public async Task InitAsync()
    {
        // Build month options and select current month by default
        BuildMonthOptions();
        SelectedMonth = AvailableMonths.First(m => m.Year == DateTime.Now.Year && m.Month == DateTime.Now.Month);

        // Load header and current month records
        await LoadHeaderAsync();
        await LoadMonthlyRecordsAsync();
    }

    private void BuildMonthOptions()
    {
        // Create month list for the last 18 months, newest first
        var now = DateTime.Now;
        for (int i = 0; i < 18; i++)
        {
            var dt = new DateTime(now.Year, now.Month, 1).AddMonths(-i);
            AvailableMonths.Add(new MonthOption(dt.Year, dt.Month));
        }
    }

    private async Task LoadHeaderAsync()
    {
        // Get balance for current account
        var accounts = await _accountRepo.GetAccountsWithBalancesAsync();
        var self = accounts.FirstOrDefault(a => a.Name == AccountName);
        TotalAmount = self?.Balance ?? 0m;
    }

    private async Task LoadMonthlyRecordsAsync()
    {
        MonthlyRecords.Clear();

        // Compute time range of the selected month
        var start = new DateTime(SelectedMonth.Year, SelectedMonth.Month, 1);
        var end = start.AddMonths(1).AddSeconds(-1);

        // Query records for this account and month
        var all = await _recordRepo.ListAsync("default"); // TODO: replace with current book if needed
        var monthRecords = all
            .Where(r => r.Account == AccountName && r.Timestamp >= start && r.Timestamp <= end)
            .OrderByDescending(r => r.Timestamp);

        foreach (var r in monthRecords)
            MonthlyRecords.Add(r);
    }

    private async Task EditAccountAsync()
    {
        // Prompt for new balance and update
        string input = await Application.Current.MainPage.DisplayPromptAsync(
            "编辑此账户", "请输入新的余额：", "保存", "取消", keyboard: Keyboard.Numeric);

        if (decimal.TryParse(input, out var newBalance))
        {
            await _accountRepo.UpdateAccountBalanceAsync(AccountName, newBalance);
            await LoadHeaderAsync(); // refresh total
        }
    }
}

public record MonthOption(int Year, int Month)
{
    public string Display => $"{Year}-{Month:00}";
}
