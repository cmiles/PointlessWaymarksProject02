using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.Extensions.Logging;
using OpenSilver.MauiHybrid.Runner;

#if ANDROID
using Android.Webkit;
#endif

namespace PointedHorizons.MauiHybrid;

#if ANDROID
internal class GeolocationWebChromeClient : WebChromeClient
{
    public override void OnGeolocationPermissionsShowPrompt(string? origin, GeolocationPermissions.ICallback? callback)
    {
        callback?.Invoke(origin, true, false);
    }
}
#endif

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
            })
            .ConfigureMauiHandlers(conf =>
            {
#if ANDROID
                conf.AddHandler<BlazorWebView, AndroidWebViewHandler>();
                Microsoft.AspNetCore.Components.WebView.Maui.BlazorWebViewHandler.BlazorWebViewMapper.AppendToMapping("GeolocationPermission", (handler, view) =>
                {
                    handler.PlatformView.Settings.SetGeolocationEnabled(true);
                    handler.PlatformView.SetWebChromeClient(new GeolocationWebChromeClient());
                });

                PointedHorizons.Services.BackgroundLocationBridge.ToggleForegroundService = (start) =>
                {
                    try
                    {
                        var context = Android.App.Application.Context;
                        var intent = new Android.Content.Intent(context, typeof(LocationTrackingService));
                        if (start)
                        {
                            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.O)
                            {
                                context.StartForegroundService(intent);
                            }
                            else
                            {
                                context.StartService(intent);
                            }
                        }
                        else
                        {
                            context.StopService(intent);
                        }
                    }
                    catch (Exception ex)
                    {
                        Android.Util.Log.Warn("MauiProgram", $"ToggleForegroundService error: {ex.Message}");
                    }
                };
#endif
            });

        builder.Services.AddScoped<IMauiHybridRunner, MauiHybridRunner>();
        builder.Services.AddMauiBlazorWebView();
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
