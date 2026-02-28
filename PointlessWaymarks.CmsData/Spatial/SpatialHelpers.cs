namespace PointlessWaymarks.CmsData.Spatial;

public static class SpatialHelpers
{
    /// <summary>
    ///     Uses reflection to look for Latitude, Longitude and Elevation properties on an object and rounds them to 6 digits.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="toProcess"></param>
    /// <returns></returns>
    public static T RoundSpatialValues<T>(T toProcess)
    {
        if (toProcess == null) return toProcess;

        var positionPropertyNames = new List<string> { "Latitude", "Longitude" };

        var positionProperties = typeof(T).GetProperties().Where(x =>
            (x.PropertyType == typeof(double) || x.PropertyType == typeof(double?)) && x.GetSetMethod() != null &&
            positionPropertyNames.Any(y => x.Name.EndsWith(y))).ToList();

        foreach (var loopProperty in positionProperties)
        {
            if (loopProperty.GetValue(toProcess) == null) continue;
            var current = (double)loopProperty.GetValue(toProcess)!;
            loopProperty.SetValue(toProcess, Math.Round(current, 6));
        }

        var distancePropertyNames = new List<string> { "Distance" };

        var distanceProperties = typeof(T).GetProperties().Where(x =>
            (x.PropertyType == typeof(double) || x.PropertyType == typeof(double?)) && x.GetSetMethod() != null &&
            distancePropertyNames.Any(y => x.Name.EndsWith(y))).ToList();

        foreach (var loopProperty in distanceProperties)
        {
            if (loopProperty.GetValue(toProcess) == null) continue;
            var current = (double)loopProperty.GetValue(toProcess)!;
            loopProperty.SetValue(toProcess, Math.Round(current, 2));
        }

        var elevationPropertyNames = new List<string> { "Elevation" };

        var elevationProperties = typeof(T).GetProperties().Where(x =>
            (x.PropertyType == typeof(double) || x.PropertyType == typeof(double?)) && x.GetSetMethod() != null &&
            elevationPropertyNames.Any(y => x.Name.EndsWith(y))).ToList();

        foreach (var loopProperty in elevationProperties)
        {
            if (loopProperty.GetValue(toProcess) == null) continue;
            var current = (double)loopProperty.GetValue(toProcess)!;
            loopProperty.SetValue(toProcess, Math.Round(current, 0));
        }

        return toProcess;
    }
}