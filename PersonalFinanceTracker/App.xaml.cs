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
                // NOTE: resolve repository however you currently do it
                await _personalInfoRepository.EnsureDatabaseInitializedAsync();
                //await _personalInfoRepository.TryAddCurrencyCodeColumnAsync();

                var info = await _personalInfoRepository.GetPersonalInfoAsync();
                var code = string.IsNullOrWhiteSpace(info.CurrencyCode) ? "EUR" : info.CurrencyCode;

                // Apply saved currency globally
                CurrencyManager.Set(code);
            }
            catch
            {
                // Swallow errors to avoid blocking app start
                CurrencyManager.Set("EUR");
            }
        }
    }
}