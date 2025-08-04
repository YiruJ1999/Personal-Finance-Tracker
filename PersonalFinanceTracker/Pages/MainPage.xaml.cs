using CommunityToolkit.Maui.Views;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.PageModels;

namespace PersonalFinanceTracker.Pages
{
    public partial class MainPage : ContentPage
    {
        private readonly MainPageModel _model;

        public MainPage(MainPageModel model)
        {
            InitializeComponent();
            _model = model;
            BindingContext = _model;

        }

        protected override async void OnAppearing()
        {
            
            base.OnAppearing();

            if (BindingContext is MainPageModel vm)
            {
                await vm.Appearing(); 
            }
        }

        private async void BuggetTapped(object sender, TappedEventArgs e)
        {
            
            var popup = new BuggetPopup();
            var result = await this.ShowPopupAsync(popup);
            System.Diagnostics.Debug.WriteLine(result);

            if (result is string amountStr && decimal.TryParse(amountStr, out var amount))
            {
                System.Diagnostics.Debug.WriteLine(BindingContext);
                if (BindingContext is MainPageModel model)
                {
                    System.Diagnostics.Debug.WriteLine($"Setting MonthlyBudget to: {amount}");
                    model.MonthlyBugget = (double) amount;
                }
            }
        }

    }
}
