using NUnit.Framework;
using PointlessWaymarks.SiteViewerMaui.Models;
using PointlessWaymarks.SiteViewerMaui.Tools;

namespace PointlessWaymarks.SiteViewerMauiTests;

[TestFixture]
public class ConnectionSettingsImportTests
{
    private const string FullJson = """
    {
        "Name": "My Site",
        "SiteDomain": "example.com",
        "Provider": "Amazon",
        "Region": "us-east-1",
        "ServiceUrl": "https://example.r2.cloudflarestorage.com",
        "Bucket": "my-bucket",
        "AccessKey": "AKIA-EXAMPLE",
        "Secret": "super-secret"
    }
    """;

    [Test]
    public void TryDeserialize_SiteViewerGuiFormat_PopulatesAllValues()
    {
        const string siteViewerGuiJson = """
        {
            "SettingsType": "OpenCloudViewer",
            "CloudViewerSettingsId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            "CloudViewerSettingsName": "GUI Site",
            "CloudViewerSiteDomain": "guisite.com",
            "CloudViewerProvider": "Wasabi",
            "CloudViewerRegion": "us-east-1",
            "CloudViewerBucket": "gui-bucket",
            "CloudViewerAccessKey": "GUI-KEY",
            "CloudViewerSecret": "GUI-SECRET",
            "CloudServiceUrl": "https://s3.wasabisys.com"
        }
        """;

        var parsed = ConnectionSettingsImport.TryDeserialize(siteViewerGuiJson, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.SettingsType, Is.EqualTo("OpenCloudViewer"));
            Assert.That(result.Name, Is.EqualTo("GUI Site"));
            Assert.That(result.SiteDomain, Is.EqualTo("guisite.com"));
            Assert.That(result.Provider, Is.EqualTo("Wasabi"));
            Assert.That(result.Region, Is.EqualTo("us-east-1"));
            Assert.That(result.Bucket, Is.EqualTo("gui-bucket"));
            Assert.That(result.AccessKey, Is.EqualTo("GUI-KEY"));
            Assert.That(result.Secret, Is.EqualTo("GUI-SECRET"));
            Assert.That(result.ServiceUrl, Is.EqualTo("https://s3.wasabisys.com"));
        });
    }

    [Test]
    public void TryDeserialize_FullJson_PopulatesAllValues()
    {
        var parsed = ConnectionSettingsImport.TryDeserialize(FullJson, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Name, Is.EqualTo("My Site"));
            Assert.That(result.SiteDomain, Is.EqualTo("example.com"));
            Assert.That(result.Provider, Is.EqualTo("Amazon"));
            Assert.That(result.Region, Is.EqualTo("us-east-1"));
            Assert.That(result.ServiceUrl, Is.EqualTo("https://example.r2.cloudflarestorage.com"));
            Assert.That(result.Bucket, Is.EqualTo("my-bucket"));
            Assert.That(result.AccessKey, Is.EqualTo("AKIA-EXAMPLE"));
            Assert.That(result.Secret, Is.EqualTo("super-secret"));
        });
    }

    [Test]
    public void TryDeserialize_PartialJson_LeavesMissingValuesNull()
    {
        const string partial = """
        {
            "Bucket": "only-bucket",
            "AccessKey": "only-key"
        }
        """;

        var parsed = ConnectionSettingsImport.TryDeserialize(partial, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Bucket, Is.EqualTo("only-bucket"));
            Assert.That(result.AccessKey, Is.EqualTo("only-key"));
            Assert.That(result.Name, Is.Null);
            Assert.That(result.SiteDomain, Is.Null);
            Assert.That(result.Provider, Is.Null);
            Assert.That(result.Region, Is.Null);
            Assert.That(result.ServiceUrl, Is.Null);
            Assert.That(result.Secret, Is.Null);
        });
    }

    [Test]
    public void TryDeserialize_IsCaseInsensitive()
    {
        const string mixedCase = """{ "bucket": "b", "accesskey": "k" }""";

        var parsed = ConnectionSettingsImport.TryDeserialize(mixedCase, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(result!.Bucket, Is.EqualTo("b"));
            Assert.That(result.AccessKey, Is.EqualTo("k"));
        });
    }

    [Test]
    public void TryDeserialize_InvalidJson_ReturnsFalse()
    {
        var parsed = ConnectionSettingsImport.TryDeserialize("this is not json {", out var result);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.False);
            Assert.That(result, Is.Null);
        });
    }

    [Test]
    public void TryDeserialize_EmptyOrWhitespace_ReturnsFalse()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ConnectionSettingsImport.TryDeserialize("", out _), Is.False);
            Assert.That(ConnectionSettingsImport.TryDeserialize("   ", out _), Is.False);
            Assert.That(ConnectionSettingsImport.TryDeserialize(null, out _), Is.False);
        });
    }

    [Test]
    public void TryDeserialize_IndividuallyEncryptedSecureCloudViewer_DecryptsSuccessfully()
    {
        const string password = "correct-horse-battery-staple";

        var encryptedJson = $$"""
        {
            "SettingsType": "SecureCloudViewer",
            "CloudViewerSettingsName": "{{ "Encrypted Site".Encrypt(password) }}",
            "CloudViewerSiteDomain": "{{ "example.com".Encrypt(password) }}",
            "CloudViewerProvider": "{{ "Amazon".Encrypt(password) }}",
            "CloudViewerRegion": "{{ "us-east-1".Encrypt(password) }}",
            "CloudViewerBucket": "{{ "enc-bucket".Encrypt(password) }}",
            "CloudViewerAccessKey": "{{ "enc-key".Encrypt(password) }}",
            "CloudViewerSecret": "{{ "enc-secret".Encrypt(password) }}",
            "CloudServiceUrl": "{{ "https://s3.example.com".Encrypt(password) }}"
        }
        """;

        var parsed = ConnectionSettingsImport.TryDeserialize(encryptedJson, password, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.SettingsType, Is.EqualTo("SecureCloudViewer"));
            Assert.That(result.Name, Is.EqualTo("Encrypted Site"));
            Assert.That(result.SiteDomain, Is.EqualTo("example.com"));
            Assert.That(result.Provider, Is.EqualTo("Amazon"));
            Assert.That(result.Region, Is.EqualTo("us-east-1"));
            Assert.That(result.Bucket, Is.EqualTo("enc-bucket"));
            Assert.That(result.AccessKey, Is.EqualTo("enc-key"));
            Assert.That(result.Secret, Is.EqualTo("enc-secret"));
            Assert.That(result.ServiceUrl, Is.EqualTo("https://s3.example.com"));
        });
    }

    [Test]
    public void TryDeserialize_IndividuallyEncrypted_WrongPassword_ReturnsFalse()
    {
        const string password = "the-right-password";

        var encryptedJson = $$"""
        {
            "SettingsType": "SecureCloudViewer",
            "CloudViewerBucket": "{{ "enc-bucket".Encrypt(password) }}"
        }
        """;

        var parsed = ConnectionSettingsImport.TryDeserialize(encryptedJson, "the-wrong-password", out var result);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.False);
            Assert.That(result, Is.Null);
        });
    }

    [Test]
    public void TryDeserialize_SecureCloudViewer_WithoutPassword_ReturnsFalse()
    {
        const string password = "the-right-password";

        var encryptedJson = $$"""
        {
            "SettingsType": "SecureCloudViewer",
            "CloudViewerBucket": "{{ "enc-bucket".Encrypt(password) }}"
        }
        """;

        var parsed = ConnectionSettingsImport.TryDeserialize(encryptedJson, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.False);
            Assert.That(result, Is.Null);
        });
    }

    [Test]
    public void TryDeserialize_IndividuallyEncryptedShortNames_DecryptsSuccessfully()
    {
        const string password = "secret-password";

        var encryptedJson = $$"""
        {
            "SettingsType": "SecureCloudViewer",
            "Name": "{{ "My Name".Encrypt(password) }}",
            "Bucket": "{{ "my-bucket".Encrypt(password) }}"
        }
        """;

        var parsed = ConnectionSettingsImport.TryDeserialize(encryptedJson, password, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.SettingsType, Is.EqualTo("SecureCloudViewer"));
            Assert.That(result.Name, Is.EqualTo("My Name"));
            Assert.That(result.Bucket, Is.EqualTo("my-bucket"));
            Assert.That(result.AccessKey, Is.Null);
        });
    }

    [Test]
    public void ReadSettingsType_SecureCloudViewer_ReturnsSecureCloudViewer()
    {
        const string json = """{ "SettingsType": "SecureCloudViewer", "Name": "test" }""";
        Assert.That(ConnectionSettingsImport.ReadSettingsType(json), Is.EqualTo("SecureCloudViewer"));
    }

    [Test]
    public void ReadSettingsType_OpenCloudViewer_ReturnsOpenCloudViewer()
    {
        const string json = """{ "SettingsType": "OpenCloudViewer", "Name": "test" }""";
        Assert.That(ConnectionSettingsImport.ReadSettingsType(json), Is.EqualTo("OpenCloudViewer"));
    }

    [Test]
    public void ReadSettingsType_NoSettingsType_ReturnsNull()
    {
        const string json = """{ "Name": "test", "Bucket": "my-bucket" }""";
        Assert.That(ConnectionSettingsImport.ReadSettingsType(json), Is.Null);
    }

    [Test]
    public void ReadSettingsType_InvalidJson_ReturnsNull()
    {
        Assert.That(ConnectionSettingsImport.ReadSettingsType("invalid json {"), Is.Null);
    }

    [Test]
    public void ReadSettingsType_EmptyOrWhitespace_ReturnsNull()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ConnectionSettingsImport.ReadSettingsType(""), Is.Null);
            Assert.That(ConnectionSettingsImport.ReadSettingsType("   "), Is.Null);
            Assert.That(ConnectionSettingsImport.ReadSettingsType(null), Is.Null);
        });
    }
}
