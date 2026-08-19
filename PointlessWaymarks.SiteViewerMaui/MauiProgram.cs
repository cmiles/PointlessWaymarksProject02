using Microsoft.Extensions.Logging;
using PointlessWaymarks.SiteViewerMaui.Storage;
using PointlessWaymarks.SiteViewerMaui.ViewModels;
using PointlessWaymarks.SiteViewerMaui.Views;

namespace PointlessWaymarks.SiteViewerMaui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Storage
        builder.Services.AddSingleton<ISecureCredentialStore, SecureCredentialStore>();
        builder.Services.AddSingleton<ProfileRepository>();

        // View models
        builder.Services.AddTransient<ConnectionsListViewModel>();
        builder.Services.AddTransient<ConnectionEditViewModel>();
        builder.Services.AddTransient<ViewerViewModel>();

        // Pages
        builder.Services.AddTransient<ConnectionsListPage>();
        builder.Services.AddTransient<ConnectionEditPage>();
        builder.Services.AddTransient<ViewerPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
