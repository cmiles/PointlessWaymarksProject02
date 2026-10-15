namespace HistoricTempestsDataDownloader;

public class StationMetadata
{
    public long StationId { get; set; }
    public long DeviceId { get; set; }
    public string StationName { get; set; } = string.Empty;
    public string Timezone { get; set; } = string.Empty;
    public int TimezoneOffsetMinutes { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double ElevationMeters { get; set; }
    public double ElevationFeet => UnitConversions.MetersToFeet(ElevationMeters);
    public long CreatedEpoch { get; set; }
    public string FirstObservationDateLocal { get; set; } = string.Empty;
    public string LastObservationDateLocal { get; set; } = string.Empty;
    public DateTime LastSyncUtc { get; set; } = DateTime.UtcNow;
}
