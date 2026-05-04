// Personal Finance Tracker
// File: PersonalFinanceTracker/Platforms/Android/MainActivity.cs
// Purpose: Contains platform-specific MAUI startup code.

using Android.App;
using Android.Content.PM;
using Android.OS;

namespace PersonalFinanceTracker
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
    }
}

