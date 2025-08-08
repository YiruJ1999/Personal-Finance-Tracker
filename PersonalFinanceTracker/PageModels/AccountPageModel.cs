using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using PersonalFinanceTracker.Data;
using System.Linq;


namespace PersonalFinanceTracker.PageModels;

public class ChartPoint
{
    public string Key { get; set; } = string.Empty;
    public double Value { get; set; }
}

public partial class AccountPageModel : ObservableObject
{
    private readonly DatabaseService _dbService;
    private readonly AccountRepository _accountRepository;

    // Observable properties
    [ObservableProperty] private decimal totalAssets;
    [ObservableProperty] private List<ChartPoint> last4MonthsTrend = new();
    [ObservableProperty] private string newAccountName = string.Empty;
    [ObservableProperty] private decimal newAccountOpeningBalance;
    [ObservableProperty] private Account? selectedAccount;
    [ObservableProperty] private decimal editedBalance;

    // Collection for the accounts list
    public ObservableCollection<Account> Accounts { get; } = new();

    public AccountPageModel(DatabaseService dbService, AccountRepository accountRepository)
    {
        _dbService = dbService;
        _accountRepository = accountRepository;
    }

    // Load data for the page: sync accounts from records, list accounts, total assets and trend.

    [RelayCommand]
    public async Task LoadAsync()
    {
        await _dbService.InitAsync();

        // 1) Sync Accounts table from Records, and recompute balances
        await _accountRepository.SyncAccountsFromRecordsAsync();

        // 2) List accounts with balances
        Accounts.Clear();
        var list = await _accountRepository.GetAccountsWithBalancesAsync();
        foreach (var a in list) Accounts.Add(a);

        // 3) Total assets
        TotalAssets = await _accountRepository.GetTotalAssetsAsync();

        // 4) Last 4 months total assets trend
        var trendData = await _accountRepository.GetLast4MonthsTotalAssetsAsync();


        if (trendData == null || trendData.Count == 0)
        {
            trendData = new Dictionary<string, decimal>
            {
                ["2025-05"] = 1000,
                ["2025-06"] = 2000,
                ["2025-07"] = 1800,
                ["2025-08"] = 2300,
            };
        }

        Last4MonthsTrend = trendData
        .OrderBy(kv => kv.Key) 
        .Select(kv => new ChartPoint { Key = kv.Key, Value = (double)kv.Value })
        .ToList();

        System.Diagnostics.Debug.WriteLine($"Trend Count = {Last4MonthsTrend.Count}");
        foreach (var p in Last4MonthsTrend)
            System.Diagnostics.Debug.WriteLine($"{p.Key} -> {p.Value}");

    }


    // Add a new account using bound properties NewAccountName/NewAccountOpeningBalance.

    [RelayCommand]
    private async Task AddAccountAsync()
    {
        if (string.IsNullOrWhiteSpace(NewAccountName))
            return;

        await _accountRepository.AddAccountAsync(NewAccountName, NewAccountOpeningBalance);
        await LoadAsync();

        // Reset input fields
        NewAccountName = string.Empty;
        NewAccountOpeningBalance = 0m;
    }

    // Delete currently selected account.
    [RelayCommand]
    private async Task DeleteAccountAsync()
    {
        if (SelectedAccount is null)
            return;

        await _accountRepository.DeleteAccountAsync(SelectedAccount.Name, alsoDeleteRecords: false);
        await LoadAsync();
    }


    // Update balance for the selected account using EditedBalance.

    [RelayCommand]
    private async Task UpdateBalanceAsync()
    {
        if (SelectedAccount is null)
            return;

        await _accountRepository.UpdateAccountBalanceAsync(SelectedAccount.Name, EditedBalance);
        await LoadAsync();
    }

    // When SelectedAccount changes, prefill EditedBalance so user can update directly
    partial void OnSelectedAccountChanged(Account? value)
    {
        EditedBalance = value?.Balance ?? 0m;
    }
}
