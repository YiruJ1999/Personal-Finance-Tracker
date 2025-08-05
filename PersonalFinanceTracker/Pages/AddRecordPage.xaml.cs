namespace PersonalFinanceTracker.Pages;

public partial class AddRecordPage : ContentPage
{
    public AddRecordPage(AddRecordPageModel pageModel)
    {
        InitializeComponent();
        BindingContext = pageModel;
    }
}
