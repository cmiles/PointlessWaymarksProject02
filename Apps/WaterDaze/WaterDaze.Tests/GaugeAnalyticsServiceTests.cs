using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WaterDaze.Models;
using WaterDaze.Services;
using Xunit;

namespace WaterDaze.Tests;

public class GaugeAnalyticsServiceTests
{
    private class MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handlerFunc) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(handlerFunc(request));
        }
    }

    private class FakeUsgsService : IUsgsService
    {
        public bool ThrowOnError { get; set; }
        public string ErrorMessage { get; set; } = "USGS 503 Service Unavailable";
        public List<DailyFlowRecord> RecordsToReturn { get; set; } = [];
        public DateTime? LastRequestedStartDate { get; private set; }

        public Task<List<DailyFlowRecord>> FetchDailyValuesAsync(string siteCode, CancellationToken cancellationToken = default)
        {
            if (ThrowOnError) throw new HttpRequestException(ErrorMessage);
            return Task.FromResult(RecordsToReturn);
        }

        public Task<List<DailyFlowRecord>> FetchDailyValuesAsync(string siteCode, DateTime? startDate, DateTime? endDate, CancellationToken cancellationToken = default)
        {
            LastRequestedStartDate = startDate;
            if (ThrowOnError) throw new HttpRequestException(ErrorMessage);
            return Task.FromResult(RecordsToReturn);
        }

        public Task<List<DailyFlowRecord>> LoadFromLocalFileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(RecordsToReturn);
        }
    }

    [Fact]
    public async Task LoadAndProcessGaugeDataAsync_WhenCachedDataExistsAndUsgsFails_UsesCachedDataAndSetsWarning()
    {
        var testSiteCode = "09489999";
        var cachedRecords = new List<DailyFlowRecord>
        {
            new() { Date = new DateTime(2025, 1, 1), MeanFlow = 10.0, HasData = true, HasFlow = true, SiteCode = testSiteCode },
            new() { Date = new DateTime(2025, 1, 2), MeanFlow = 0.0, HasData = true, HasFlow = false, SiteCode = testSiteCode },
            new() { Date = new DateTime(2025, 1, 3), MeanFlow = 5.0, HasData = true, HasFlow = true, SiteCode = testSiteCode }
        };

        var json = UsgsService.SerializeDailyFlowRecords(cachedRecords);
        var handler = new MockHttpMessageHandler(req =>
        {
            if (req.RequestUri?.ToString().Contains(testSiteCode) == true)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var httpClient = new HttpClient(handler);
        var fakeService = new FakeUsgsService
        {
            ThrowOnError = true,
            ErrorMessage = "503 Server Overload"
        };

        var analyticsService = new GaugeAnalyticsService(httpClient);
        var site = new GaugeSite { SiteCode = testSiteCode, SiteName = "TEST CREEK" };

        var dashboard = await analyticsService.LoadAndProcessGaugeDataAsync(site, fakeService);

        Assert.NotNull(dashboard);
        Assert.True(dashboard.IsCachedData);
        Assert.True(dashboard.HasWarning);
        Assert.Contains("Using cached data through 2025-01-03", dashboard.WarningMessage);
        Assert.Contains("503 Server Overload", dashboard.WarningMessage);
        Assert.Equal(3, dashboard.TotalDaysWithData);
        Assert.Equal(2, dashboard.TotalDaysWithFlow);
        Assert.Equal(new DateTime(2025, 1, 4), fakeService.LastRequestedStartDate);
        Assert.Equal("2025-01-01 to 2025-01-03", dashboard.CachedDateRangeText);
        Assert.Contains("2025-01-04", dashboard.QueriedDateRangeText);
        Assert.Contains("(failed)", dashboard.QueriedDateRangeText);
        Assert.Contains("Cache: 2025-01-01 to 2025-01-03", dashboard.DataSourceSummary);
        Assert.Contains("Queried USGS: 2025-01-04", dashboard.DataSourceSummary);
        Assert.Contains("(failed)", dashboard.DataSourceSummary);
        Assert.DoesNotContain(".json", dashboard.DataSourceSummary);
    }

    [Fact]
    public async Task LoadAndProcessGaugeDataAsync_WhenCachedDataExistsAndUsgsSucceeds_MergesNewRecords()
    {
        var testSiteCode = "09489998";
        var cachedRecords = new List<DailyFlowRecord>
        {
            new() { Date = new DateTime(2025, 1, 1), MeanFlow = 10.0, HasData = true, HasFlow = true, SiteCode = testSiteCode },
            new() { Date = new DateTime(2025, 1, 2), MeanFlow = 0.0, HasData = true, HasFlow = false, SiteCode = testSiteCode }
        };

        var json = UsgsService.SerializeDailyFlowRecords(cachedRecords);
        var handler = new MockHttpMessageHandler(req =>
        {
            if (req.RequestUri?.ToString().Contains(testSiteCode) == true)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var httpClient = new HttpClient(handler);
        var fakeService = new FakeUsgsService
        {
            ThrowOnError = false,
            RecordsToReturn =
            [
                new() { Date = new DateTime(2025, 1, 3), MeanFlow = 25.0, HasData = true, HasFlow = true, SiteCode = testSiteCode }
            ]
        };

        var analyticsService = new GaugeAnalyticsService(httpClient);
        var site = new GaugeSite { SiteCode = testSiteCode, SiteName = "TEST CREEK" };

        var dashboard = await analyticsService.LoadAndProcessGaugeDataAsync(site, fakeService);

        Assert.NotNull(dashboard);
        Assert.True(dashboard.IsCachedData);
        Assert.False(dashboard.HasWarning);
        Assert.Equal(3, dashboard.TotalDaysWithData);
        Assert.Equal(2, dashboard.TotalDaysWithFlow);
        Assert.Equal("2025-01-01 to 2025-01-02", dashboard.CachedDateRangeText);
        Assert.Contains("2025-01-03", dashboard.QueriedDateRangeText);
        Assert.Contains("Cache: 2025-01-01 to 2025-01-02", dashboard.DataSourceSummary);
        Assert.Contains("Queried USGS: 2025-01-03", dashboard.DataSourceSummary);
        Assert.DoesNotContain(".json", dashboard.DataSourceSummary);
    }

    [Fact]
    public async Task LoadAndProcessGaugeDataAsync_WhenNoCachedDataExists_ReportsDirectUsgsQuery()
    {
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var httpClient = new HttpClient(handler);

        var fakeService = new FakeUsgsService
        {
            ThrowOnError = false,
            RecordsToReturn =
            [
                new() { Date = new DateTime(2024, 6, 1), MeanFlow = 15.0, HasData = true, HasFlow = true, SiteCode = "09489997" },
                new() { Date = new DateTime(2024, 6, 2), MeanFlow = 20.0, HasData = true, HasFlow = true, SiteCode = "09489997" }
            ]
        };

        var analyticsService = new GaugeAnalyticsService(httpClient);
        var site = new GaugeSite { SiteCode = "09489997", SiteName = "LIVE STREAM" };

        var dashboard = await analyticsService.LoadAndProcessGaugeDataAsync(site, fakeService);

        Assert.NotNull(dashboard);
        Assert.False(dashboard.IsCachedData);
        Assert.Equal("None", dashboard.CachedDateRangeText);
        Assert.Contains("Cache: None", dashboard.DataSourceSummary);
        Assert.Contains("Queried USGS: All available records", dashboard.DataSourceSummary);
        Assert.Contains("2024-06-01 to 2024-06-02", dashboard.DataSourceSummary);
        Assert.DoesNotContain(".json", dashboard.DataSourceSummary);
    }

    [Fact]
    public async Task LoadAndProcessGaugeDataAsync_WhenCacheIsCurrentThroughToday_ReportsNoQueryNeeded()
    {
        var testSiteCode = "09489996";
        var today = DateTime.Today;
        var cachedRecords = new List<DailyFlowRecord>
        {
            new() { Date = today.AddDays(-5), MeanFlow = 10.0, HasData = true, HasFlow = true, SiteCode = testSiteCode },
            new() { Date = today, MeanFlow = 12.0, HasData = true, HasFlow = true, SiteCode = testSiteCode }
        };

        var json = UsgsService.SerializeDailyFlowRecords(cachedRecords);
        var handler = new MockHttpMessageHandler(req =>
        {
            if (req.RequestUri?.ToString().Contains(testSiteCode) == true)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var httpClient = new HttpClient(handler);
        var fakeService = new FakeUsgsService
        {
            ThrowOnError = false
        };

        var analyticsService = new GaugeAnalyticsService(httpClient);
        var site = new GaugeSite { SiteCode = testSiteCode, SiteName = "CURRENT STREAM" };

        var dashboard = await analyticsService.LoadAndProcessGaugeDataAsync(site, fakeService);

        Assert.NotNull(dashboard);
        Assert.True(dashboard.IsCachedData);
        Assert.Contains($"Cache: {today.AddDays(-5):yyyy-MM-dd} to {today:yyyy-MM-dd}", dashboard.DataSourceSummary);
        Assert.Contains("Queried USGS: None (cache is current)", dashboard.DataSourceSummary);
        Assert.DoesNotContain(".json", dashboard.DataSourceSummary);
    }

    [Fact]
    public async Task MainViewModel_LoadGaugeDataAsync_PopulatesDataSourceSummary()
    {
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var httpClient = new HttpClient(handler);

        var fakeService = new FakeUsgsService
        {
            ThrowOnError = false,
            RecordsToReturn =
            [
                new() { Date = new DateTime(2024, 1, 1), MeanFlow = 5.0, HasData = true, HasFlow = true, SiteCode = "09485000" }
            ]
        };

        var analyticsService = new GaugeAnalyticsService(httpClient);
        var viewModel = new WaterDaze.ViewModels.MainViewModel(fakeService, analyticsService);

        var site = new GaugeSite { SiteCode = "09485000", SiteName = "RINCON CREEK" };
        await viewModel.LoadGaugeDataAsync(site);

        Assert.False(string.IsNullOrWhiteSpace(viewModel.DataSourceSummary));
        Assert.Contains("Queried USGS", viewModel.DataSourceSummary);
        Assert.DoesNotContain(".json", viewModel.DataSourceSummary);
    }

    [Fact]
    public async Task TryLoadCachedGaugeDataAsync_RequestsFromConfiguredNetworkUrl()
    {
        string? requestedUrl = null;
        var handler = new MockHttpMessageHandler(req =>
        {
            requestedUrl = req.RequestUri?.ToString();
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var httpClient = new HttpClient(handler);
        var analyticsService = new GaugeAnalyticsService(httpClient);

        await analyticsService.TryLoadCachedGaugeDataAsync("09485000");

        Assert.Equal("https://software.pointlesswaymarks.com/WaterDaze/UsgsData/09485000.json", requestedUrl);
    }
}
