// Personal Finance Tracker
// File: PersonalFinanceTracker/Pages/AddAccountPopup.xaml.cs
// Purpose: Contains code-behind for a MAUI page or popup.

using System;
using System.Globalization;
using CommunityToolkit.Maui.Views;

namespace PersonalFinanceTracker.Popups
{
    public record AddAccountResult(string Name, decimal Opening);

    public partial class AddAccountPopup : Popup
    {
        public AddAccountPopup()
        {
            InitializeComponent();
        }

        // Cancel -> return null
        private void OnCancelClicked(object sender, EventArgs e)
        {
            Close(null);
        }

        // Save -> validate and return result
        private void OnSaveClicked(object sender, EventArgs e)
        {
            // Validate name
            var name = NameEntry?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                // Keep popup open and focus the field
                NameEntry?.Focus();
                return;
            }

            decimal opening = 0m;
            var raw = OpeningEntry?.Text;
            if (!string.IsNullOrWhiteSpace(raw) &&
                decimal.TryParse(raw, NumberStyles.Number, CultureInfo.CurrentCulture, out var val))
            {
                opening = val;
            }

            // Close popup and pass result to the caller
            Close(new AddAccountResult(name, opening));
        }
    }
}

