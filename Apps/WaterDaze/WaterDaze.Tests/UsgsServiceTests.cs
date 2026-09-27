using System;
using System.Collections.Generic;
using WaterDaze.Models;
using WaterDaze.Services;
using Xunit;

namespace WaterDaze.Tests;

public class UsgsServiceTests
{
    [Fact]
    public void BuildUsgsUrl_WithDateRange_BuildsCorrectUrl()
    {
        var urlWithRange = UsgsService.BuildUsgsUrl("09485000", new DateTime(2026, 9, 1), new DateTime(2026, 9, 25));
        Assert.Equal("https://waterservices.usgs.gov/nwis/dv/?format=json&sites=09485000&startDT=2026-09-01&endDT=2026-09-25&siteStatus=all", urlWithRange);

        var urlWithEndOnly = UsgsService.BuildUsgsUrl("09485000", null, new DateTime(2026, 9, 25));
        Assert.Equal("https://waterservices.usgs.gov/nwis/dv/?format=json&sites=09485000&startDT=1900-01-01&endDT=2026-09-25&siteStatus=all", urlWithEndOnly);
    }

    [Fact]
    public void SerializationAndDeserialization_RoundTripsCorrectly()
    {
        var original = new List<DailyFlowRecord>
        {
            new()
            {
                Date = new DateTime(2026, 5, 10),
                MeanFlow = 12.34,
                HasData = true,
                HasFlow = true,
                Qualifiers = "A",
                SiteCode = "09485000",
                SiteName = "RINCON CREEK"
            },
            new()
            {
                Date = new DateTime(2026, 5, 11),
                MeanFlow = 0.0,
                HasData = true,
                HasFlow = false,
                Qualifiers = "P",
                SiteCode = "09485000",
                SiteName = "RINCON CREEK"
            }
        };

        var json = UsgsService.SerializeDailyFlowRecords(original);
        var deserialized = UsgsService.DeserializeDailyFlowRecords(json);

        Assert.Equal(2, deserialized.Count);
        Assert.Equal(original[0].Date, deserialized[0].Date);
        Assert.Equal(original[0].MeanFlow, deserialized[0].MeanFlow);
        Assert.Equal(original[0].HasData, deserialized[0].HasData);
        Assert.Equal(original[0].HasFlow, deserialized[0].HasFlow);
        Assert.Equal(original[0].Qualifiers, deserialized[0].Qualifiers);
        Assert.Equal(original[0].SiteCode, deserialized[0].SiteCode);
    }

    [Fact]
    public void ParseUsgsResponse_HandlesMissingValuesAndQualifiers()
    {
        var rawResponse = new UsgsDailyValueResponse
        {
            Value = new UsgsValue
            {
                TimeSeries =
                [
                    new UsgsTimeSeries
                    {
                        SourceInfo = new UsgsSourceInfo
                        {
                            SiteCode = [new UsgsSiteCodeItem { Value = "09485000" }],
                            SiteName = "RINCON CREEK"
                        },
                        Values =
                        [
                            new UsgsValuesContainer
                            {
                                Value =
                                [
                                    new UsgsPointValue { DateTime = "2026-01-01T00:00:00.000", Value = "10.5", Qualifiers = ["A"] },
                                    new UsgsPointValue { DateTime = "2026-01-02T00:00:00.000", Value = "-999999", Qualifiers = ["P", "Mnt"] },
                                    new UsgsPointValue { DateTime = "2026-01-03T00:00:00.000", Value = "0.0", Qualifiers = ["A"] }
                                ]
                            }
                        ]
                    }
                ]
            }
        };

        var records = UsgsService.ParseUsgsResponse(rawResponse);

        Assert.Equal(3, records.Count);

        Assert.True(records[0].HasData);
        Assert.True(records[0].HasFlow);
        Assert.Equal(10.5, records[0].MeanFlow);
        Assert.Equal("A", records[0].Qualifiers);

        // Missing value -999999
        Assert.False(records[1].HasData);
        Assert.False(records[1].HasFlow);
        Assert.Null(records[1].MeanFlow);
        Assert.Equal("P,Mnt", records[1].Qualifiers);

        // Zero flow
        Assert.True(records[2].HasData);
        Assert.False(records[2].HasFlow);
        Assert.Equal(0.0, records[2].MeanFlow);
    }
}
