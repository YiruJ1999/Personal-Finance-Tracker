using PersonalFinanceTracker.PageModels;
namespace PersonalFinanceTracker.Pages;

public partial class AccountPage : ContentPage
{
    private readonly AccountPageModel pageModel;

    public AccountPage(AccountPageModel viewModel)
    {
        InitializeComponent();
        pageModel = viewModel;
        BindingContext = pageModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await pageModel.LoadAsync();

    }
}