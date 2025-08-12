using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Syncfusion.Maui.Toolkit.Hosting;
using Syncfusion.Maui.Core.Hosting;

namespace PersonalFinanceTracker
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureSyncfusionToolkit()
                .ConfigureSyncfusionCore()
                .ConfigureMauiHandlers(handlers =>
                {
#if IOS || MACCATALYST
                    handlers.AddHandler<Microsoft.Maui.Controls.CollectionView, Microsoft.Maui.Controls.Handlers.Items2.CollectionViewHandler2>();
#endif
                })
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("SegoeUI-Semibold.ttf", "SegoeSemibold");
                    fonts.AddFont("FluentSystemIcons-Regular.ttf", FluentUI.FontFamily);
                });

#if DEBUG
            builder.Logging.AddDebug();
            builder.Services.AddLogging(configure => configure.AddDebug());
#endif

            // Add services
            builder.Services.AddSingleton<DatabaseService>();
            builder.Services.AddSingleton<RecordRepository>();
            builder.Services.AddSingleton<SeedDataService>();
            builder.Services.AddSingleton<ModalErrorHandler>();

            builder.Services.AddSingleton<MainPage>();
            builder.Services.AddSingleton<MainPageModel>();

            builder.Services.AddTransient<AddRecordPage>();
            builder.Services.AddTransient<AddRecordPageModel>();

            builder.Services.AddTransient<ViewRecordPageModel>();
            builder.Services.AddTransient<ViewRecordPage>();

            builder.Services.AddTransient<PersonalInfoPage>();
            builder.Services.AddTransient<PersonalInfoPageModel>();
            builder.Services.AddSingleton<PersonalInfoRepository>();

            builder.Services.AddTransient<AccountPage>();
            builder.Services.AddTransient<AccountPageModel>();
            builder.Services.AddSingleton<AccountRepository>();

            builder.Services.AddTransient<BookPage>();
            builder.Services.AddTransient<BookPageModel>();

            builder.Services.AddTransient<BuggetPopup>();

            builder.Services.AddTransient<PersonalFinanceTracker.Pages.CreateNewBookPopup>();

            var app = builder.Build();
            App.Services = app.Services;
            return app;

        }
    }
}
