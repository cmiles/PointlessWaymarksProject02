using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WaterDaze.Models;
using WaterDaze.Services;

namespace WaterDazeDataDownloader.Services;

public class DownloadResult
{
    public bool Success { get; set; }
    public string SiteCode { get; set; } = string.Empty;
    public string SiteName { get; set; } = string.Empty;
    public string OutputFilePath { get; set; } = string.Empty;
    public int RecordCount { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime? MinDate { get; set; }
    public DateTime? MaxDate { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
}

public class DownloadSummary
{
    public int TotalSites { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public int TotalRecords { get; set; }
    public long TotalBytesWritten { get; set; }
    public TimeSpan ElapsedTime { get; set; }
    public List<DownloadResult> Results { get; set; } = [];
}

public class GaugeDataDownloaderOptions
{
    public string OutputDirectory { get; set; } = string.Empty;
    public DateTime EndDate { get; set; } = DateTime.Today.AddDays(-2);
    public int MaxRetries { get; set; } = 5;
    public TimeSpan InitialBackoff { get; set; } = TimeSpan.FromSeconds(2);
    public TimeSpan DelayBetweenRequests { get; set; } = TimeSpan.FromSeconds(1);
    public Action<string>? Logger { get; set; }
}

public class GaugeDataDownloader(HttpClient? httpClient = null)
{
    private readonly HttpClient _httpClient = httpClient ?? new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(60)
    };

    public async Task<DownloadSummary> DownloadAllGaugesAsync(
        IReadOnlyList<GaugeSite> sites,
        GaugeDataDownloaderOptions options,
        CancellationToken cancellationToken = default)
    {
        var log = options.Logger ?? Console.WriteLine;
        var summary = new DownloadSummary
        {
            TotalSites = sites.Count
        };

        var startTime = DateTime.UtcNow;

        if (!Directory.Exists(options.OutputDirectory))
        {
            Directory.CreateDirectory(options.OutputDirectory);
            log($"Created output directory: {options.OutputDirectory}");
        }

        for (var i = 0; i < sites.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var site = sites[i];
            log($"[{i + 1}/{sites.Count}] Fetching {site.SiteCode} - {site.SiteName} through {options.EndDate:yyyy-MM-dd}...");

            var result = await DownloadSingleGaugeAsync(site, options, cancellationToken);
            summary.Results.Add(result);

            if (result.Success)
            {
                summary.SuccessCount++;
                summary.TotalRecords += result.RecordCount;
                summary.TotalBytesWritten += result.FileSizeBytes;

                var sizeKb = result.FileSizeBytes / 1024.0;
                var rangeText = result.MinDate.HasValue && result.MaxDate.HasValue
                    ? $"{result.MinDate:yyyy-MM-dd} to {result.MaxDate:yyyy-MM-dd}"
                    : "N/A";

                log($"  [SUCCESS] Saved {result.RecordCount:N0} records to {Path.GetFileName(result.OutputFilePath)} ({sizeKb:N1} KB) [{rangeText}]");
            }
            else
            {
                summary.FailureCount++;
                log($"  [ERROR] Failed downloading {site.SiteCode}: {result.ErrorMessage}");
            }

            // Polite delay between requests to avoid overloading USGS servers
            if (i < sites.Count - 1 && options.DelayBetweenRequests > TimeSpan.Zero)
            {
                await Task.Delay(options.DelayBetweenRequests, cancellationToken);
            }
        }

        summary.ElapsedTime = DateTime.UtcNow - startTime;
        return summary;
    }

    public async Task<DownloadResult> DownloadSingleGaugeAsync(
        GaugeSite site,
        GaugeDataDownloaderOptions options,
        CancellationToken cancellationToken = default)
    {
        var log = options.Logger ?? Console.WriteLine;
        var result = new DownloadResult
        {
            SiteCode = site.SiteCode,
            SiteName = site.SiteName
        };

        var outputFilePath = Path.Combine(options.OutputDirectory, $"{site.SiteCode}.json");
        result.OutputFilePath = outputFilePath;

        var dir = Path.GetDirectoryName(outputFilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var url = UsgsService.BuildUsgsUrl(site.SiteCode, startDate: null, endDate: options.EndDate);

        var currentBackoff = options.InitialBackoff;
        var attempt = 0;

        while (attempt <= options.MaxRetries)
        {
            attempt++;
            try
            {
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                if (response.StatusCode == HttpStatusCode.ServiceUnavailable || // 503
                    response.StatusCode == HttpStatusCode.TooManyRequests ||     // 429
                    response.StatusCode == HttpStatusCode.InternalServerError || // 500
                    response.StatusCode == HttpStatusCode.BadGateway ||          // 502
                    response.StatusCode == HttpStatusCode.GatewayTimeout)        // 504
                {
                    if (attempt <= options.MaxRetries)
                    {
                        result.RetryCount++;
                        log($"  [WARNING] USGS server returned HTTP {(int)response.StatusCode} ({response.StatusCode}). Retrying in {currentBackoff.TotalSeconds:F1}s (attempt {attempt}/{options.MaxRetries})...");
                        await Task.Delay(currentBackoff, cancellationToken);
                        currentBackoff *= 2;
                        continue;
                    }

                    response.EnsureSuccessStatusCode();
                }

                response.EnsureSuccessStatusCode();

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var records = await UsgsService.DeserializeDailyFlowRecordsAsync(stream, cancellationToken);

                // Filter through end date and remove invalid default dates
                var filtered = records
                    .Where(r => r.Date != default && r.Date <= options.EndDate.Date)
                    .OrderBy(r => r.Date)
                    .ToList();

                // Save to JSON per gauge in the specified folder
                var json = UsgsService.SerializeDailyFlowRecords(filtered, writeIndented: true);
                await File.WriteAllTextAsync(outputFilePath, json, Encoding.UTF8, cancellationToken);

                var fileInfo = new FileInfo(outputFilePath);
                result.Success = true;
                result.RecordCount = filtered.Count;
                result.FileSizeBytes = fileInfo.Length;
                result.MinDate = filtered.Count > 0 ? filtered.Min(r => r.Date) : null;
                result.MaxDate = filtered.Count > 0 ? filtered.Max(r => r.Date) : null;

                return result;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException or IOException)
            {
                if (cancellationToken.IsCancellationRequested) throw;

                if (attempt <= options.MaxRetries)
                {
                    result.RetryCount++;
                    log($"  [WARNING] Transient network error: {ex.Message}. Retrying in {currentBackoff.TotalSeconds:F1}s (attempt {attempt}/{options.MaxRetries})...");
                    await Task.Delay(currentBackoff, cancellationToken);
                    currentBackoff *= 2;
                }
                else
                {
                    result.Success = false;
                    result.ErrorMessage = ex.Message;
                    return result;
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        result.Success = false;
        result.ErrorMessage = "Exceeded maximum retry attempts.";
        return result;
    }
}
