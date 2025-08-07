using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using PersonalFinanceTracker.Messages;

namespace PersonalFinanceTracker.PageModels;


public partial class AccountPageModel : ObservableObject
{
    private readonly DatabaseService _dbService;
    private readonly AccountRepository _accountRepository;

    public ObservableCollection<Account> Accounts { get; }

    public AccountPageModel(DatabaseService dbService, AccountRepository accountRepository)
    {
        _dbService = dbService;
        _accountRepository = accountRepository;
        Accounts = new ObservableCollection<Account>();
    }
    [RelayCommand]
    public async Task Appearing()
    {
        await _dbService.InitAsync();

    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        await _dbService.InitAsync();

    }

}
