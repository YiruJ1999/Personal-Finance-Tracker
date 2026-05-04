// Personal Finance Tracker
// File: PersonalFinanceTracker/Pages/AccountDetailPage.xaml.cs
// Purpose: Contains code-behind for a MAUI page or popup.

// AccountDetailPage.xaml.cs
using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinanceTracker.PageModels;
using PersonalFinanceTracker.Resources.Strings;

namespace PersonalFinanceTracker.Pages
{
    public partial class AccountDetailPage : ContentPage, IQueryAttributable
    {
        private readonly AccountDetailPageModel _vm;
        public AccountDetailPage() : this(ResolveVm()) { }
        public AccountDetailPage(AccountDetailPageModel vm)
        {
            _vm = vm ?? throw new ArgumentNullException(nameof(vm));
            BindingContext = _vm;
            InitializeComponent();
        }

        private static AccountDetailPageModel ResolveVm()
        {
            var sp = Application.Current?.Handler?.MauiContext?.Services
                     ?? throw new InvalidOperationException("Service provider not ready.");
            return sp.GetRequiredService<AccountDetailPageModel>();
        }

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("id", out var raw))
            {
                if (raw is int i) _vm.AccountId = i;
                else if (raw is string s && int.TryParse(s, out var j)) _vm.AccountId = j;
            }
        }
    }
}

