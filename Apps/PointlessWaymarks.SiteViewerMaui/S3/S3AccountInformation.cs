using Amazon.Runtime;
using Amazon.S3;

namespace PointlessWaymarks.SiteViewerMaui.S3;

// Copied (self-contained) from PointlessWaymarks.CommonTools.S3.S3AccountInformation.
public class S3AccountInformation : IS3AccountInformation
{
    public required Func<string> AccessKey { get; init; }
    public required Func<string> BucketName { get; init; }
    public required Func<string> FullFileNameForJsonUploadInformation { get; init; }
    public required Func<string> FullFileNameForToExcel { get; init; }

    public AmazonS3Client S3Client()
    {
        var accessKey = AccessKey();
        var secret = Secret();

        var possibleServiceUrl = ServiceUrl?.Invoke();
        var credentials = new BasicAWSCredentials(accessKey, secret);

        return new AmazonS3Client(credentials, new AmazonS3Config
        {
            ServiceURL = possibleServiceUrl,
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED
        });
    }

    public required Func<S3Providers> S3Provider { get; set; }
    public required Func<string> Secret { get; init; }
    public required Func<string> ServiceUrl { get; init; }
}
