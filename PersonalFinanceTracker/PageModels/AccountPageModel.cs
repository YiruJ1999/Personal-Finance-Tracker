using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using PersonalFinanceTracker.Messages;
using PersonalFinanceTracker.Data;

namespace PersonalFinanceTracker.PageModels;



public partial class AccountPageModel : ObservableObject
{
    private readonly DatabaseService _dbService;
    private readonly AccountRepository _accountRepository;

    [ObservableProperty] private decimal totalAssets;
    [ObservableProperty] private Dictionary<string, decimal> last4MonthsTrend = new();
    [ObservableProperty] private string newAccountName = string.Empty;
    [ObservableProperty] private decimal newAccountOpeningBalance;
    [ObservableProperty] private Account? selectedAccount;
    [ObservableProperty] private decimal editedBalance;
    public ObservableCollection<Account> Accounts { get; } = new();

    public AccountPageModel(DatabaseService dbService, AccountRepository accountRepository)
    {
        _dbService = dbService;
        _accountRepository = accountRepository;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        await _dbService.InitAsync();

        // 1) sync Accounts table from Records, and recompute balances
        await _accountRepository.SyncAccountsFromRecordsAsync();

        // 2) list accounts with balances
        Accounts.Clear();
        var list = await _accountRepository.GetAccountsWithBalancesAsync();
        foreach (var a in list) Accounts.Add(a);

        // 3) total assets
        totalAssets = await _accountRepository.GetTotalAssetsAsync();

        // 4) last 4 months trend
        last4MonthsTrend = await _accountRepository.GetLast4MonthsTotalAssetsAsync();
    }

    [RelayCommand]
    private async Task AddAccountAsync()
    {
        if (string.IsNullOrWhiteSpace(NewAccountName)) return;
        await _accountRepository.AddAccountAsync(NewAccountName, NewAccountOpeningBalance);
        await LoadAsync();
        NewAccountName = string.Empty;
        NewAccountOpeningBalance = 0m;
    }

    [RelayCommand]
    private async Task DeleteAccountAsync()
    {
        if (SelectedAccount is null) return;
        await _accountRepository.DeleteAccountAsync(SelectedAccount.Name, alsoDeleteRecords: false);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task UpdateBalanceAsync()
    {
        if (SelectedAccount is null) return;
        await _accountRepository.UpdateAccountBalanceAsync(SelectedAccount.Name, EditedBalance, true);
        await LoadAsync();
    }
}
