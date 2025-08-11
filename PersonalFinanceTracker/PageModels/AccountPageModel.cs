using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using PersonalFinanceTracker.Data;
using System.Linq;
using System;                    
using Microsoft.Maui.Controls;   

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

        // Fallback demo data if repository returns nothing
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
            .OrderBy(kv => kv.Key) // ascending by YYYY-MM
            .Select(kv => new ChartPoint { Key = kv.Key, Value = (double)kv.Value })
            .ToList();

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


    // Open account detail page from a list item tap.
    [RelayCommand]
    private async Task OpenAccountDetailAsync(string accountName)
    {
        // Prefer Shell navigation with query parameter "name"
        if (Shell.Current is not null)
        {
            var route = $"accountDetail?name={Uri.EscapeDataString(accountName)}";
            await Shell.Current.GoToAsync(route);
            return;
        }

        // Fallback if Shell is not used
        await Application.Current.MainPage.DisplayAlert("提示", "请先在应用中注册账户详情页的导航路由。", "好的");
    }

    // Show an "Add Account" popup (name + optional opening balance).
    [RelayCommand]
    private async Task ShowAddAccountPopupAsync()
    {
        // Ask for account name
        var name = await Application.Current.MainPage.DisplayPromptAsync(
            "添加账户", "请输入账户名称：", "保存", "取消", placeholder: "例如：现金/银行卡");

        if (string.IsNullOrWhiteSpace(name))
            return;

        // Ask for opening balance (optional)
        var openingText = await Application.Current.MainPage.DisplayPromptAsync(
            "期初余额", "可选：输入期初余额（留空则为 0）", "确定", "跳过", keyboard: Keyboard.Numeric);

        decimal opening = 0m;
        if (!string.IsNullOrWhiteSpace(openingText) && decimal.TryParse(openingText, out var val))
            opening = val;

        await _accountRepository.AddAccountAsync(name.Trim(), opening);

        // Reload list and totals
        await LoadAsync();
    }

    // Show a delete confirmation popup with an extra option to also delete all records.
    [RelayCommand]
    private async Task ShowDeleteAccountPopupAsync(string accountName)
    {
        // First confirmation
        var confirm = await Application.Current.MainPage.DisplayAlert(
            "删除账户", $"确定要删除账户“{accountName}”？", "删除", "取消");

        if (!confirm) return;

        // Ask whether to also delete records
        var choice = await Application.Current.MainPage.DisplayActionSheet(
            "是否同时删除该账户的所有明细记录？", "取消", null,
            "仅删除账户（保留明细）",
            "删除账户并删除全部明细");

        if (choice is null || choice == "取消") return;

        bool alsoDelete = choice == "删除账户并删除全部明细";
        await _accountRepository.DeleteAccountAsync(accountName, alsoDelete);

        // Reload after deletion
        await LoadAsync();
    }
}
