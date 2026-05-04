// Personal Finance Tracker
// File: PersonalFinanceTracker/Pages/ViewRecordPage.xaml.cs
// Purpose: Contains code-behind for a MAUI page or popup.

namespace PersonalFinanceTracker.Pages;
using PersonalFinanceTracker.Resources.Strings;

public partial class ViewRecordPage : ContentPage
{
    private readonly ViewRecordPageModel _model;
    public ViewRecordPage(ViewRecordPageModel model)
	{
		InitializeComponent();
        _model = model;
        BindingContext = _model;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is ViewRecordPageModel vm)
        {
            await vm.Appearing();
            
        }
    }
}
