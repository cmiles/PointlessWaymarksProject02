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
    private const double MaxQueryAreaDegrees = 0.01; // Maximum area in square degrees

    private static readonly List<OverpassServer> OverpassServers =
    [
        new() { Url = "https://overpass-api.de/api/interpreter" },
        new() { Url = "https://overpass.kumi.systems/api/interpreter" },
        new() { Url = "https://overpass.private.coffee/api/interpreter" }
    ];

    private static readonly HttpClient OsmHttpClient = new() { Timeout = TimeSpan.FromMinutes(3) };

    private static void AddOsmApiServerToServerList(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        var existing =
            OverpassServers.FirstOrDefault(s => string.Equals(s.Url, url, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            OverpassServers.Remove(existing);
            OverpassServers.Add(existing);
            return;
        }

        var server = new OverpassServer { Url = url };
        OverpassServers.Add(server);
    }

    // Add this helper method to divide the envelope if needed
    private static List<Envelope> DivideEnvelopeIfNeeded(Envelope envelope, IntersectResult intersectResult)
    {
        var width = envelope.MaxX - envelope.MinX;
        var height = envelope.MaxY - envelope.MinY;
        var area = width * height;

        if (area <= MaxQueryAreaDegrees)
            // Envelope is small enough, return it as is
            return [envelope];

        // Calculate number of divisions needed based only on area
        var totalDivisionsNeeded = Math.Ceiling(area / MaxQueryAreaDegrees);

        // Try to make divisions roughly square by taking the square root
        var divisionsPerSide = Math.Ceiling(Math.Sqrt(totalDivisionsNeeded));
        var xDivisions = Math.Max(1, (int)divisionsPerSide);
        var yDivisions = Math.Max(1, (int)divisionsPerSide);

        // Adjust divisions based on aspect ratio of the envelope
        var aspectRatio = width / height;
        if (aspectRatio > 1.5)
        {
            // Wider than tall, increase x divisions
            xDivisions = (int)Math.Ceiling(xDivisions * Math.Sqrt(aspectRatio));
            yDivisions = (int)Math.Ceiling(totalDivisionsNeeded / xDivisions);
        }
        else if (aspectRatio < 0.67)
        {
            // Taller than wide, increase y divisions
            yDivisions = (int)Math.Ceiling(yDivisions * Math.Sqrt(1 / aspectRatio));
            xDivisions = (int)Math.Ceiling(totalDivisionsNeeded / yDivisions);
        }

        // Ensure we don't create too many sub-envelopes
        var totalDivisions = xDivisions * yDivisions;
        if (totalDivisions > 25) // Arbitrary limit to prevent excessive queries
        {
            // Recalculate to stay under the limit while maintaining aspect ratio
            var scaleFactor = Math.Sqrt(25.0 / totalDivisions);
            xDivisions = Math.Max(1, (int)Math.Ceiling(xDivisions * scaleFactor));
            yDivisions = Math.Max(1, (int)Math.Ceiling(yDivisions * scaleFactor));
        }

        var result = new List<Envelope>();
        var xStep = width / xDivisions;
        var yStep = height / yDivisions;

        // Create grid of sub-envelopes
        for (var y = 0; y < yDivisions; y++)
        for (var x = 0; x < xDivisions; x++)
        {
            var minX = envelope.MinX + x * xStep;
            var minY = envelope.MinY + y * yStep;
            var maxX = x == xDivisions - 1 ? envelope.MaxX : minX + xStep;
            var maxY = y == yDivisions - 1 ? envelope.MaxY : minY + yStep;

            var subEnvelope = new Envelope(minX, maxX, minY, maxY);
            var subEnvelopeGeometry = GeoJsonTools.EnvelopeToGeometry(subEnvelope);

            // Only add sub-envelope if it intersects with at least one feature
            if (intersectResult.Features.Any(feature =>
                    feature.Geometry.Intersects(subEnvelopeGeometry) || subEnvelopeGeometry.Contains(feature.Geometry)))
                result.Add(subEnvelope);
        }

        return result;
    }

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
                Log.Error(ex,
                    "Error querying OSM Overpass for intersections for feature {FeatureDescription}, ID {FeatureId}",
                    loopToCheck.Description,
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

        // Ensure settings.OsmOverpassUrl is in the static list
        AddOsmApiServerToServerList(settings.OsmOverpassUrl);
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

            string? jsonString = null;
            Exception? lastEx = null;
            var availableServers = OverpassServers.Where(s => !s.FailedIn).Reverse().ToList();
            if (availableServers.Count == 0)
            {
                Log.Error("All OSM Overpass servers failed for 'is_in' query. Server list: {@Servers}",
                    OverpassServers);
                throw new InvalidOperationException(
                    $"All OSM Overpass servers failed for 'is_in' query. Server list: {string.Join(", ", OverpassServers)}");
            }

            OverpassServer? usedServer = null;
            foreach (var serverTry in availableServers)
                try
                {
                    usedServer = serverTry;
                    await OsmOverpassRateLimiter.WaitForRateLimitAsync(settings.RateLimitOsmOverpass,
                        cancellationToken);
                    var response = await OsmHttpClient.PostAsync(serverTry.Url, content, cancellationToken);
                    OsmOverpassRateLimiter.RecordApiCall();
                    Log.ForContext("query", query)
                        .ForContext("server", serverTry.Url)
                        .ForContext("response.StatusCode", response.StatusCode)
                        .ForContext("hint",
                            "This log entry records 'in' queries to the OSM Overpass API for point-based tag lookup.")
                        .Information("OSM Overpass 'in' Query");
                    response.EnsureSuccessStatusCode();
                    jsonString = await response.Content.ReadAsStringAsync(cancellationToken);
                    lastEx = null;
                    break;
                }
                catch (Exception ex)
                {
                    serverTry.FailedIn = true;
                    lastEx = ex;
                    Log.Warning(ex, "Failed OSM Overpass 'is_in' query on {ServerUrl}, trying next available...",
                        serverTry.Url);
                }

            if (lastEx != null)
            {
                Log.Error(lastEx, "All OSM Overpass servers failed for 'is_in' query. Server list: {@Servers}",
                    OverpassServers);
                throw new InvalidOperationException(
                    $"All OSM Overpass servers failed for 'is_in' query. Server list: {string.Join(", ", OverpassServers)}",
                    lastEx);
            }

            var options = new JsonSerializerOptions();
            options.Converters.Add(new OsmElementConverter());

            OsmResponse? features;
            try
            {
                features = JsonSerializer.Deserialize<OsmResponse>(jsonString ?? throw new InvalidOperationException(),
                    options);
                if (features?.Elements == null)
                    throw new JsonException("Deserialized OsmResponse is null or missing elements.");
            }
            catch (Exception ex)
            {
                Log.ForContext("query", query)
                    .ForContext("server", usedServer?.Url)
                    .ForContext("jsonString", jsonString)
                    .Error(ex, "Failed to deserialize OSM Overpass API response. Raw response logged for analysis.");
                throw new InvalidOperationException(
                    $"Failed to deserialize OSM Overpass API response. See inner exception and logs for details. Raw response: {jsonString}",
                    ex);
            }

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

        // Ensure settings.OsmOverpassUrl is in the static list
        AddOsmApiServerToServerList(settings.OsmOverpassUrl);

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

        // Check if the envelope is too large and needs to be divided
        var subEnvelopes = DivideEnvelopeIfNeeded(envelope, intersectResult);
        var allFeatures = new List<IFeature>();

        // Process each sub-envelope
        foreach (var subEnvelope in subEnvelopes)
        {
            progress?.Report($"Processing sub-region {subEnvelopes.IndexOf(subEnvelope) + 1} of {subEnvelopes.Count}");

            var minLat = subEnvelope.MinY;
            var minLon = subEnvelope.MinX;
            var maxLat = subEnvelope.MaxY;
            var maxLon = subEnvelope.MaxX;

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

            string? jsonString = null;
            Exception? lastEx = null;
            var availableServers = OverpassServers.Where(s => !s.FailedIntersect).Reverse().ToList();
            if (availableServers.Count == 0)
            {
                Log.Error("All OSM Overpass servers failed for 'intersect' query. Server list: {@Servers}",
                    OverpassServers);
                throw new InvalidOperationException(
                    $"All OSM Overpass servers failed for 'intersect' query. Server list: {string.Join(", ", OverpassServers)}");
            }

            OverpassServer? usedServer = null;
            foreach (var serverTry in availableServers)
                try
                {
                    usedServer = serverTry;
                    await OsmOverpassRateLimiter.WaitForRateLimitAsync(settings.RateLimitOsmOverpass,
                        cancellationToken);
                    var response = await OsmHttpClient.PostAsync(serverTry.Url, content, cancellationToken);
                    OsmOverpassRateLimiter.RecordApiCall();
                    Log.ForContext("query", query)
                        .ForContext("server", serverTry.Url)
                        .ForContext("response.StatusCode", response.StatusCode)
                        .ForContext("hint",
                            "This log entry records queries to the OSM Overpass API in order to facilitate exploring the results at a later time - using the API for tagging is not unique, but I didn't come across useful helps/tips/guidance on best way to include/exclude relevant data - overpass-turbo.eu is a useful resource to manually run and see these queries.")
                        .Information("OSM Overpass Query");
                    response.EnsureSuccessStatusCode();
                    jsonString = await response.Content.ReadAsStringAsync(cancellationToken);
                    lastEx = null;
                    break;
                }
                catch (Exception ex)
                {
                    serverTry.FailedIntersect = true;
                    lastEx = ex;
                    Log.Warning(ex, "Failed OSM Overpass 'intersect' query on {ServerUrl}, trying next available...",
                        serverTry.Url);
                }

            if (lastEx != null)
            {
                Log.Error(lastEx, "All OSM Overpass servers failed for 'intersect' query. Server list: {@Servers}",
                    OverpassServers);
                throw new InvalidOperationException(
                    $"All OSM Overpass servers failed for 'intersect' query. Server list: {string.Join(", ", OverpassServers)}",
                    lastEx);
            }

            var options = new JsonSerializerOptions();
            options.Converters.Add(new OsmElementConverter());

            OsmResponse? features;

            try
            {
                features = JsonSerializer.Deserialize<OsmResponse>(jsonString ?? throw new InvalidOperationException(),
                    options);
            }
            catch (Exception e)
            {
                Log.ForContext("query", query)
                    .ForContext("server", usedServer?.Url)
                    .ForContext("intersectResultDescription", intersectResult.Description)
                    .ForContext("json", jsonString).ForContext("query", query)
                    .Error(e, $"OSM Json Deserialization Error - {e.Message}");
                throw;
            }

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

            // Add features from this sub-query to the combined results
            allFeatures.AddRange(listOfFeatures);
        }

        foreach (var loopOsmFeatures in allFeatures)
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

    private class OverpassServer
    {
        public bool FailedIn { get; set; }
        public bool FailedIntersect { get; set; }
        public string Url { get; init; } = string.Empty;

        public override string ToString()
        {
            return $"Url={Url}, FailedIntersect={FailedIntersect}, FailedIn={FailedIn}";
        }
    }
}