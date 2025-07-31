using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.PageModels;

namespace PersonalFinanceTracker.Pages
{
    public partial class MainPage : ContentPage
    {
        public MainPage(MainPageModel model)
        {
            InitializeComponent();
            BindingContext = model;
        }
    }
}