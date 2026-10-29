using System.Collections.Generic;

namespace PointedHorizons.Models;

public class HorizonProfilePoint
{
    public double Azimuth { get; set; }
    public double AltitudeAngle { get; set; }
    public double DistanceKm { get; set; }
    public double ElevationM { get; set; }
    public double Lat { get; set; }
    public double Lon { get; set; }
    public int DistanceBand { get; set; } // 0: <10km, 1: 10-25km, 2: 25-50km, 3: >50km
}

public class HorizonPeak
{
    public string Name { get; set; } = string.Empty;
    public double ElevationM { get; set; }
    public double ElevationFt => ElevationM * 3.28084;
    public double DistanceKm { get; set; }
    public double DistanceMi => DistanceKm * 0.621371;
    public double Azimuth { get; set; }
    public double AltitudeAngle { get; set; }
    public double ProminenceM { get; set; }
    public double Lat { get; set; }
    public double Lon { get; set; }
}

public class HorizonRidgeLayer
{
    public int LayerIndex { get; set; }
    public string Label { get; set; } = string.Empty;
    public List<HorizonProfilePoint> Points { get; set; } = [];
}

public class HorizonSceneData
{
    public double ObserverLat { get; set; }
    public double ObserverLon { get; set; }
    public double ObserverElevationM { get; set; }
    public double ObserverElevationFt => ObserverElevationM * 3.28084;
    public string ObserverName { get; set; } = string.Empty;

    public double AzimuthCenter { get; set; }
    public double FieldOfView { get; set; } // e.g. 60, 90, 120, 360
    public double MinAltitude { get; set; } = -5.0;
    public double MaxAltitude { get; set; } = 15.0;

    public List<HorizonProfilePoint> ProfilePoints { get; set; } = [];
    public List<HorizonPeak> Peaks { get; set; } = [];
    public List<HorizonRidgeLayer> RidgeLayers { get; set; } = [];
    public List<string> TileKeysUsed { get; set; } = [];
    public double CalculationTimeMs { get; set; }
}
