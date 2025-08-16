// Services/CurrencyManager.cs
using System.Globalization;

namespace PersonalFinanceTracker.Services
{
    public static class CurrencyManager
    {
        // Default to ISO code => symbol map (can be overridden at startup)
        private static Dictionary<string, string> _symbols = new(StringComparer.OrdinalIgnoreCase)
        {
            // keep a few safe fallbacks; will be replaced by CLDR map on boot
            ["EUR"] = "€",
            ["USD"] = "$",
            ["GBP"] = "£",
            ["JPY"] = "¥",
            ["CNY"] = "¥",
        };

        public static string CurrentCode { get; private set; } = "EUR";
        public static string CurrentSymbol => _symbols.TryGetValue(CurrentCode, out var s) ? s : CurrentCode;

        public static event EventHandler? CurrencyChanged;

        /// <summary>Replace the whole symbol map (e.g., with CLDR full dataset).</summary>
        public static void SetSymbolMap(Dictionary<string, string> map)
        {
            if (map is null || map.Count == 0) return;
            _symbols = map;
        }

        /// <summary>Set current currency and update CultureInfo.NumberFormat.CurrencySymbol.</summary>
        public static void Set(string isoCode)
        {
            CurrentCode = isoCode?.ToUpperInvariant() ?? "EUR";

            var culture = (CultureInfo)CultureInfo.CurrentCulture.Clone();
            culture.NumberFormat.CurrencySymbol = CurrentSymbol;

            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;

            CurrencyChanged?.Invoke(null, EventArgs.Empty);
        }
    }
}
