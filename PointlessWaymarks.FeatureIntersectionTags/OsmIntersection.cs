using System.Text;
using System.Text.Json;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.FeatureIntersectionTags.Models;
using PointlessWaymarks.FeatureIntersectionTags.OsmOverpass;
using PointlessWaymarks.SpatialTools;
using Serilog;

namespace PointlessWaymarks.FeatureIntersectionTags;

public static class OsmIntersection
{
    // Returns true if the OSM element should be filtered out according to tagFilters
    public static bool IsOsmElementFiltered(OsmElement osmElement, List<string> tagFilters)
    {
        static string TrimOuterQuotes(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;

            // Check for double quotes
            if (value.StartsWith("\"") && value.EndsWith("\"") && value.Length >= 2)
                return value.Substring(1, value.Length - 2);

            // Check for single quotes
            if (value.StartsWith("'") && value.EndsWith("'") && value.Length >= 2)
                return value.Substring(1, value.Length - 2);

            return value;
        }

        foreach (var loopFilter in tagFilters)
        {
            if (string.IsNullOrWhiteSpace(loopFilter)) continue;

            var trimmedFilter = loopFilter.Trim();

            if (!trimmedFilter.Contains(':'))
            {
                var filter = TrimOuterQuotes(trimmedFilter.Trim());

                if (osmElement.Tags.Keys.Any(x => x.Equals(filter, StringComparison.InvariantCultureIgnoreCase)))
                    return true;

                continue;
            }

            if (trimmedFilter.EndsWith(':'))
            {
                var filter = TrimOuterQuotes(trimmedFilter.TrimEnd(':').Trim());

                if (osmElement.Tags.Keys.Any(x => x.Equals(filter, StringComparison.InvariantCultureIgnoreCase)))
                    return true;

                continue;
            }

            if (trimmedFilter.StartsWith(':'))
            {
                var tagValue = TrimOuterQuotes(trimmedFilter[1..].Trim());
                if (osmElement.Tags.Values.Any(v => v.Equals(tagValue, StringComparison.InvariantCultureIgnoreCase)))
                    return true;

                continue;
            }

            var parts = trimmedFilter.Split(':', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 2) continue;

            var tag = TrimOuterQuotes(parts[0].Trim());
            var value = TrimOuterQuotes(parts[1].Trim());

            var matchingTags = osmElement.Tags
                .Where(x => x.Key.Equals(tag, StringComparison.InvariantCultureIgnoreCase)).ToList();

            if (!matchingTags.Any()) continue;

            if (matchingTags.Any(x => x.Value.Equals(value, StringComparison.InvariantCultureIgnoreCase))) return true;
        }

        return false;
    }

    public static async Task<List<IntersectResult>> ProcessOsmIntersections(this List<IntersectResult> toCheck,
        IntersectSettings settings,
        CancellationToken cancellationToken,
        IProgress<string>? progress = null)
    {
        var counter = 0;

        foreach (var loopToCheck in toCheck)
        {
            progress?.Report(
                $"Processing {counter} of {toCheck.Count} features for intersection tags via OSM.");

            try
            {
                await QueryOverpassForIntersectsAsync(loopToCheck, settings,
                    cancellationToken, progress);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error querying OSM Overpass for intersections for feature {FeatureId}",
                    loopToCheck.ContentId);
                progress?.Report($"Error querying OSM Overpass for intersections: {ex.Message}");
            }

            if (settings.OsmInTagging)
                try
                {
                    await QueryOverpassForInAsync(loopToCheck, settings,
                        cancellationToken, progress);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error querying OSM Overpass for 'is_in' data for feature {FeatureId}",
                        loopToCheck.ContentId);
                    progress?.Report($"Error querying OSM Overpass for 'is_in' data: {ex.Message}");
                }
        }

        return toCheck;
    }

    public static async Task QueryOverpassForInAsync(IntersectResult intersectResult,
        IntersectSettings settings, CancellationToken cancellationToken, IProgress<string>? progress)
    {
        if (intersectResult.OsmIsInPoints.Count == 0) return;

        var client = new HttpClient();
        var counter = 0;

        foreach (var point in intersectResult.OsmIsInPoints)
        {
            progress?.Report(
                $"Processing OSM 'in' query for point {counter + 1} of {intersectResult.OsmIsInPoints.Count}.");
            if (counter++ > 0 && settings.RateLimitOsmOverpass) await Task.Delay(500, cancellationToken);

            var query = $"""
                         [out:json];
                         is_in({point.Y},{point.X});
                         out tags;
                         """;

            var content = new StringContent($"data={Uri.EscapeDataString(query)}", Encoding.UTF8,
                "application/x-www-form-urlencoded");

            HttpResponseMessage response;

            try
            {
                await OsmOverpassRateLimiter.WaitForRateLimitAsync(settings.RateLimitOsmOverpass, cancellationToken);

                response = await client.PostAsync(settings.OsmOverpassUrl, content, cancellationToken);

                OsmOverpassRateLimiter.RecordApiCall();

                Log.ForContext("query", query)
                    .ForContext("response.StatusCode", response.StatusCode)
                    .ForContext("hint",
                        "This log entry records 'in' queries to the OSM Overpass API for point-based tag lookup.")
                    .Information("OSM Overpass 'in' Query");

                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                // Log the first attempt failure as a warning
                Log.Warning(ex, "First attempt to query OSM Overpass API failed, retrying after delay");
                progress?.Report($"OSM Overpass API request failed, retrying: {ex.Message}");

                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);

                // Retry once
                await OsmOverpassRateLimiter.WaitForRateLimitAsync(settings.RateLimitOsmOverpass, cancellationToken);

                response = await client.PostAsync(settings.OsmOverpassUrl, content, cancellationToken);

                OsmOverpassRateLimiter.RecordApiCall();

                Log.ForContext("query", query)
                    .ForContext("response.StatusCode", response.StatusCode)
                    .ForContext("hint",
                        "This log entry records 'in' queries to the OSM Overpass API for point-based tag lookup.")
                    .Information("OSM Overpass 'in' Query - Retry");
            }

            response.EnsureSuccessStatusCode();
            var jsonString = await response.Content.ReadAsStringAsync(cancellationToken);

            var options = new JsonSerializerOptions();
            options.Converters.Add(new OsmElementConverter());
            var features = JsonSerializer.Deserialize<OsmResponse>(jsonString, options);

            if (features?.Elements is null) continue;

            foreach (var osmElement in features.Elements)
                if (osmElement.Tags.TryGetValue("name", out var nameTag))
                    if (!string.IsNullOrWhiteSpace(nameTag) &&
                        !intersectResult.Tags.Contains(nameTag, StringComparer.InvariantCultureIgnoreCase))
                    {
                        if (IsOsmElementFiltered(osmElement, settings.OsmTagFilters)) continue;

                        // Get a list of tags to add
                        var tagsToAdd = new List<string> { nameTag };

                        // Create a feature from the point and OSM element tags
                        var feature = new Feature(new Point(point),
                            OsmGeometryHelpers.ConvertOsmTagsToAttributesTable(osmElement));

                        // Create an IntersectWithFeature object
                        var intersectWithFeature = new IntersectWithFeature
                        {
                            Feature = feature,
                            Source = "OSM Is In Query",
                            Tags = tagsToAdd
                        };

                        // Add the feature to the intersections list
                        intersectResult.IntersectsWith.Add(intersectWithFeature);

                        // Add the tag to the tags list
                        intersectResult.Tags.Add(nameTag);

                        // Add the source if it's not already there
                        if (!intersectResult.Sources.Contains("OSM Is In Query"))
                            intersectResult.Sources.Add("OSM Is In Query");
                    }
        }
    }

    public static async Task QueryOverpassForIntersectsAsync(IntersectResult intersectResult,
        IntersectSettings settings, CancellationToken cancellationToken, IProgress<string>? progress)
    {
        if (intersectResult.Features.Count == 0) return;

        var client = new HttpClient();

        // Calculate bounding box of all features
        Envelope? envelope = null;
        foreach (var feature in intersectResult.Features)
        {
            if (feature.Geometry == null) continue;

            var featureEnvelope = feature.Geometry.EnvelopeInternal;
            if (envelope == null)
                envelope = new Envelope(featureEnvelope);
            else
                envelope.ExpandToInclude(featureEnvelope);
        }

        if (envelope == null) return;

        var bufferInMeters = Math.Max(((double)(settings.BufferPointsAndLinesByFeet ?? 0M)).FeetToMeters(), 50);

        envelope.ExpandBy(
            DistanceTools.ApproximateMetersToLongitudeDegrees(bufferInMeters,
                envelope.MinX,
                envelope.MinY),
            DistanceTools.ApproximateMetersToLatitudeDegrees(bufferInMeters,
                envelope.MaxX,
                envelope.MaxY)
        );

        var minLat = envelope.MinY;
        var minLon = envelope.MinX;
        var maxLat = envelope.MaxY;
        var maxLon = envelope.MaxX;

        // Create Overpass query in XML format
        var query = FormattableString.Invariant($"""

                                                                 [out:json];
                                                                 ( node({minLat},{minLon},{maxLat},{maxLon});
                                                                   way({minLat},{minLon},{maxLat},{maxLon}); );
                                                                 out geom qt;
                                                                 <;
                                                                 out qt;
                                                             
                                                 """);

        Log.ForContext("query", query).ForContext("hint",
                "This log entry records queries to the OSM Overpass API in order to facilitate exploring the results at a later time - using the API for tagging is not unique, but I didn't come across useful helps/tips/guidance on best way to include/exclude relevant data - overpass-turbo.eu is a useful resource to manually run and see these queries.")
            .Information("OSM Overpass Query");

        var content = new StringContent($"data={Uri.EscapeDataString(query)}", Encoding.UTF8,
            "application/x-www-form-urlencoded");

        HttpResponseMessage response;

        try
        {
            await OsmOverpassRateLimiter.WaitForRateLimitAsync(settings.RateLimitOsmOverpass, cancellationToken);

            response = await client.PostAsync(settings.OsmOverpassUrl, content, cancellationToken);

            OsmOverpassRateLimiter.RecordApiCall();

            Log.ForContext("query", query)
                .ForContext("response.StatusCode", response.StatusCode)
                .ForContext("hint",
                    "This log entry records queries to the OSM Overpass API in order to facilitate exploring the results at a later time - using the API for tagging is not unique, but I didn't come across useful helps/tips/guidance on best way to include/exclude relevant data - overpass-turbo.eu is a useful resource to manually run and see these queries.")
                .Information("OSM Overpass Query");

            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            // Log the first attempt failure as a warning
            Log.Warning(ex, "First attempt to query OSM Overpass API failed, retrying after delay");
            progress?.Report($"OSM Overpass API request failed, retrying: {ex.Message}");

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);

            // Retry once
            await OsmOverpassRateLimiter.WaitForRateLimitAsync(settings.RateLimitOsmOverpass, cancellationToken);

            response = await client.PostAsync(settings.OsmOverpassUrl, content, cancellationToken);

            OsmOverpassRateLimiter.RecordApiCall();

            Log.ForContext("query", query)
                .ForContext("response.StatusCode", response.StatusCode)
                .ForContext("hint",
                    "This log entry records queries to the OSM Overpass API in order to facilitate exploring the results at a later time - using the API for tagging is not unique, but I didn't come across useful helps/tips/guidance on best way to include/exclude relevant data - overpass-turbo.eu is a useful resource to manually run and see these queries.")
                .Information("OSM Overpass Query - Retry");
        }

        response.EnsureSuccessStatusCode();
        var jsonString = await response.Content.ReadAsStringAsync(cancellationToken);

        var options = new JsonSerializerOptions();
        options.Converters.Add(new OsmElementConverter());
        var features = JsonSerializer.Deserialize<OsmResponse>(jsonString, options);

        if (features?.Elements is null) return;

        var listOfFeatures = new List<IFeature>();
        // Convert to geometries
        foreach (var osmElement in features.Elements)
        {
            // Skip elements without name
            if (!osmElement.Tags.TryGetValue("name", out var name)) continue;

            if (string.IsNullOrEmpty(name)) continue;

            if (IsOsmElementFiltered(osmElement, settings.OsmTagFilters)) continue;

            if (osmElement is OsmNode node) listOfFeatures.Add(OsmGeometryHelpers.NodeToFeature(node));

            if (osmElement is OsmWay way)
            {
                var convertedWay =
                    OsmGeometryHelpers.WayToFeature(OsmWayWithGeometry.FromOsmWay(way,
                        features.Elements.OfType<OsmNode>().ToList(), true));
                if (convertedWay is not null) listOfFeatures.Add(convertedWay);
            }
        }

        var simpleRelations = OsmSimpleRelation.SimpleRelationsFromResponse(features, settings.OsmTagFilters)
            .SelectMany(x => x.Features());

        listOfFeatures.AddRange(simpleRelations);

        foreach (var loopOsmFeatures in listOfFeatures)
            if (intersectResult.Features.Any(x => loopOsmFeatures.Geometry.Intersects(x.Geometry)))
                if (loopOsmFeatures.Attributes.Exists("name"))
                {
                    var nameTag = loopOsmFeatures.Attributes["name"].ToString();
                    if (!string.IsNullOrWhiteSpace(nameTag) &&
                        !intersectResult.Tags.Contains(nameTag, StringComparer.InvariantCultureIgnoreCase))
                    {
                        // Get a list of tags to add
                        var tagsToAdd = new List<string> { nameTag };

                        // Create an IntersectWithFeature object
                        var intersectWithFeature = new IntersectWithFeature
                        {
                            Feature = loopOsmFeatures,
                            Source = "OSM Feature Intersect",
                            Tags = tagsToAdd
                        };

                        // Add the feature to the intersections list
                        intersectResult.IntersectsWith.Add(intersectWithFeature);

                        // Add the tag to the tags list
                        intersectResult.Tags.Add(nameTag);

                        // Add the source if it's not already there
                        if (!intersectResult.Sources.Contains("OSM Feature Intersect"))
                            intersectResult.Sources.Add("OSM Feature Intersect");
                    }
                }
    }
}