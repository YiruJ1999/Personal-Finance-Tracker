// Services/CurrencyManager.cs
using System.Globalization;

namespace PersonalFinanceTracker.Services
{
    public static class CurrencyManager
    {
        // Map from ISO code to symbol; extend as needed
        private static readonly Dictionary<string, string> Symbols = new()
        {
            ["EUR"] = "€",
            ["USD"] = "$",
            ["GBP"] = "?",
            ["JPY"] = "?",
            ["CNY"] = "?"
        };

        public static string CurrentCode { get; private set; } = "EUR";

        public static string CurrentSymbol =>
            Symbols.TryGetValue(CurrentCode, out var s) ? s : CurrentCode;

        public static event EventHandler? CurrencyChanged;

        /// <summary>
        /// Set current currency code and update global NumberFormat CurrencySymbol.
        /// This keeps language/date formats intact and only changes the money symbol.
        /// </summary>
        public static void Set(string isoCode)
        {
            CurrentCode = isoCode?.ToUpperInvariant() ?? "EUR";

            // clone current culture to keep language/date/time, but change currency symbol
            var culture = (CultureInfo)CultureInfo.CurrentCulture.Clone();
            culture.NumberFormat.CurrencySymbol = CurrentSymbol;

            // apply globally so StringFormat {0:C} reflects the new symbol
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;

            CurrencyChanged?.Invoke(null, EventArgs.Empty);
        }
    }
}
