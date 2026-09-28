using Amazon;

namespace PointlessWaymarks.SiteViewerMaui.S3;

// Self-contained copy of the single helper from PointlessWaymarks.CommonTools.S3Tools
// needed by this app (Amazon region -> service URL). The rest of the desktop S3Tools
// (listing/upload helpers) is intentionally omitted.
public static class S3Tools
{
    public static string AmazonServiceUrlFromBucketRegion(string bucketRegion)
    {
        var endpoint = RegionEndpoint.EnumerableAllRegions.SingleOrDefault(x =>
            x.SystemName == bucketRegion);

        if (endpoint is null) return string.Empty;

        return $"https://s3.{endpoint.SystemName}.{endpoint.PartitionDnsSuffix}";
    }
}
