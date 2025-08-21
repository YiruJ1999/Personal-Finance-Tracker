using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace PersonalFinanceTracker.Converters
{
    // Convert (Amount, Type) -> "+123.45" / "-67.89"
    public sealed class AmountWithSignConverter : IMultiValueConverter
    {
        public object Convert(object[] v, Type t, object p, CultureInfo culture)
        {
            decimal amount = v[0] is decimal d ? d :
                             v[0] is string s && decimal.TryParse(s, NumberStyles.Any, culture, out var dv) ? dv : 0m;

            var isExpense = (v[1]?.ToString() ?? "").Equals("Expense", StringComparison.OrdinalIgnoreCase)
                            || (v[1]?.ToString() ?? "").Equals("支出");
            var signed = isExpense ? -amount : amount;

            var nfi = (NumberFormatInfo)culture.NumberFormat.Clone();
            nfi.CurrencyPositivePattern = 3; // "n $"
            nfi.CurrencyNegativePattern = 8; // "-n $"
            return signed.ToString("C", nfi);
        }

        public object[] ConvertBack(object v, Type[] ts, object p, CultureInfo c) => throw new NotSupportedException();
    }
}
