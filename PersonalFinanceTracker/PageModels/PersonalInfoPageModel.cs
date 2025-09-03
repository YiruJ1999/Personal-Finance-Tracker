using PersonalFinanceTracker.Data;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Services;
using PersonalFinanceTracker.Resources.Strings;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

using System.Linq;
using System.IO;
using PersonalFinanceTracker.Localization; 
using System;                     

namespace PersonalFinanceTracker.PageModels
{
    public class PersonalInfoPageModel : INotifyPropertyChanged
    {
        private readonly PersonalInfoRepository _repository = new PersonalInfoRepository(new Services.DatabaseService());

        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        private string _avatarPath = string.Empty;
        public string AvatarPath
        {
            get => _avatarPath;
            set
            {
                _avatarPath = value;
                // Update ImageSource when file path changes
                RefreshAvatarImage();
                OnPropertyChanged();
            }
        }

        // Make the avatar nullable so it can be cleared when no path is set
        private ImageSource? _avatar;
        public ImageSource? Avatar
        {
            get => _avatar;
            set { _avatar = value; OnPropertyChanged(); }
        }

        private void RefreshAvatarImage()
        {
            try
            {
                // If path is null/empty or file missing => use default avatar
                if (string.IsNullOrWhiteSpace(_avatarPath) || !File.Exists(_avatarPath))
                {
                    // Use app-embedded image as default
                    Avatar = ImageSource.FromFile("default_avatar.png");
                    return;
                }

                // IMPORTANT: Use stream to avoid image caching when the same file path is overwritten
                // Also, set Avatar to null first so the UI will re-render even if the "new" image
                // ends up with the same dimensions/metadata.
                Avatar = null; // force a layout/visual refresh
                Avatar = ImageSource.FromFile(_avatarPath);
            }
            catch
            {
                // On any error, fall back to default avatar
                Avatar = ImageSource.FromFile("default_avatar.png");
            }
        }


        // ---------------- Currency selection ----------------

        // Provide all ISO currency codes from CurrencyManager (sorted)
        public ObservableCollection<string> CurrencyOptions { get; } =
            new(CurrencyManager.GetAllCodes());

        private string _currencyCode = "EUR";
        /// <summary>
        /// ISO 4217 currency code (e.g., "EUR", "USD", "CNY").
        /// Bound to Picker.SelectedItem in the PersonalInfo page.
        /// </summary>
        public string CurrencyCode
        {
            get => _currencyCode;
            set
            {
                if (_currencyCode == value) return;
                _currencyCode = value;
                OnPropertyChanged();

                // Persist and apply the currency
                Preferences.Default.Set("currency_code", _currencyCode);
                CurrencyManager.Set(_currencyCode); // updates CurrencySymbol and raises CurrencyChanged
            }
        }

        // ---------------- Language selection ----------------

        /// <summary>
        /// Simple option record for the language picker.
        /// </summary>
        public record LanguageOption(string Code, string DisplayName);

        /// <summary>
        /// Language list for the picker. Codes must match your .resx suffixes and LanguageManager.
        /// </summary>
        public ObservableCollection<LanguageOption> LanguageOptions { get; } = new()
        {
            new(LanguageManager.ZhHans, "¼òÌåÖÐÎÄ"),
            new(LanguageManager.En,      "English"),
            new(LanguageManager.De,      "Deutsch"),
        };

        private LanguageOption? _selectedLanguage;
        /// <summary>
        /// Two-way bound to the language picker. Applying language also persists to PersonalInfo.
        /// </summary>
        public LanguageOption? SelectedLanguage
        {
            get => _selectedLanguage;
            set
            {
                if (_selectedLanguage == value || value is null) return;
                _selectedLanguage = value;
                OnPropertyChanged();

                // Apply language globally and persist to DB row
                LanguageManager.SetLanguage(value.Code);
                if (_loadedInfo != null)
                {
                    _loadedInfo.LanguageCode = value.Code;   // requires LanguageCode in PersonalInfo model
                    _ = _repository.SaveAsync(_loadedInfo);  // fire-and-forget save
                }

                // Notify localized computed properties to refresh
                OnPropertyChanged(nameof(HelloText));
            }
        }

        /// <summary>
        /// Example localized text with placeholder, used to validate dynamic refresh.
        /// </summary>
        public string HelloText
            => string.Format(AppResources.Hello_User, Name ?? "");

        private PersonalInfo? _loadedInfo;

        /// <summary>
        /// Load persisted personal info from repository and apply currency + language to the whole app.
        /// </summary>
        public async Task LoadAsync()
        {
            // Ensure DB is ready (your repository should create tables and default row if missing).
            await _repository.EnsureDatabaseInitializedAsync();

            _loadedInfo = await _repository.GetPersonalInfoAsync();
            if (_loadedInfo != null)
            {
                Name = _loadedInfo.Name ?? string.Empty;
                AvatarPath = _loadedInfo.AvatarPath ?? string.Empty;

                // Fallback to EUR if no currency has been stored yet
                CurrencyCode = string.IsNullOrWhiteSpace(_loadedInfo.CurrencyCode) ? "EUR" : _loadedInfo.CurrencyCode;

                // ADD: pick language from DB, default zh-Hans
                var lang = string.IsNullOrWhiteSpace(_loadedInfo.LanguageCode) ? LanguageManager.ZhHans : _loadedInfo.LanguageCode;
                LanguageManager.SetLanguage(lang);
                SelectedLanguage = LanguageOptions.FirstOrDefault(x => x.Code == lang) ?? LanguageOptions[0];
            }
            else
            {
                AvatarPath = string.Empty;

                // If repository returns null, initialize a safe default
                CurrencyCode = "EUR";

                // ADD: default language
                LanguageManager.SetLanguage(LanguageManager.ZhHans);
                SelectedLanguage = LanguageOptions[0];
            }

            // Apply currency globally so {0:C} / "C" formatting picks up the right symbol
            CurrencyManager.Set(CurrencyCode);

            // ADD: refresh localized computed properties when language changes
            LanguageManager.LanguageChanged += (_, __) =>
            {
                OnPropertyChanged(nameof(HelloText));
            };
        }

        /// <summary>
        /// Save personal info (including currency & language) and immediately update global currency symbol.
        /// </summary>
        public async Task SaveAsync()
        {
            if (_loadedInfo == null)
                _loadedInfo = new PersonalInfo();

            _loadedInfo.Name = Name;
            _loadedInfo.AvatarPath = AvatarPath;

            // Persist the selected currency code
            _loadedInfo.CurrencyCode = string.IsNullOrWhiteSpace(CurrencyCode) ? "EUR" : CurrencyCode;

            // ADD: persist language code (if user changed in picker)
            _loadedInfo.LanguageCode = SelectedLanguage?.Code ?? LanguageManager.ZhHans;

            await _repository.SaveAsync(_loadedInfo);

            // Update app-wide currency symbol instantly after saving
            CurrencyManager.Set(_loadedInfo.CurrencyCode);

            // Ensure language is applied (no-op if already set through setter)
            LanguageManager.SetLanguage(_loadedInfo.LanguageCode);

            // Update localized properties after save
            OnPropertyChanged(nameof(HelloText));
        }

        // --- INotifyPropertyChanged boilerplate ---
        public event PropertyChangedEventHandler? PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string name = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
