using NUnit.Framework;
using PointlessWaymarks.SiteViewerMaui.Services;

namespace PointlessWaymarks.SiteViewerMauiTests;

[TestFixture]
public class ViewerNavigationTests
{
    private const string Site = "example.com";
    private const string Host = "pw.local";

    [Test]
    public void BlankUri_IsBlocked()
    {
        var decision = ViewerNavigation.Decide("", Site, Host);
        Assert.That(decision.Kind, Is.EqualTo(ViewerNavigationKind.Block));
    }

    [Test]
    public void NonHttpUri_IsBlocked()
    {
        var decision = ViewerNavigation.Decide("ftp://example.com/file", Site, Host);
        Assert.That(decision.Kind, Is.EqualTo(ViewerNavigationKind.Block));
    }

    [Test]
    public void SiteDomainLink_IsRewrittenToVirtualHost()
    {
        var decision = ViewerNavigation.Decide("https://example.com/blog/post", Site, Host);

        Assert.Multiple(() =>
        {
            Assert.That(decision.Kind, Is.EqualTo(ViewerNavigationKind.Rewrite));
            Assert.That(decision.Url, Is.EqualTo("https://pw.local/blog/post"));
        });
    }

    [Test]
    public void VirtualHostUrl_IsAllowed()
    {
        var decision = ViewerNavigation.Decide("https://pw.local/index.html", Site, Host);
        Assert.That(decision.Kind, Is.EqualTo(ViewerNavigationKind.Allow));
    }

    [Test]
    public void ExternalUrl_IsSentToBrowser()
    {
        var decision = ViewerNavigation.Decide("https://www.microsoft.com", Site, Host);

        Assert.Multiple(() =>
        {
            Assert.That(decision.Kind, Is.EqualTo(ViewerNavigationKind.External));
            Assert.That(decision.Url, Is.EqualTo("https://www.microsoft.com"));
        });
    }
}
