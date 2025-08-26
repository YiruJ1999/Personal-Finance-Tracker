using CommunityToolkit.Maui.Views;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.PageModels;
using PersonalFinanceTracker.Resources.Strings;

namespace PersonalFinanceTracker.Pages
{
    public partial class MainPage : ContentPage
    {
        private readonly MainPageModel _model;

        public MainPage(MainPageModel model)
        {
            _model = model;
            BindingContext = _model;
            InitializeComponent();


        }

        protected override async void OnAppearing()
        {
            
            base.OnAppearing();

            if (BindingContext is MainPageModel vm)
            {
                await vm.Appearing(); 
            }
        }



    }
}
