// Personal Finance Tracker
// File: PersonalFinanceTracker/Platforms/Tizen/Main.cs
// Purpose: Contains platform-specific MAUI startup code.

using System;
using Microsoft.Maui;
using Microsoft.Maui.Hosting;

namespace PersonalFinanceTracker
{
    internal class Program : MauiApplication
    {
        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

        static void Main(string[] args)
        {
            var app = new Program();
            app.Run(args);
        }
    }
}

