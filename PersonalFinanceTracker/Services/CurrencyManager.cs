using System.Globalization;
using System.Threading;                 
using Microsoft.Maui.ApplicationModel; 

namespace PersonalFinanceTracker.Services
{
    public static class CurrencyManager
    {
        private static Dictionary<string, string> _symbols = new(StringComparer.OrdinalIgnoreCase)
        {
            ["EUR"] = "€",
            ["USD"] = "$",
            ["GBP"] = "£",
            ["JPY"] = "¥",
            ["CNY"] = "¥",
        };

        public static string CurrentCode { get; private set; } = "EUR";
        public static string CurrentSymbol => _symbols.TryGetValue(CurrentCode, out var s) ? s : CurrentCode;

        public static event EventHandler? CurrencyChanged;

        // Reentrancy guard: 0 = idle, 1 = setting
        private static int _isSetting = 0;

        public static void SetSymbolMap(Dictionary<string, string> map)
        {
            if (map is null || map.Count == 0) return;
            _symbols = map;
        }

        /// <summary>
        /// Set the current ISO currency code and update culture currency symbol safely.
        /// - Avoids deadlocks on Windows by only changing the current thread culture.
        /// - Uses DefaultThreadCurrent* on non-Windows platforms.
        /// - Raises CurrencyChanged on the main thread.
        /// - Guards against reentrancy and no-op updates.
        /// </summary>
        public static void Set(string? isoCode)
        {
            // Normalize input
            var normalized = (isoCode ?? "EUR").ToUpperInvariant();

            // No change -> no work
            if (string.Equals(CurrentCode, normalized, StringComparison.Ordinal))
                return;

            // Reentrancy guard
            if (Interlocked.Exchange(ref _isSetting, 1) == 1)
                return;

            try
            {
                CurrentCode = normalized;

                // Prepare a cloned culture with the desired currency symbol
                var culture = (CultureInfo)CultureInfo.CurrentCulture.Clone();
                culture.NumberFormat.CurrencySymbol = CurrentSymbol;

                if (OperatingSystem.IsWindows())
                {
                    // Windows/WinUI: avoid DefaultThreadCurrent* at app startup; set only the current thread.
                    // This should be called from the UI thread or after the UI is up.
                    CultureInfo.CurrentCulture = culture;
                    CultureInfo.CurrentUICulture = culture;
                }
                else
                {
                    // Mobile platforms: safe to apply as defaults for new threads
                    CultureInfo.DefaultThreadCurrentCulture = culture;
                    CultureInfo.DefaultThreadCurrentUICulture = culture;
                }
            }
            finally
            {
                Interlocked.Exchange(ref _isSetting, 0);
            }

            // Raise change event on the main thread (safe for UI subscribers)
            if (MainThread.IsMainThread)
            {
                CurrencyChanged?.Invoke(null, EventArgs.Empty);
            }
            else
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try { CurrencyChanged?.Invoke(null, EventArgs.Empty); } catch { /* swallow */ }
                });
            }
        }

        public static IReadOnlyList<string> GetAllCodes()
        {
            return _symbols.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
}
