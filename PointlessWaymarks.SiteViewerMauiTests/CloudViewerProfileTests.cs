using NUnit.Framework;
using PointlessWaymarks.SiteViewerMaui.Models;
using PointlessWaymarks.SiteViewerMaui.S3;

namespace PointlessWaymarks.SiteViewerMauiTests;

[TestFixture]
public class CloudViewerProfileTests
{
    [Test]
    public void AmazonProfile_DerivesServiceUrlFromRegion_AndIgnoresSuppliedServiceUrl()
    {
        var profile = new CloudViewerProfile
        {
            Name = "Test",
            Provider = nameof(S3Providers.Amazon),
            Region = "us-east-1",
            Bucket = "my-bucket",
            SiteDomain = "example.com"
        };

        var account = profile.S3AccountInformation("access", "secret", "https://should-be-ignored.example");

        Assert.Multiple(() =>
        {
            Assert.That(account.S3Provider(), Is.EqualTo(S3Providers.Amazon));
            Assert.That(account.ServiceUrl(), Is.EqualTo("https://s3.us-east-1.amazonaws.com"));
            Assert.That(account.AccessKey(), Is.EqualTo("access"));
            Assert.That(account.Secret(), Is.EqualTo("secret"));
            Assert.That(account.BucketName(), Is.EqualTo("my-bucket"));
        });
    }

    [Test]
    public void NonAmazonProfile_UsesSuppliedServiceUrl()
    {
        var profile = new CloudViewerProfile
        {
            Name = "Cloudflare Test",
            Provider = nameof(S3Providers.Cloudflare),
            Bucket = "cf-bucket",
            SiteDomain = "example.com"
        };

        var serviceUrl = "https://account.r2.cloudflarestorage.com";
        var account = profile.S3AccountInformation("cf-access", "cf-secret", serviceUrl);

        Assert.Multiple(() =>
        {
            Assert.That(account.S3Provider(), Is.EqualTo(S3Providers.Cloudflare));
            Assert.That(account.ServiceUrl(), Is.EqualTo(serviceUrl));
            Assert.That(account.AccessKey(), Is.EqualTo("cf-access"));
            Assert.That(account.Secret(), Is.EqualTo("cf-secret"));
            Assert.That(account.BucketName(), Is.EqualTo("cf-bucket"));
        });
    }

    [Test]
    public void NonAmazonProfile_NullServiceUrl_ResolvesToEmptyString()
    {
        var profile = new CloudViewerProfile
        {
            Provider = nameof(S3Providers.Wasabi),
            Bucket = "w-bucket"
        };

        var account = profile.S3AccountInformation("a", "s", null);

        Assert.That(account.ServiceUrl(), Is.EqualTo(string.Empty));
    }

    [Test]
    public void AmazonServiceUrlFromBucketRegion_KnownRegion_ReturnsExpectedUrl()
    {
        Assert.That(S3Tools.AmazonServiceUrlFromBucketRegion("us-west-2"),
            Is.EqualTo("https://s3.us-west-2.amazonaws.com"));
    }

    [Test]
    public void AmazonServiceUrlFromBucketRegion_UnknownRegion_ReturnsEmpty()
    {
        Assert.That(S3Tools.AmazonServiceUrlFromBucketRegion("not-a-real-region"), Is.EqualTo(string.Empty));
    }
}
