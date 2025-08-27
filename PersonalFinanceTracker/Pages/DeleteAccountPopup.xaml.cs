using System;
using CommunityToolkit.Maui.Views;

namespace PersonalFinanceTracker.Popups
{
    public record DeleteAccountDecision(bool Confirmed, bool AlsoDelete);

    public partial class DeleteAccountPopup : Popup
    {
        private readonly int _accountId;
        private readonly string? _accountName;

        // accountName optional; pass it if you have it for a nicer message
        public DeleteAccountPopup(int accountId, string? accountName = null)
        {
            InitializeComponent();
            _accountId = accountId;
            _accountName = accountName;

            if (!string.IsNullOrWhiteSpace(_accountName))
            {
                // Keep text simple; your localization can override if needed
                MsgLabel.Text = $"确定要删除账户“{_accountName}”（ID: {_accountId}）？";
            }
        }

        private void OnCancelClicked(object sender, EventArgs e)
        {
            // Close with null meaning canceled
            Close(null);
        }

        private void OnDeleteClicked(object sender, EventArgs e)
        {
            var alsoDelete = AlsoDeleteRecordsCheck?.IsChecked ?? false;
            // Close with the decision payload
            Close(new DeleteAccountDecision(true, alsoDelete));
        }
    }
}
