using NUnit.Framework;
using PointlessWaymarks.SiteViewerMaui.Models;
using PointlessWaymarks.SiteViewerMaui.S3;

namespace PointlessWaymarks.SiteViewerMauiTests;

[TestFixture]
public class ConnectionValidationTests
{
    private static CloudViewerProfile ValidAmazonProfile() => new()
    {
        Name = "Test",
        Provider = nameof(S3Providers.Amazon),
        Region = "us-east-1",
        Bucket = "bucket",
        SiteDomain = "example.com"
    };

    [Test]
    public void ValidAmazonProfile_WithCredentials_IsValid()
    {
        var result = ConnectionValidation.Validate(ValidAmazonProfile(), "access", "secret", null);
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void MissingName_IsInvalid()
    {
        var profile = ValidAmazonProfile();
        profile.Name = "   ";
        var result = ConnectionValidation.Validate(profile, "access", "secret", null);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Error, Does.Contain("Name"));
        });
    }

    [Test]
    public void MissingSiteDomain_IsInvalid()
    {
        var profile = ValidAmazonProfile();
        profile.SiteDomain = "";
        var result = ConnectionValidation.Validate(profile, "access", "secret", null);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Error, Does.Contain("Domain"));
        });
    }

    [Test]
    public void MissingProvider_IsInvalid()
    {
        var profile = ValidAmazonProfile();
        profile.Provider = "";
        var result = ConnectionValidation.Validate(profile, "access", "secret", null);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Error, Does.Contain("Provider"));
        });
    }

    [Test]
    public void MissingAccessKey_IsInvalid()
    {
        var result = ConnectionValidation.Validate(ValidAmazonProfile(), "", "secret", null);
        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void MissingSecret_IsInvalid()
    {
        var result = ConnectionValidation.Validate(ValidAmazonProfile(), "access", "  ", null);
        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void NonAmazonProvider_WithoutServiceUrl_IsInvalid()
    {
        var profile = new CloudViewerProfile
        {
            Name = "Cf",
            Provider = nameof(S3Providers.Cloudflare),
            Bucket = "bucket",
            SiteDomain = "example.com"
        };

        var result = ConnectionValidation.Validate(profile, "access", "secret", null);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Error, Does.Contain("Service URL"));
        });
    }

    [Test]
    public void NonAmazonProvider_WithServiceUrl_IsValid()
    {
        var profile = new CloudViewerProfile
        {
            Name = "Cf",
            Provider = nameof(S3Providers.Cloudflare),
            Bucket = "bucket",
            SiteDomain = "example.com"
        };

        var result = ConnectionValidation.Validate(profile, "access", "secret",
            "https://account.r2.cloudflarestorage.com");

        Assert.That(result.IsValid, Is.True);
    }
}
