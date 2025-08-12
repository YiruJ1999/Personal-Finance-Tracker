using CommunityToolkit.Maui.Views;

namespace PersonalFinanceTracker.Pages;

public partial class BookPage : ContentPage
{
	public BookPage(BookPageModel vm)
	{
        BindingContext = vm;
		InitializeComponent();
		
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is BookPageModel vm)
        {
            await vm.Appearing();
        }
    }


}