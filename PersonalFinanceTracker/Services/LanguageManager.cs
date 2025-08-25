using System;
using System.Globalization;
using System.Threading;

namespace PersonalFinanceTracker.Services
{
    /// <summary>
    /// Static language switch service that wraps Culture setting and raises change notifications.
    /// </summary>
    public static class LanguageManager
    {
        // Fired when language changed; UI can listen to refresh translations.
        public static event EventHandler? LanguageChanged;

        // ISO language codes we support
        public const string ZhHans = "zh-Hans";
        public const string En = "en";
        public const string De = "de";

        public static string CurrentLanguageCode { get; private set; } = ZhHans;

        /// <summary>
        /// Set UI and data culture for the whole app.
        /// </summary>
        public static void SetLanguage(string languageCode)
        {
            // Normalize / fallback
            if (string.IsNullOrWhiteSpace(languageCode))
                languageCode = ZhHans;

            CurrentLanguageCode = languageCode;

            var culture = CultureInfo.GetCultureInfo(languageCode);

            // Apply culture globally for new threads
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;

            // Also apply to current thread
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;

            // Set AppResources’ culture (generated ResX wrapper)
            AppResources.Culture = culture;

            LanguageChanged?.Invoke(null, EventArgs.Empty);
        }
    }
}
