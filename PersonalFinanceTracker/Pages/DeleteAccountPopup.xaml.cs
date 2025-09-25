using System;
using CommunityToolkit.Maui.Views;
using PersonalFinanceTracker.Resources.Strings;
using PersonalFinanceTracker.Services;

namespace PersonalFinanceTracker.Popups
{
    public record DeleteAccountDecision(bool Confirmed, bool AlsoDelete);

    public partial class DeleteAccountPopup : Popup
    {
        private readonly int _accountId;
        private readonly string? _accountName;

        private EventHandler? _langHandler;

        // accountName optional; pass it if you have it for a nicer message
        public DeleteAccountPopup(int accountId, string? accountName = null)
        {
            InitializeComponent();
            _accountId = accountId;
            _accountName = accountName;

            if (!string.IsNullOrWhiteSpace(_accountName))
            {
                // Keep text simple; your localization can override if needed
                MsgLabel.Text = string.Format(AppResources.Dialog_DeleteAccountMsg_NoId, _accountName);
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
