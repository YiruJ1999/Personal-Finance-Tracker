using CommunityToolkit.Maui.Views;

namespace PersonalFinanceTracker.Popups;

public partial class AddAccountPopup : Popup
{
    public AddAccountPopup()
    {
        InitializeComponent();
    }

    // Return tuple (name, openingBalance?) back to caller
    private void OnSaveClicked(object sender, EventArgs e)
    {
        // Validate and close with result
        var name = NameEntry.Text?.Trim();
        decimal? opening = null;

        if (!string.IsNullOrWhiteSpace(OpeningEntry.Text) &&
            decimal.TryParse(OpeningEntry.Text, out var val))
        {
            opening = val;
        }

        // You may validate name non-empty here.
        Close((name, opening));
    }

    private void OnCancelClicked(object sender, EventArgs e)
    {
        Close(null);
    }
}
