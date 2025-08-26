using CommunityToolkit.Maui.Views;
using PersonalFinanceTracker.Resources.Strings;

namespace PersonalFinanceTracker.Popups;

public partial class DeleteAccountPopup : Popup
{
    private readonly string _accountName;
    public DeleteAccountPopup(string accountName)
    {
        InitializeComponent();
        _accountName = accountName;
        MsgLabel.Text = $"确定要删除账户“{_accountName}”？";
    }

    private void OnDeleteClicked(object sender, EventArgs e)
    {
        var alsoDelete = AlsoDeleteRecordsCheck.IsChecked;
        Close((confirm: true, alsoDelete));
    }

    private void OnCancelClicked(object sender, EventArgs e) => Close((confirm: false, alsoDelete: false));
}
