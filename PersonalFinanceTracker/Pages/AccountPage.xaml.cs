using System;
using Microsoft.Maui.Controls;
using PersonalFinanceTracker.PageModels;

namespace PersonalFinanceTracker.Pages
{
    public partial class AccountPage : ContentPage
    {
        private readonly AccountPageModel _vm;

        public AccountPage(AccountPageModel vm)
        {
            InitializeComponent();

            _vm = vm ?? throw new ArgumentNullException(nameof(vm));
            BindingContext = _vm;
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
