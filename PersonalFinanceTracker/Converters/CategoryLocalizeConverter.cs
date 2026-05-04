// Personal Finance Tracker
// File: PersonalFinanceTracker/Converters/CategoryLocalizeConverter.cs
// Purpose: Converts bound values into display-ready values for XAML views.

using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using PersonalFinanceTracker.Resources.Strings;

namespace PersonalFinanceTracker.Converters
{
    /// <summary>
    /// Convert raw stored category (in any supported language) into the localized display name.
    /// If unknown, fall back to the original string.
    /// </summary>
    public sealed class CategoryLocalizeConverter : IValueConverter
    {
        // Map raw strings (any language variants) to a resource key
        private static readonly (string[] variants, Func<string> value)[] Map =
        {
            (new[] { "饮食", "Food", "Essen" },             () => AppResources.Category_Food),
            (new[] { "交通", "Transport", "Verkehr" },       () => AppResources.Category_Transport),
            (new[] { "购物", "Shopping", "Einkaufen" },      () => AppResources.Category_Shopping),
            (new[] { "娱乐", "Entertainment", "Unterhaltung"},() => AppResources.Category_Entertainment),
            (new[] { "医疗", "Medical", "Medizin" },         () => AppResources.Category_Medical),
            (new[] { "教育", "Education", "Bildung" },       () => AppResources.Category_Education),
            (new[] { "数码", "Digital", "Digital" },         () => AppResources.Category_Digital),
            (new[] { "工资", "Salary", "Gehalt" },           () => AppResources.Category_Salary),
            (new[] { "奖金", "Bonus", "Bonus" },             () => AppResources.Category_Bonus),
            (new[] { "余额调整", "Balance Adjustment", "Saldoanpassung" }, () => AppResources.Category_BalanceAdjust ?? "余额调整"),
        };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var raw = value as string;
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

            foreach (var (variants, getter) in Map)
            {
                foreach (var v in variants)
                {
                    if (string.Equals(v, raw, StringComparison.OrdinalIgnoreCase))
                        return getter(); // return current-language resource
                }
            }
            // Fallback: raw
            return raw;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value;
    }
}
