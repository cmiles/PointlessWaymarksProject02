using NUnit.Framework;
using PointlessWaymarks.SiteViewerMaui.Services;

namespace PointlessWaymarks.SiteViewerMauiTests;

[TestFixture]
public class S3ContentServiceTests
{
    [Test]
    public void MapPathToS3Key_Root_MapsToIndexHtml()
    {
        Assert.Multiple(() =>
        {
            Assert.That(S3ContentService.MapPathToS3Key("/"), Is.EqualTo("index.html"));
            Assert.That(S3ContentService.MapPathToS3Key(""), Is.EqualTo("index.html"));
            Assert.That(S3ContentService.MapPathToS3Key(null), Is.EqualTo("index.html"));
        });
    }

    [Test]
    public void MapPathToS3Key_StripsLeadingSlash()
    {
        Assert.That(S3ContentService.MapPathToS3Key("/blog/post/index.html"),
            Is.EqualTo("blog/post/index.html"));
    }

    [Test]
    public void MapPathToS3Key_AppliesPrefix()
    {
        Assert.That(S3ContentService.MapPathToS3Key("/style.css", "site/"),
            Is.EqualTo("site/style.css"));
    }

    [Test]
    public void ProcessTextContent_RewritesDomainToVirtualHost()
    {
        var input = "<a href=\"https://example.com/page\">x</a> <img src=\"//example.com/a.png\"/>";
        var output = S3ContentService.ProcessTextContent(input, "example.com", "pw.local");

        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Contain("//pw.local/page"));
            Assert.That(output, Does.Contain("//pw.local/a.png"));
            Assert.That(output, Does.Not.Contain("example.com"));
        });
    }

    [Test]
    public void ProcessTextContent_EmptyDomain_ReturnsUnchanged()
    {
        var input = "no change here";
        Assert.That(S3ContentService.ProcessTextContent(input, "", "pw.local"), Is.EqualTo(input));
    }

    [Test]
    public void NeedsTextProcessing_TrueForTextTypes()
    {
        Assert.Multiple(() =>
        {
            Assert.That(S3ContentService.NeedsTextProcessing("index.html"), Is.True);
            Assert.That(S3ContentService.NeedsTextProcessing("app.css"), Is.True);
            Assert.That(S3ContentService.NeedsTextProcessing("app.js"), Is.True);
            Assert.That(S3ContentService.NeedsTextProcessing("data.json"), Is.True);
            Assert.That(S3ContentService.NeedsTextProcessing("photo.jpg"), Is.False);
            Assert.That(S3ContentService.NeedsTextProcessing("clip.webm"), Is.False);
        });
    }

    [TestCase("a.html", "text/html")]
    [TestCase("a.css", "text/css")]
    [TestCase("a.js", "application/javascript")]
    [TestCase("a.json", "application/json")]
    [TestCase("a.png", "image/png")]
    [TestCase("a.jpg", "image/jpeg")]
    [TestCase("a.svg", "image/svg+xml")]
    [TestCase("a.flac", "audio/flac")]
    [TestCase("a.gpx", "application/gpx+xml")]
    [TestCase("a.webm", "video/webm")]
    [TestCase("a.ogg", "video/ogg")]
    [TestCase("a.unknownext", "application/octet-stream")]
    public void ResolveContentType_MapsExtensions(string path, string expected)
    {
        Assert.That(S3ContentService.ResolveContentType(path), Is.EqualTo(expected));
    }
}
