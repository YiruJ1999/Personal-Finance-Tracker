// Personal Finance Tracker
// File: PersonalFinanceTracker/Pages/RecordDetailPage.xaml.cs
// Purpose: Contains code-behind for a MAUI page or popup.

using System.Threading.Tasks;

namespace PersonalFinanceTracker.Pages;

public partial class RecordDetailPage : ContentPage
{
	private readonly RecordDetailPageModel _model;
    public RecordDetailPage(RecordDetailPageModel model)
	{
		InitializeComponent();
        _model = model;
        BindingContext = _model;
    }
    protected override void OnAppearing()
    {
        base.OnAppearing();
    }
}
