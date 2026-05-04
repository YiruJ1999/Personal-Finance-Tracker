// Personal Finance Tracker
// File: PersonalFinanceTracker/Services/LanguageManager.cs
// Purpose: Provides an application service shared across repositories and page models.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using PersonalFinanceTracker.Resources.Strings;

namespace PersonalFinanceTracker.Services
{
    /// <summary>
    /// Static language switch service: change ONLY the UI culture for resource lookup.
    /// Does NOT touch CurrentCulture so currency/number formats remain controlled elsewhere.
    /// </summary>
    public static class LanguageManager
    {
        // Fired when language changed; UI can listen to refresh translations.
        public static event EventHandler? LanguageChanged;

        // Neutral language codes we accept from the app (Picker, DB, etc.)
        public const string ZhHans = "zh-Hans";
        public const string En = "en";
        public const string De = "de";

        /// <summary>
        /// Mapping from neutral codes to specific UI cultures for resource lookup.
        /// Using specific cultures avoids ambiguous fallbacks.
        /// </summary>
        private static readonly Dictionary<string, string> UiCultureMap = new()
        {
            [ZhHans] = "zh-CN", // Simplified Chinese (China)
            [En] = "en-US", // English (United States)
            [De] = "de-DE", // German (Germany)
        };

        public static string CurrentLanguageCode { get; private set; } = ZhHans;

        /// <summary>
        /// Set ONLY the UI culture for the whole app. Do not change CurrentCulture.
        /// This keeps currency/number/date formatting untouched (e.g., by CurrencyManager).
        /// </summary>
        public static void SetLanguage(string languageCode)
        {
            // Normalize input and set internal state
            if (string.IsNullOrWhiteSpace(languageCode))
                languageCode = ZhHans;

            CurrentLanguageCode = languageCode;

            // Resolve a specific UI culture for resource lookup
            var cultureName = UiCultureMap.TryGetValue(languageCode, out var mapped)
                ? mapped
                : languageCode; // fallback: allow a fully qualified code if provided

            var uiCulture = CultureInfo.GetCultureInfo(cultureName);

            // ✅ Apply ONLY UI culture (resources / UI strings)
            CultureInfo.DefaultThreadCurrentUICulture = uiCulture;
            Thread.CurrentThread.CurrentUICulture = uiCulture;

            // Direct the generated ResX wrapper to use this UI culture
            AppResources.Culture = uiCulture;

            // Notify listeners so XAML bindings (Translate extension) can refresh
            LanguageChanged?.Invoke(null, EventArgs.Empty);
        }
    }
}

