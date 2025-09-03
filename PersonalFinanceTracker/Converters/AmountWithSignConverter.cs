using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace PersonalFinanceTracker.Converters
{
    // Convert (Amount, Type) -> localized currency string with sign
    public sealed class AmountWithSignConverter : IMultiValueConverter
    {
        public object Convert(object[] v, Type t, object p, CultureInfo _)
        {
            // Use the app's current culture (kept in sync by your CurrencyManager)
            var culture = CultureInfo.CurrentCulture;

            // Parse amount with the same culture
            decimal amount =
                v[0] is decimal d ? d :
                v[0] is string s && decimal.TryParse(s, NumberStyles.Any, culture, out var dv) ? dv : 0m;

            // Be tolerant to different languages for "expense"
            var type = (v[1]?.ToString() ?? string.Empty);
            var isExpense = type.Equals("Expense", StringComparison.OrdinalIgnoreCase) || type.Equals("支出");

            var abs = Math.Abs(amount);

            // Optional: keep your preferred currency patterns
            var nfi = (NumberFormatInfo)culture.NumberFormat.Clone();
            nfi.CurrencyPositivePattern = 3; // "n $"
            nfi.CurrencyNegativePattern = 3; // "-n $"

            string sign = isExpense ? "-" : "+";

            if (abs == 0m) sign = string.Empty;


            return $"{sign}{abs.ToString("C", nfi)}";
        }

        public object[] ConvertBack(object v, Type[] ts, object p, CultureInfo c) => throw new NotSupportedException();
    }
}
