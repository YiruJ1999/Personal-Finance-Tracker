using Microsoft.Maui.Controls;
using System;
using System.ComponentModel;

namespace PersonalFinanceTracker.Localization
{
    /// <summary>
    /// XAML markup extension to translate a resource key and auto-refresh when language changes.
    /// Usage in XAML: Text="{loc:Translate Settings_Language}"
    /// </summary>
    [ContentProperty(nameof(Key))]
    public class Translate : IMarkupExtension<BindingBase>
    {
        /// <summary>
        /// Resource key defined in AppResources.*.resx
        /// </summary>
        public string Key { get; set; } = string.Empty;

        public BindingBase ProvideValue(IServiceProvider serviceProvider)
        {
            // Create a binding to a proxy that implements INotifyPropertyChanged.
            // When language changes, the proxy raises PropertyChanged and UI updates.
            return new Binding(nameof(LocalizationProxy.Value), source: new LocalizationProxy(Key));
        }

        object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider)
            => ProvideValue(serviceProvider);

        /// <summary>
        /// Proxy object that exposes a single property "Value" bound by XAML.
        /// It listens to LanguageManager.LanguageChanged and notifies UI.
        /// </summary>
        private sealed class LocalizationProxy : INotifyPropertyChanged
        {
            private readonly string _key;
            public event PropertyChangedEventHandler? PropertyChanged;

            public LocalizationProxy(string key)
            {
                _key = key;
                // Subscribe to language change
                Services.LanguageManager.LanguageChanged += OnLanguageChanged;
            }

            private void OnLanguageChanged(object? sender, EventArgs e)
            {
                // Notify UI that "Value" has changed
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }

            public string Value
            {
                get
                {
                    // Fetch the translation from generated ResX wrapper
                    var val = AppResources.ResourceManager.GetString(_key, AppResources.Culture);
                    return val ?? $"!{_key}!";
                }
            }
        }
    }
}
