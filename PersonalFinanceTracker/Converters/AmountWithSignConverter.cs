using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace PersonalFinanceTracker.Converters
{
    // Convert (Amount, Type) -> "+123.45" / "-67.89"
    public class AmountWithSignConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // values[0] = Amount (decimal), values[1] = Type (string: "收入"/"支出")
            if (values is null || values.Length < 2) return null;
            if (values[0] is not decimal amount) return null;

            var type = values[1] as string;
            var sign = type == "收入" ? "+" : "-";
            return $"{sign}{amount.ToString("0.##", culture)}";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException(); // one-way only
    }
}
