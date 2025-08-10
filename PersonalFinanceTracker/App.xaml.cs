namespace PersonalFinanceTracker
{
    public partial class App : Application
    {
        public static IServiceProvider Services { get; set; } = default!;

        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}