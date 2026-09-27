using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WaterDaze.Models;

public class UsgsDailyValueResponse
{
    [JsonPropertyName("value")] public UsgsValue? Value { get; set; }
}

public class UsgsValue
{
    [JsonPropertyName("timeSeries")] public List<UsgsTimeSeries>? TimeSeries { get; set; }
}

public class UsgsTimeSeries
{
    [JsonPropertyName("sourceInfo")] public UsgsSourceInfo? SourceInfo { get; set; }

    [JsonPropertyName("values")] public List<UsgsValuesContainer>? Values { get; set; }

    [JsonPropertyName("variable")] public UsgsVariable? Variable { get; set; }
}

public class UsgsSourceInfo
{
    [JsonPropertyName("siteCode")] public List<UsgsSiteCodeItem>? SiteCode { get; set; }

    [JsonPropertyName("siteName")] public string? SiteName { get; set; }
}

public class UsgsSiteCodeItem
{
    [JsonPropertyName("value")] public string? Value { get; set; }
}

public class UsgsVariable
{
    [JsonPropertyName("options")] public UsgsOptions? Options { get; set; }

    [JsonPropertyName("variableCode")] public List<UsgsVariableCodeItem>? VariableCode { get; set; }
}

public class UsgsVariableCodeItem
{
    [JsonPropertyName("value")] public string? Value { get; set; }
}

public class UsgsOptions
{
    [JsonPropertyName("option")] public List<UsgsOptionItem>? Option { get; set; }
}

public class UsgsOptionItem
{
    [JsonPropertyName("optionCode")] public string? OptionCode { get; set; }
}

public class UsgsValuesContainer
{
    [JsonPropertyName("value")] public List<UsgsPointValue>? Value { get; set; }
}

public class UsgsPointValue
{
    [JsonPropertyName("dateTime")] public string? DateTime { get; set; }

    [JsonPropertyName("qualifiers")] public List<string>? Qualifiers { get; set; }

    [JsonPropertyName("value")] public string? Value { get; set; }
}