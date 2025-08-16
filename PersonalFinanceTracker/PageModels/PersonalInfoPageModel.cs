using PersonalFinanceTracker.Data;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Services; // for CurrencyManager
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace PersonalFinanceTracker.PageModels
{
    public class PersonalInfoPageModel : INotifyPropertyChanged
    {
        // NOTE: Keep your current repository creation logic.
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
                Avatar = string.IsNullOrEmpty(value) ? null : ImageSource.FromFile(value);
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

        // --- Currency selection ---

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
            set { _currencyCode = value; OnPropertyChanged(); }
        }

        private PersonalInfo? _loadedInfo;

        /// <summary>
        /// Load persisted personal info from repository and apply currency to the whole app.
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
            }
            else
            {
                // If repository returns null, initialize a safe default
                CurrencyCode = "EUR";
            }

            // Apply currency globally so {0:C} / "C" formatting picks up the right symbol
            CurrencyManager.Set(CurrencyCode);
        }

        /// <summary>
        /// Save personal info (including currency) and immediately update global currency symbol.
        /// </summary>
        public async Task SaveAsync()
        {
            if (_loadedInfo == null)
                _loadedInfo = new PersonalInfo();

            _loadedInfo.Name = Name;
            _loadedInfo.AvatarPath = AvatarPath;

            // Persist the selected currency code
            _loadedInfo.CurrencyCode = string.IsNullOrWhiteSpace(CurrencyCode) ? "EUR" : CurrencyCode;

            await _repository.SaveAsync(_loadedInfo);

            // Update app-wide currency symbol instantly after saving
            CurrencyManager.Set(_loadedInfo.CurrencyCode);
        }

        // --- INotifyPropertyChanged boilerplate ---
        public event PropertyChangedEventHandler? PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string name = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
