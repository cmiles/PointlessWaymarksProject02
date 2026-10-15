using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace HistoricTempests.Models;

public class MonthlyYearlyComparisonsReport
{
    [JsonPropertyName("station")]
    public StationMetadata? Station { get; set; }

    [JsonPropertyName("recordDateRange")]
    public RecordDateRangeInfo? RecordDateRange { get; set; }

    [JsonPropertyName("annualSummaries")]
    public List<AnnualSummaryRecord> AnnualSummaries { get; set; } = [];

    [JsonPropertyName("monthlySummaries")]
    public List<MonthlySummaryRecord> MonthlySummaries { get; set; } = [];

    [JsonPropertyName("monthByMonthMultiYearComparison")]
    public Dictionary<string, MonthMatrixItem> MonthByMonthMultiYearComparison { get; set; } = [];
}

public class RecordDateRangeInfo
{
    [JsonPropertyName("firstDate")]
    public string FirstDate { get; set; } = string.Empty;

    [JsonPropertyName("lastDate")]
    public string LastDate { get; set; } = string.Empty;

    [JsonPropertyName("totalDays")]
    public int TotalDays { get; set; }
}
