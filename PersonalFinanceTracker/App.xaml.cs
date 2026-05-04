// Personal Finance Tracker
// File: PersonalFinanceTracker/App.xaml.cs
// Purpose: Contains application source code for Personal Finance Tracker.

namespace PersonalFinanceTracker
{
    public partial class App : Application
    {
        public static IServiceProvider Services { get; set; } = default!;
        private readonly DatabaseService _databaseService;
        private readonly PersonalInfoRepository _personalInfoRepository;

        public App(PersonalInfoRepository personalInfoRepository, DatabaseService databaseService)
        {
            InitializeComponent();
            _personalInfoRepository = personalInfoRepository;
            _databaseService = databaseService;
            _ = InitializeLanguageAsync(personalInfoRepository);
        }

        private async Task InitializeLanguageAsync(PersonalInfoRepository repo)
        {
            // Load personal info and set language
            var info = await repo.GetPersonalInfoAsync();
            LanguageManager.SetLanguage(info.LanguageCode);
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }

        protected override async void OnStart()
        {
            base.OnStart();

            try
            {
                // 1) Load the full ISO4217->symbol map from CLDR JSON
                var fullMap = CurrencySymbolLoader.LoadAllSymbols();
                CurrencyManager.SetSymbolMap(fullMap);

                // 2) Read user’s saved currency code from PersonalInfo and apply
                var repo = new PersonalInfoRepository(new DatabaseService());
                await repo.EnsureDatabaseInitializedAsync();
                var info = await repo.GetPersonalInfoAsync();
                var code = string.IsNullOrWhiteSpace(info?.CurrencyCode) ? "EUR" : info!.CurrencyCode;
                CurrencyManager.Set(code);
            }
            catch
            {
                // Fallback to EUR to avoid blocking app start
                CurrencyManager.Set("EUR");
            }
        }
    }
}
