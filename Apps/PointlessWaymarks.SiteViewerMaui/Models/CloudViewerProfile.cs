using PointlessWaymarks.SiteViewerMaui.S3;

namespace PointlessWaymarks.SiteViewerMaui.Models;

/// <summary>
///     Non-secret metadata for a single saved Cloud Viewer connection. This is the mobile,
///     self-contained equivalent of the desktop <c>SecureCloudViewerSettings</c> - but where the
///     desktop version stores metadata in an .ini file and secrets in the Windows Password Vault,
///     here the metadata is persisted as JSON (see <c>ProfileRepository</c>) and the secrets
///     (access key, secret and - for non-Amazon providers - the service URL) live only in
///     <c>SecureStorage</c> keyed by <see cref="Id" /> (see <c>SecureCredentialStore</c>).
/// </summary>
public class CloudViewerProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;

    /// <summary>Name of one of the <see cref="S3Providers" /> values.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Amazon only - the region system name used to derive the service URL.</summary>
    public string Region { get; set; } = string.Empty;

    public string Bucket { get; set; } = string.Empty;

    /// <summary>The site's domain, e.g. <c>example.com</c> (no protocol).</summary>
    public string SiteDomain { get; set; } = string.Empty;

    /// <summary>
    ///     Builds an <see cref="IS3AccountInformation" /> from this profile's fields plus the supplied
    ///     secrets. Mirrors <c>SecureCloudViewerSettings.S3AccountInformation()</c>: for Amazon the
    ///     service URL is derived from the region and the supplied <paramref name="serviceUrl" /> is
    ///     ignored; for other providers the supplied service URL is used.
    /// </summary>
    public IS3AccountInformation S3AccountInformation(string accessKey, string secret, string? serviceUrl)
    {
        Enum.TryParse(Provider, out S3Providers cloudProvider);

        return new S3AccountInformation
        {
            ServiceUrl = cloudProvider == S3Providers.Amazon
                ? () => S3Tools.AmazonServiceUrlFromBucketRegion(Region)
                : () => serviceUrl ?? string.Empty,
            AccessKey = () => accessKey,
            Secret = () => secret,
            BucketName = () => Bucket,
            FullFileNameForJsonUploadInformation = () =>
                Path.Combine(Path.GetTempPath(), $"{DateTime.Now:yyyy-MM-dd--HH-mm-ss}---File-Upload-Data.json"),
            FullFileNameForToExcel = () =>
                Path.Combine(Path.GetTempPath(), $"{DateTime.Now:yyyy-MM-dd--HH-mm-ss}---S3UploadItems.xlsx"),
            S3Provider = () => cloudProvider
        };
    }
}
