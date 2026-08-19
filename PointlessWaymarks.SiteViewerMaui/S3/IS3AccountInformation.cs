using Amazon.S3;

namespace PointlessWaymarks.SiteViewerMaui.S3;

// Copied (self-contained) from PointlessWaymarks.CommonTools.S3.IS3AccountInformation.
public interface IS3AccountInformation
{
    Func<string> AccessKey { get; }
    Func<string> BucketName { get; }
    Func<string> ServiceUrl { get; init; }
    Func<string> FullFileNameForJsonUploadInformation { get; }
    Func<string> FullFileNameForToExcel { get; }
    Func<S3Providers> S3Provider { get; set; }
    Func<string> Secret { get; }
    AmazonS3Client S3Client();
}
