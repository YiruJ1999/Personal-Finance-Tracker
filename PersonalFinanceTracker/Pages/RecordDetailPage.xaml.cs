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