namespace PersonalFinanceTracker.Pages;
using PersonalFinanceTracker.Resources.Strings;

public partial class AddRecordPage : ContentPage
{
    private readonly AddRecordPageModel pageModel;
    public AddRecordPage(AddRecordPageModel viewModel)
    {
        InitializeComponent();
        pageModel = viewModel;
        BindingContext = pageModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        this.SizeChanged += AddRecordPage_SizeChanged;
        UpdateCategoryColumns();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        this.SizeChanged -= AddRecordPage_SizeChanged;
    }

    private void AddRecordPage_SizeChanged(object? sender, EventArgs e) => UpdateCategoryColumns();

    private void UpdateCategoryColumns()
    {
        // tileWidth and gap tuned for the UI
        const double tileWidth = 88;    // approx tile min width including padding
        const double gap = 8;           // spacing between tiles
        const double horizontalPadding = 40; // page side paddings total (20 + 20)

        double usable = Math.Max(0, this.Width - horizontalPadding + gap);
        int cols = Math.Max(3, (int)Math.Floor(usable / (tileWidth + gap)));

        if (BindingContext is PersonalFinanceTracker.PageModels.AddRecordPageModel vm && vm.CategoryColumns != cols)
            vm.CategoryColumns = cols;
    }


}
