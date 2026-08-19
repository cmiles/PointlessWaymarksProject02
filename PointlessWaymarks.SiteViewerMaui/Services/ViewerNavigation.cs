namespace PointlessWaymarks.SiteViewerMaui.Services;

public enum ViewerNavigationKind
{
    /// <summary>Allow the navigation to proceed inside the WebView.</summary>
    Allow,

    /// <summary>Cancel the navigation and load the rewritten (in-site) URL instead.</summary>
    Rewrite,

    /// <summary>Cancel the navigation and open the URL in the system browser.</summary>
    External,

    /// <summary>Cancel the navigation and do nothing (blank/unsupported URL).</summary>
    Block
}

public readonly record struct ViewerNavigationDecision(ViewerNavigationKind Kind, string? Url);

/// <summary>
///     Pure (MAUI-free) reproduction of the desktop <c>SitePreviewControl</c> navigation rules:
///     block blank/non-http URLs; rewrite links that point at the live site domain so they stay on
///     the in-app virtual host; send everything that is not on the virtual host to the system
///     browser; otherwise allow the navigation.
/// </summary>
public static class ViewerNavigation
{
    public static ViewerNavigationDecision Decide(string? uri, string siteDomain, string virtualHost)
    {
        if (string.IsNullOrEmpty(uri))
            return new ViewerNavigationDecision(ViewerNavigationKind.Block, null);

        if (!uri.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return new ViewerNavigationDecision(ViewerNavigationKind.Block, null);

        // The content service rewrites html/css/js so that in-site links point at the virtual host,
        // but this also catches links produced by JavaScript at runtime that still point at the site.
        if (!string.IsNullOrEmpty(siteDomain) &&
            uri.Contains(siteDomain, StringComparison.CurrentCultureIgnoreCase) &&
            !uri.Contains(virtualHost, StringComparison.CurrentCultureIgnoreCase))
        {
            var rewritten = uri.Replace($"//{siteDomain}", $"//{virtualHost}", StringComparison.OrdinalIgnoreCase);
            return new ViewerNavigationDecision(ViewerNavigationKind.Rewrite, rewritten);
        }

        Uri parsed;
        try
        {
            parsed = new Uri(uri);
        }
        catch
        {
            return new ViewerNavigationDecision(ViewerNavigationKind.Block, null);
        }

        if (!string.Equals(parsed.Authority, virtualHost, StringComparison.CurrentCultureIgnoreCase))
            return new ViewerNavigationDecision(ViewerNavigationKind.External, uri);

        return new ViewerNavigationDecision(ViewerNavigationKind.Allow, uri);
    }
}
