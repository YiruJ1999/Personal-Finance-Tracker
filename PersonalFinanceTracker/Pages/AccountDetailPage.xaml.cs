using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;                 
using System;
using System.Collections.Generic;
using PersonalFinanceTracker.Services;
using PersonalFinanceTracker.Data;
using PersonalFinanceTracker.PageModels;

namespace PersonalFinanceTracker.Pages;

public partial class AccountDetailPage : ContentPage, IQueryAttributable
{
    // Parameterless ctor so Shell can instantiate the page
    public AccountDetailPage()
    {
        InitializeComponent();
    }

    // Receive query parameters from Shell (e.g., "?name=xxx")
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        var name = query.TryGetValue("name", out var raw)
            ? Uri.UnescapeDataString(raw?.ToString() ?? string.Empty)
            : string.Empty;

        // Resolve services from the global ServiceProvider
        var sp = App.Services;
        var db = sp.GetRequiredService<DatabaseService>();
        var accountRepo = sp.GetRequiredService<AccountRepository>();

        // Prefer resolving; if not registered, fallback to 'new RecordRepository(db)'
        var recordRepo = sp.GetService<RecordRepository>() ?? new RecordRepository(db);

        // Create the ViewModel and bind it
        var vm = new AccountDetailPageModel(name, accountRepo, recordRepo);
        BindingContext = vm;

        // Load the page data
        _ = vm.InitAsync();
    }
}
