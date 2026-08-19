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
            Assert.That(result!.Name, Is.EqualTo("GUI Site"));
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
    public void EncryptedRoundTrip_DecryptsAndDeserializes()
    {
        const string password = "correct-horse-battery-staple";

        var toWrite = new ConnectionSettingsImport
        {
            Name = "Encrypted Site",
            Bucket = "enc-bucket",
            AccessKey = "enc-key",
            Secret = "enc-secret"
        };

        var encrypted = toWrite.Serialize().Encrypt(password);

        var decrypted = encrypted.Decrypt(password);
        var parsed = ConnectionSettingsImport.TryDeserialize(decrypted, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(result!.Name, Is.EqualTo("Encrypted Site"));
            Assert.That(result.Bucket, Is.EqualTo("enc-bucket"));
            Assert.That(result.AccessKey, Is.EqualTo("enc-key"));
            Assert.That(result.Secret, Is.EqualTo("enc-secret"));
        });
    }

    [Test]
    public void Decrypt_WithWrongPassword_DoesNotProduceValidJson()
    {
        const string password = "the-right-password";
        var encrypted = new ConnectionSettingsImport { Bucket = "b" }.Serialize().Encrypt(password);

        // Decrypting with the wrong password either throws or yields garbage - either way it must not
        // silently produce the original settings. TryDecrypt swallows exceptions to a failure message.
        var decrypted = encrypted.TryDecrypt("the-wrong-password");
        var parsed = ConnectionSettingsImport.TryDeserialize(decrypted, out var result);

        Assert.That(parsed && result?.Bucket == "b", Is.False);
    }
}
