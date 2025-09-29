namespace PersonalFinanceTracker.Pages;

public partial class EditRecordPage : ContentPage
{
    private readonly EditRecordPageModel pageModel;
    public EditRecordPage(EditRecordPageModel viewModel)
    {
        InitializeComponent();
        pageModel = viewModel;
        BindingContext = pageModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        this.SizeChanged += EditRecordPage_SizeChanged;
        UpdateCategoryColumns();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        this.SizeChanged -= EditRecordPage_SizeChanged;
    }

    private void EditRecordPage_SizeChanged(object? sender, EventArgs e) => UpdateCategoryColumns();

    private void UpdateCategoryColumns()
    {
        // tileWidth and gap tuned for the UI
        const double tileWidth = 88;    // approx tile min width including padding
        const double gap = 8;           // spacing between tiles
        const double horizontalPadding = 40; // page side paddings total (20 + 20)

        double usable = Math.Max(0, this.Width - horizontalPadding + gap);
        int cols = Math.Max(3, (int)Math.Floor(usable / (tileWidth + gap)));

        if (BindingContext is PersonalFinanceTracker.PageModels.EditRecordPageModel vm && vm.CategoryColumns != cols)
            vm.CategoryColumns = cols;
    }


}
