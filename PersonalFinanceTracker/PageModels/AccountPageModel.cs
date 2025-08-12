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
using Microsoft.Maui.Storage;

namespace PersonalFinanceTracker.PageModels
{
    // DTO for chart points ("YYYY-MM", value)
    public class ChartPoint
    {
        public string Key { get; set; } = string.Empty;
        public double Value { get; set; }
    }

    public partial class AccountPageModel : ObservableObject
    {
        private readonly DatabaseService _dbService;
        private readonly AccountRepository _accountRepository;
        private readonly RecordRepository _recordRepository; 

        // Observable properties for UI
        [ObservableProperty] private decimal totalAssets;
        [ObservableProperty] private List<ChartPoint> last4MonthsTrend = new();
        [ObservableProperty] private string newAccountName = string.Empty;
        [ObservableProperty] private decimal newAccountOpeningBalance;
        [ObservableProperty] private Account? selectedAccount;
        [ObservableProperty] private decimal editedBalance;

        // Collection bound to the accounts list
        public ObservableCollection<Account> Accounts { get; } = new();

        // Preference key used across pages to locate current ledger (book)
        private const string PrefKeyCurrentBook = "current_book";

        public AccountPageModel(
            DatabaseService dbService,
            AccountRepository accountRepository,
            RecordRepository recordRepository) 
        {
            _dbService = dbService ?? throw new ArgumentNullException(nameof(dbService));
            _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
            _recordRepository = recordRepository ?? throw new ArgumentNullException(nameof(recordRepository));
        }

        // Load page data: sync from records, then list accounts, totals and last-4-months trend.
        [RelayCommand]
        public async Task LoadAsync()
        {
            await _dbService.InitAsync();

            // 1) Sync Account table by aggregating all records (records are the source of truth)
            await _accountRepository.SyncAccountsFromRecordsAsync();

            // 2) Load accounts for the list
            Accounts.Clear();
            var list = await _accountRepository.GetAccountsWithBalancesAsync();
            foreach (var a in list) Accounts.Add(a);

            // 3) Total assets (sum of Account table)
            TotalAssets = await _accountRepository.GetTotalAssetsAsync();

            // 4) Last 4 months trend (YYYY-MM -> total)
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

            if (SelectedAccount is not null)
                EditedBalance = SelectedAccount.Balance;
        }

        // Add a new account using bound fields NewAccountName / NewAccountOpeningBalance.
        [RelayCommand]
        private async Task AddAccountAsync()
        {
            if (string.IsNullOrWhiteSpace(NewAccountName))
                return;

            await _accountRepository.AddAccountAsync(NewAccountName.Trim(), NewAccountOpeningBalance);
            await LoadAsync();

            // Reset inputs
            NewAccountName = string.Empty;
            NewAccountOpeningBalance = 0m;
        }

        // Delete the currently selected account (keeps records).
        [RelayCommand]
        private async Task DeleteAccountAsync()
        {
            if (SelectedAccount is null)
                return;

            await _accountRepository.DeleteAccountAsync(SelectedAccount.Name, alsoDeleteRecords: false);
            await LoadAsync();
        }

        // Update balance by writing an adjustment record.
        [RelayCommand]
        private async Task UpdateBalanceAsync()
        {
            if (SelectedAccount is null)
                return;

            await _dbService.InitAsync();

            var current = SelectedAccount.Balance;
            var target = EditedBalance;
            var delta = target - current;

            if (delta == 0m)
            {
                await LoadAsync();
                return;
            }

            var type = delta > 0 ? "收入" : "支出";
            var amount = Math.Abs(delta);
            var book = GetCurrentBook();

            var record = new Record
            {
                Type = type,
                Amount = amount,
                Category = "余额调整",
                Note = "账户详情页手动调整",
                Timestamp = DateTime.Now,
                Account = SelectedAccount.Name // normalization handled in repository
            };

            await _recordRepository.SaveAsync(book, record);
            await LoadAsync();
        }


        // Keep EditedBalance in sync with the selected account.
        partial void OnSelectedAccountChanged(Account? value)
        {
            EditedBalance = value?.Balance ?? 0m;
        }

        // Navigate to account detail page (parameter is account name from item bindings).
        [RelayCommand]
        private async Task OpenAccountDetailAsync(string accountName)
        {
            if (string.IsNullOrWhiteSpace(accountName))
                return;

            var selected = Accounts.FirstOrDefault(a =>
                a.Name.Equals(accountName, StringComparison.OrdinalIgnoreCase));
            SelectedAccount = selected;
            EditedBalance = selected?.Balance ?? 0m;

            if (Shell.Current is not null)
            {
                var route = $"accountDetail?name={Uri.EscapeDataString(accountName)}";
                await Shell.Current.GoToAsync(route);
                return;
            }

            await Application.Current.MainPage.DisplayAlert("提示", "请先在应用中注册账户详情页的导航路由。", "好的");
        }


        // Show Add Account popup (name + optional opening balance).
        [RelayCommand]
        private async Task ShowAddAccountPopupAsync()
        {
            var name = await Application.Current.MainPage.DisplayPromptAsync(
                "添加账户", "请输入账户名称：", "保存", "取消", placeholder: "例如：现金/银行卡");
            if (string.IsNullOrWhiteSpace(name)) return;

            var openingText = await Application.Current.MainPage.DisplayPromptAsync(
                "期初余额", "可选：输入期初余额（留空则为 0）", "确定", "跳过", keyboard: Keyboard.Numeric);

            decimal opening = 0m;
            if (!string.IsNullOrWhiteSpace(openingText) && decimal.TryParse(openingText, out var val))
                opening = val;

            await _accountRepository.AddAccountAsync(name.Trim(), opening);
            await LoadAsync();
        }

        // Show delete confirmation with an option to also delete all related records.
        [RelayCommand]
        private async Task ShowDeleteAccountPopupAsync(string accountName)
        {
            if (string.IsNullOrWhiteSpace(accountName))
                return;

            var confirm = await Application.Current.MainPage.DisplayAlert(
                "删除账户", $"确定要删除账户“{accountName}”？", "删除", "取消");
            if (!confirm) return;

            var choice = await Application.Current.MainPage.DisplayActionSheet(
                "是否同时删除该账户的所有明细记录？", "取消", null,
                "仅删除账户（保留明细）",
                "删除账户并删除全部明细");

            if (choice is null || choice == "取消") return;

            bool alsoDelete = choice == "删除账户并删除全部明细";
            await _accountRepository.DeleteAccountAsync(accountName, alsoDelete);
            await LoadAsync();
        }

        // -----------------------
        // Helpers
        // -----------------------
        private static string GetCurrentBook()
        {
            try
            {
                var name = Preferences.Default.Get(PrefKeyCurrentBook, string.Empty);
                return string.IsNullOrWhiteSpace(name) ? "Default" : name.Trim();
            }
            catch
            {
                var name = Preferences.Get(PrefKeyCurrentBook, string.Empty);
                return string.IsNullOrWhiteSpace(name) ? "Default" : name.Trim();
            }
        }
    }
}
