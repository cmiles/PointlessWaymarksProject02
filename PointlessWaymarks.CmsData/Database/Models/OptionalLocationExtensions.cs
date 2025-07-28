using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.SpatialTools;

namespace PointlessWaymarks.CmsData.Database.Models;

public static class OptionalLocationExtensions
{
    public static IFeature? FeatureFromPoint(this IOptionalLocation content)
    {
        if (content.Longitude is null || content.Latitude is null) return null;
        return new Feature(content.PointFromLatitudeLongitude(), new AttributesTable());
    }

    /// <summary>
    ///     Creates a Feature representing a circle with the specified radius in feet around the location's point
    /// </summary>
    /// <param name="content">The location containing coordinates</param>
    /// <param name="radiusInFeet">The radius of the circle in feet</param>
    /// <returns>A Feature containing a circular polygon, or null if the location doesn't have valid coordinates</returns>
    public static IFeature? FeatureFromPointAsCircle(this IOptionalLocation content, double radiusInFeet)
    {
        if (content.Longitude is null || content.Latitude is null) return null;

        // Get the point from the location
        var point = content.PointFromLatitudeLongitude2D();
        if (point == null) return null;

        var circle = PointTools.CreateCircle(point, radiusInFeet);

        // Return the circle as a feature
        return new Feature(circle, new AttributesTable());
    }

    public static bool HasLocation(this IOptionalLocation content)
    {
        return content.Longitude is not null && content.Latitude is not null;
    }

    public static async Task<bool> HasValidLocation(this IOptionalLocation content)
    {
        if (content.Longitude is null || content.Latitude is null) return false;

        if (!(await CommonContentValidation.LatitudeValidation(content.Latitude.Value)).Valid) return false;
        if (!(await CommonContentValidation.LongitudeValidation(content.Latitude.Value)).Valid) return false;

        return true;
    }

    /// <summary>
    ///     Returns either a Point or a PointZ from the Contents Values
    /// </summary>
    /// <returns></returns>
    public static Point? PointFromLatitudeLongitude(this IOptionalLocation content)
    {
        if (content.Longitude is null || content.Latitude is null) return null;
        return content.Elevation is null
            ? new Point(content.Longitude.Value, content.Latitude.Value)
            : new Point(content.Longitude.Value, content.Latitude.Value, content.Elevation.Value.FeetToMeters());
    }

    /// <summary>
    ///     Returns a 2D Point
    /// </summary>
    /// <returns></returns>
    public static Point? PointFromLatitudeLongitude2D(this IOptionalLocation content)
    {
        if (content.Longitude is null || content.Latitude is null) return null;
        return new Point(content.Longitude.Value, content.Latitude.Value);
    }
}