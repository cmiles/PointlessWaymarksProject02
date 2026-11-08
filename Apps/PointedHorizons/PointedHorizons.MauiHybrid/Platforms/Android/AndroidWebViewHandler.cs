using System;
using Microsoft.AspNetCore.Components.WebView.Maui;

namespace PointedHorizons.MauiHybrid;

public class AndroidWebViewHandler : BlazorWebViewHandler
{
    protected override void ConnectHandler(global::Android.Webkit.WebView webView)
    {
        webView.Settings.SetSupportMultipleWindows(false);

        // Dynamically match user's Android system font scale (Settings -> Display -> Font size)
        float fontScale = webView.Context?.Resources?.Configuration?.FontScale ?? 1.0f;
        int systemFontPercent = (int)Math.Round(fontScale * 100.0f);
        webView.Settings.TextZoom = Math.Max(100, Math.Min(140, systemFontPercent));

        base.ConnectHandler(webView);
    }
}
