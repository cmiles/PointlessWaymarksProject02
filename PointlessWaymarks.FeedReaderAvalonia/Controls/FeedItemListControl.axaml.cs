using Avalonia.Controls;
using Microsoft.Web.WebView2.Core;
using PointlessWaymarks.AvaloniaCommon.Utility;
using PointlessWaymarks.AvaloniaToolkit.WebView;

namespace PointlessWaymarks.FeedReaderAvalonia.Controls;

public partial class FeedItemListControl : UserControl
{
    public FeedItemListControl()
    {
        InitializeComponent();

        if (BodyContentWebView != null)
        {
            BodyContentWebView.CoreWebView2InitializationCompleted += BodyContentWebView_OnCoreWebView2InitializationCompleted;
            BodyContentWebView.NavigationStarting += BodyContentWebView_OnNavigationStarting;
        }

        if (RssContentWebView != null)
        {
            RssContentWebView.CoreWebView2InitializationCompleted += RssContentWebView_OnCoreWebView2InitializationCompleted;
        }
    }

    private void BodyContentWebView_OnCoreWebView2InitializationCompleted(object? sender,
        CoreWebView2InitializationCompletedEventArgs e)
    {
        if (BodyContentWebView?.CoreWebView2 == null) return;

        BodyContentWebView.CoreWebView2.BasicAuthenticationRequested += (o, args) =>
        {
            if (DataContext is not FeedItemListContext context) return;

            args.Response.UserName = context.DisplayBasicAuthUsername;
            args.Response.Password = context.DisplayBasicAuthPassword;
        };
    }

    private void BodyContentWebView_OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        var context = DataContext as FeedItemListContext;

        if (string.IsNullOrWhiteSpace(context?.DisplayUrl)) return;

        if (e.IsRedirected || e.Uri.Equals(context.DisplayUrl, StringComparison.OrdinalIgnoreCase)) return;

        e.Cancel = true;

        ProcessHelpers.OpenUrlInExternalBrowser(e.Uri);
    }

    private void RssContentWebView_OnCoreWebView2InitializationCompleted(object? sender,
        CoreWebView2InitializationCompletedEventArgs e)
    {
        if (RssContentWebView?.CoreWebView2 == null) return;

        RssContentWebView.CoreWebView2.BasicAuthenticationRequested += (o, args) =>
        {
            if (DataContext is not FeedItemListContext context) return;

            args.Response.UserName = context.DisplayBasicAuthUsername;
            args.Response.Password = context.DisplayBasicAuthPassword;
        };
    }
} 