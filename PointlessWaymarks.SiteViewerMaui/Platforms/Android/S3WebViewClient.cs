using System.Text;
using Android.Webkit;
using PointlessWaymarks.SiteViewerMaui.Services;

namespace PointlessWaymarks.SiteViewerMaui;

/// <summary>
///     Android <see cref="WebViewClient" /> that intercepts requests to the in-app virtual host and
///     serves bytes fetched from S3 via <see cref="S3ContentService" /> (no localhost server), and
///     enforces the desktop-style navigation rules via <see cref="ViewerNavigation" />: in-site links
///     stay in the WebView, external links are handed to the system browser.
/// </summary>
public class S3WebViewClient : WebViewClient
{
    private readonly S3ContentService _content;
    private readonly Action<string> _onAddressChanged;
    private readonly Action<bool> _onResourceLoadingChanged;
    private readonly string _siteDomain;
    private readonly string _virtualHost;
    private int _inFlightRequests;

    public S3WebViewClient(S3ContentService content, string siteDomain, string virtualHost,
        Action<string> onAddressChanged, Action<bool> onResourceLoadingChanged)
    {
        _content = content;
        _siteDomain = siteDomain;
        _virtualHost = virtualHost;
        _onAddressChanged = onAddressChanged;
        _onResourceLoadingChanged = onResourceLoadingChanged;
    }

    public override WebResourceResponse? ShouldInterceptRequest(Android.Webkit.WebView? view,
        IWebResourceRequest? request)
    {
        var url = request?.Url;

        // Only intercept requests for our virtual host; anything else is left to the platform (and
        // will be caught / redirected by ShouldOverrideUrlLoading for top-level navigations).
        if (url == null || !string.Equals(url.Host, _virtualHost, StringComparison.OrdinalIgnoreCase))
            return base.ShouldInterceptRequest(view, request);

        var path = string.IsNullOrEmpty(url.Path) ? "/" : url.Path;

        Interlocked.Increment(ref _inFlightRequests);
        _onResourceLoadingChanged(true);

        try
        {
            var content = _content.GetContentAsync(path).GetAwaiter().GetResult();

            if (content == null)
                return TextResponse(404, "Not Found", $"Not found: {path}");

            return new WebResourceResponse(
                content.Value.ContentType,
                "utf-8",
                200,
                "OK",
                new Dictionary<string, string> { { "Access-Control-Allow-Origin", "*" } },
                new MemoryStream(content.Value.Bytes));
        }
        catch (Exception ex)
        {
            return TextResponse(500, "Error", ex.Message);
        }
        finally
        {
            if (Interlocked.Decrement(ref _inFlightRequests) <= 0)
                _onResourceLoadingChanged(false);
        }
    }

    public override bool ShouldOverrideUrlLoading(Android.Webkit.WebView? view, IWebResourceRequest? request)
    {
        return HandleNavigation(view, request?.Url?.ToString());
    }

    public override void OnPageStarted(Android.Webkit.WebView? view, string? url, Android.Graphics.Bitmap? favicon)
    {
        base.OnPageStarted(view, url, favicon);
        UpdateAddress(url);
    }

    private bool HandleNavigation(Android.Webkit.WebView? view, string? uri)
    {
        var decision = ViewerNavigation.Decide(uri, _siteDomain, _virtualHost);

        switch (decision.Kind)
        {
            case ViewerNavigationKind.Block:
                return true; // cancel

            case ViewerNavigationKind.External:
                if (!string.IsNullOrEmpty(decision.Url))
                    _ = Launcher.Default.OpenAsync(decision.Url);
                return true; // cancel - handled externally

            case ViewerNavigationKind.Rewrite:
                if (!string.IsNullOrEmpty(decision.Url)) view?.LoadUrl(decision.Url);
                return true; // cancel - we loaded the rewritten URL

            case ViewerNavigationKind.Allow:
            default:
                UpdateAddress(decision.Url);
                return false; // allow the WebView to load it
        }
    }

    private void UpdateAddress(string? uri)
    {
        if (string.IsNullOrEmpty(uri)) return;

        try
        {
            var parsed = new Uri(uri);
            if (string.Equals(parsed.Host, _virtualHost, StringComparison.OrdinalIgnoreCase))
                _onAddressChanged(parsed.PathAndQuery);
        }
        catch
        {
            // ignore unparsable URLs for the address bar
        }
    }

    private static WebResourceResponse TextResponse(int statusCode, string reason, string message)
    {
        return new WebResourceResponse(
            "text/plain",
            "utf-8",
            statusCode,
            reason,
            new Dictionary<string, string>(),
            new MemoryStream(Encoding.UTF8.GetBytes(message)));
    }
}
