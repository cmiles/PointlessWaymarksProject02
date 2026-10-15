using System;
using System.Text.Json.Serialization;

namespace HistoricTempests.Models;

public class StationMetadata
{
    [JsonPropertyName("stationId")]
    public int StationId { get; set; }

    [JsonPropertyName("deviceId")]
    public int DeviceId { get; set; }

    [JsonPropertyName("stationName")]
    public string StationName { get; set; } = string.Empty;

    [JsonPropertyName("timezone")]
    public string Timezone { get; set; } = string.Empty;

    [JsonPropertyName("latitude")]
    public double Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double Longitude { get; set; }

    [JsonPropertyName("elevationMeters")]
    public double ElevationMeters { get; set; }

    [JsonPropertyName("elevationFeet")]
    public double ElevationFeet { get; set; }
}
