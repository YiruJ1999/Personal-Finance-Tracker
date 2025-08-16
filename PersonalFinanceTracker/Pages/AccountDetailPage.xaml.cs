using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using PersonalFinanceTracker.PageModels;

namespace PersonalFinanceTracker.Pages
{
    public partial class AccountDetailPage : ContentPage, IQueryAttributable
    {
        private readonly AccountDetailPageModel _vm;

        public AccountDetailPage()
        {
            var sp = App.Services;
            _vm = sp.GetRequiredService<AccountDetailPageModel>();
            BindingContext = _vm;

            InitializeComponent();
        }

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            // Expect "?id=123"
            if (query.TryGetValue("id", out var raw))
            {
                if (raw is int i) _vm.AccountId = i;
                else if (raw is string s && int.TryParse(Uri.UnescapeDataString(s), out var id)) _vm.AccountId = id;
            }

            // Optional: if your VM没有在 AccountId 变化时自动 Init（OnAccountIdChanged 内没调用 Init），解开下面这行：
            // _ = _vm.InitAsync();
        }
    }
}
