using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using PersonalFinanceTracker.PageModels;

namespace PersonalFinanceTracker.Pages
{
    // This page is created by Shell, then we pull the VM from DI and bind it.
    // We accept a query parameter "id" (int) and pass it to the VM.
    public partial class AccountDetailPage : ContentPage, IQueryAttributable
    {
        private readonly AccountDetailPageModel _vm;

        public AccountDetailPage()
        {
            InitializeComponent();

            // Resolve the ViewModel from the global ServiceProvider (registered in MauiProgram)
            var sp = App.Services;
            _vm = sp.GetRequiredService<AccountDetailPageModel>();
            BindingContext = _vm;
        }

        /// <summary>
        /// Shell passes query parameters here after page construction.
        /// Expect "?id=123". We parse it and set VM.AccountId.
        /// </summary>
        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            int id = 0;

            if (query.TryGetValue("id", out var raw))
            {
                // Accept either int or string form
                if (raw is int i) id = i;
                else if (raw is string s && int.TryParse(Uri.UnescapeDataString(s), out var parsed))
                    id = parsed;
            }

            if (id <= 0)
            {
                // Non-blocking snackbar; do not throw here
                _ = AppShell.DisplaySnackbarAsync("无效的账户 id。");
                return;
            }

            _ = _vm.InitAsync();
        }
    }
}
