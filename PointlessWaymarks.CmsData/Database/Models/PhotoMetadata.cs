using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.SpatialTools;

namespace PointlessWaymarks.CmsData.Database.Models;

public class PhotoMetadata
{
    public string? Aperture { get; set; }
    public string? CameraMake { get; set; }
    public string? CameraModel { get; set; }
    public double? Elevation { get; set; }
    public string? FocalLength { get; set; }
    public int? Iso { get; set; }
    public double? Latitude { get; set; }
    public string? Lens { get; set; }
    public string? License { get; set; }
    public double? Longitude { get; set; }
    public string? PhotoCreatedBy { get; set; }
    public DateTime PhotoCreatedOn { get; set; }
    public DateTime? PhotoCreatedOnUtc { get; set; }
    public double? PhotoDirection { get; set; }
    public string? ShutterSpeed { get; set; }
    public string? Summary { get; set; }
    public string? Tags { get; set; }
    public string? Title { get; set; }

    public Point? PointFromLatitudeLongitude()
    {
        if (Longitude is null || Latitude is null) return null;
        return Elevation is null
            ? new Point(Longitude.Value, Latitude.Value)
            : new Point(Longitude.Value, Latitude.Value, Elevation.Value.FeetToMeters());
    }

    public IFeature? FeatureFromPoint()
    {
        if (Longitude is null || Latitude is null) return null;
        return new Feature(PointFromLatitudeLongitude(), new AttributesTable());
    }

    /// <summary>
    ///     Creates a Feature representing a circle with the specified radius in feet around the location's point
    /// </summary>
    /// <param name="radiusInFeet">The radius of the circle in feet</param>
    /// <returns>A Feature containing a circular polygon, or null if the location doesn't have valid coordinates</returns>
    public IFeature? FeatureFromPointAsCircle(double radiusInFeet)
    {
        if (Longitude is null || Latitude is null) return null;

        // Get the point from the location
        var point = PointFromLatitudeLongitude2D();
        if (point == null) return null;

        var circle = PointTools.CreateCircle(point, radiusInFeet);

        // Return the circle as a feature
        return new Feature(circle, new AttributesTable());
    }

    /// <summary>
    ///     Returns a 2D Point
    /// </summary>
    /// <returns></returns>
    public Point? PointFromLatitudeLongitude2D()
    {
        if (Longitude is null || Latitude is null) return null;
        return new Point(Longitude.Value, Latitude.Value);
    }
}