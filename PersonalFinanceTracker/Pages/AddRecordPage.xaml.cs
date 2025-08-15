namespace PersonalFinanceTracker.Pages;

public partial class AddRecordPage : ContentPage
{
    private readonly AddRecordPageModel pageModel;
    public AddRecordPage(AddRecordPageModel viewModel)
    {
        InitializeComponent();
        pageModel = viewModel;
        BindingContext = pageModel;
    }
    

}
