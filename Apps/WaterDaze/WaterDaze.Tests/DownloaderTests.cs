using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WaterDaze.Models;
using WaterDaze.Services;
using WaterDazeDataDownloader;
using WaterDazeDataDownloader.Services;
using Xunit;

namespace WaterDaze.Tests;

public class DownloaderTests
{
    private class MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handlerFunc) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(handlerFunc(request));
        }
    }

    [Fact]
    public async Task Program_NoArguments_ReturnsExitCode1()
    {
        var exitCode = await Program.Main([]);
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task Program_HelpArgument_ReturnsExitCode0()
    {
        var exitCode = await Program.Main(["--help"]);
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task Downloader_RetriesOn503AndSucceeds()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "WaterDazeTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            var callCount = 0;
            var sampleJson = UsgsService.SerializeDailyFlowRecords(
            [
                new DailyFlowRecord
                {
                    Date = new DateTime(2026, 1, 1),
                    MeanFlow = 15.5,
                    HasData = true,
                    HasFlow = true,
                    SiteCode = "09485000",
                    SiteName = "RINCON CREEK"
                },
                new DailyFlowRecord
                {
                    Date = new DateTime(2026, 1, 2),
                    MeanFlow = 0.0,
                    HasData = true,
                    HasFlow = false,
                    SiteCode = "09485000",
                    SiteName = "RINCON CREEK"
                }
            ]);

            var handler = new MockHttpMessageHandler(req =>
            {
                callCount++;
                if (callCount <= 2)
                {
                    return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable); // 503
                }
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(sampleJson, Encoding.UTF8, "application/json")
                };
            });

            var httpClient = new HttpClient(handler);
            var downloader = new GaugeDataDownloader(httpClient);

            var options = new GaugeDataDownloaderOptions
            {
                OutputDirectory = tempDir,
                EndDate = new DateTime(2026, 1, 2),
                MaxRetries = 3,
                InitialBackoff = TimeSpan.FromMilliseconds(10),
                DelayBetweenRequests = TimeSpan.Zero,
                Logger = _ => { }
            };

            var site = new GaugeSite { SiteCode = "09485000", SiteName = "RINCON CREEK" };
            var result = await downloader.DownloadSingleGaugeAsync(site, options);

            Assert.True(result.Success, $"Download failed with message: {result.ErrorMessage}");
            Assert.Equal(2, result.RetryCount);
            Assert.Equal(2, result.RecordCount);
            Assert.Equal(3, callCount);

            var savedFile = Path.Combine(tempDir, "09485000.json");
            Assert.True(File.Exists(savedFile));

            await using (var stream = File.OpenRead(savedFile))
            {
                var loaded = await UsgsService.DeserializeDailyFlowRecordsAsync(stream);
                Assert.Equal(2, loaded.Count);
                Assert.Equal(15.5, loaded[0].MeanFlow);
            }
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Downloader_FiltersRecordsThroughEndDate()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "WaterDazeTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            var sampleJson = UsgsService.SerializeDailyFlowRecords(
            [
                new DailyFlowRecord { Date = new DateTime(2026, 1, 1), MeanFlow = 10, HasData = true, HasFlow = true, SiteCode = "09485000" },
                new DailyFlowRecord { Date = new DateTime(2026, 1, 2), MeanFlow = 20, HasData = true, HasFlow = true, SiteCode = "09485000" },
                new DailyFlowRecord { Date = new DateTime(2026, 1, 3), MeanFlow = 30, HasData = true, HasFlow = true, SiteCode = "09485000" },
                new DailyFlowRecord { Date = new DateTime(2026, 1, 4), MeanFlow = 40, HasData = true, HasFlow = true, SiteCode = "09485000" }
            ]);

            var handler = new MockHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(sampleJson, Encoding.UTF8, "application/json")
            });

            var httpClient = new HttpClient(handler);
            var downloader = new GaugeDataDownloader(httpClient);

            var options = new GaugeDataDownloaderOptions
            {
                OutputDirectory = tempDir,
                EndDate = new DateTime(2026, 1, 2), // through Jan 2
                MaxRetries = 3,
                InitialBackoff = TimeSpan.FromMilliseconds(10),
                DelayBetweenRequests = TimeSpan.Zero,
                Logger = _ => { }
            };

            var site = new GaugeSite { SiteCode = "09485000", SiteName = "RINCON CREEK" };
            var result = await downloader.DownloadSingleGaugeAsync(site, options);

            Assert.True(result.Success);
            Assert.Equal(2, result.RecordCount);
            Assert.Equal(new DateTime(2026, 1, 2), result.MaxDate);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}
