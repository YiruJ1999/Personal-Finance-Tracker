using System;
using Microsoft.Maui.Controls;
using PersonalFinanceTracker.PageModels;
using PersonalFinanceTracker.Resources.Strings;

namespace PersonalFinanceTracker.Pages
{
    public partial class AccountPage : ContentPage
    {
        private readonly AccountPageModel _vm;

        public AccountPage(AccountPageModel vm)
        {
            _vm = vm ?? throw new ArgumentNullException(nameof(vm));
            BindingContext = _vm;
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            try
            {
                await _vm.LoadAsync();
            }
            catch (Exception ex)
            {
                // Log or show a non-blocking toast if needed.
                System.Diagnostics.Debug.WriteLine("[AccountPage] Load failed: " + ex.Message);
            }
        }
    }
}
