// Personal Finance Tracker
// File: PersonalFinanceTracker/Platforms/iOS/AppDelegate.cs
// Purpose: Contains platform-specific MAUI startup code.

using Foundation;

namespace PersonalFinanceTracker
{
    [Register("AppDelegate")]
    public class AppDelegate : MauiUIApplicationDelegate
    {
        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}

