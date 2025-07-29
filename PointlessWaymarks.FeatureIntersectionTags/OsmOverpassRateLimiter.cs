namespace PointlessWaymarks.FeatureIntersectionTags;

/// <summary>
///     Provides rate limiting for OSM Overpass API calls
/// </summary>
public static class OsmOverpassRateLimiter
{
    private static DateTime _lastApiCallTime = DateTime.MinValue;
    private static readonly TimeSpan DefaultMinimumInterval = TimeSpan.FromMilliseconds(800);
    private static readonly SemaphoreSlim Semaphore = new(1, 1);

    /// <summary>
    ///     Records that an API call has been made
    /// </summary>
    public static void RecordApiCall()
    {
        _lastApiCallTime = DateTime.UtcNow;
    }

    /// <summary>
    ///     Waits if necessary to respect rate limits before making an API call
    /// </summary>
    /// <param name="enforceRateLimit">Whether to enforce the rate limit</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public static async Task WaitForRateLimitAsync(bool enforceRateLimit, CancellationToken cancellationToken)
    {
        if (!enforceRateLimit) return;

        await Semaphore.WaitAsync(cancellationToken);

        try
        {
            var timeSinceLastCall = DateTime.UtcNow - _lastApiCallTime;

            if (timeSinceLastCall < DefaultMinimumInterval)
            {
                var delayTime = DefaultMinimumInterval - timeSinceLastCall;
                await Task.Delay(delayTime, cancellationToken);
            }

            _lastApiCallTime = DateTime.UtcNow;
        }
        finally
        {
            Semaphore.Release();
        }
    }
}