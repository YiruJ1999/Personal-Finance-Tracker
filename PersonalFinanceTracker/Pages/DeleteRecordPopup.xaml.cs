using CommunityToolkit.Maui.Views;
using PersonalFinanceTracker.Resources.Strings;

namespace PersonalFinanceTracker.Pages;

public partial class DeleteRecordPopup : Popup
{
    private readonly int _recordId;

    public DeleteRecordPopup(int recordId, string? category = null, decimal? amount = null)
    {
        InitializeComponent();

        _recordId = recordId;

        if (string.IsNullOrWhiteSpace(category) && amount is null)
        {
            // Keep the localized default text from XAML binding, do nothing
        }
        else
        {
            MsgLabel.Text = $"{AppResources.Dialog_DeleteRecordMsg} {category ?? ""} {amount?.ToString("C") ?? ""}".Trim();
        }
    }

    // User canceled
    private void OnCancelClicked(object? sender, EventArgs e) => Close(false);

    // User confirmed deletion
    private void OnDeleteClicked(object? sender, EventArgs e) => Close(true);
}
