using Android.Webkit;

namespace PointlessWaymarks.SiteViewerMaui;

/// <summary>
///     Android <see cref="WebChromeClient" /> that surfaces the top-level page load progress
///     (0-100) so the viewer can drive a non-blocking progress indicator. The callback fires off
///     the UI thread, so consumers must marshal back to the UI thread before touching bindings.
/// </summary>
public class S3WebChromeClient : WebChromeClient
{
    private readonly Action<int> _onProgress;

    public S3WebChromeClient(Action<int> onProgress)
    {
        _onProgress = onProgress;
    }

    public override void OnProgressChanged(Android.Webkit.WebView? view, int newProgress)
    {
        base.OnProgressChanged(view, newProgress);
        _onProgress(newProgress);
    }
}
