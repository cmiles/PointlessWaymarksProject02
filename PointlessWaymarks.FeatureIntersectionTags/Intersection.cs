using System.Text.Json;
using System.Text.RegularExpressions;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.FeatureIntersectionTags.Models;
using PointlessWaymarks.SpatialTools;
using Serilog;

namespace PointlessWaymarks.FeatureIntersectionTags;

public static class Intersection
{
    public static async Task<List<IntersectFileTaggingResult>> FileIntersectionTags(this List<FileInfo> toTag,
        IntersectSettings settings, bool tagsToLower, bool sanitizeTags,
        bool tagSpacesToHyphens,
        CancellationToken cancellationToken, int tagMaxCharacterLength = 256,
        IProgress<string>? progress = null)
    {
        var sourceFileAndFeatures = new List<IntersectFileTaggingResult>();
        toTag.ForEach(x => sourceFileAndFeatures.Add(new IntersectFileTaggingResult(x)));

        var metadataFiles = sourceFileAndFeatures.Where(x =>
                FileMetadataTools.ExifToolWriteSupportedExtensions.Any(y =>
                    y.Equals(x.FileToTag.Extension, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var pointBufferInFeet = settings.BufferPointsAndLinesByFeet ?? 0;

        foreach (var loopFile in metadataFiles)
        {
            var location = await FileMetadataTools.Location(loopFile.FileToTag, false, progress);

            if (!location.HasValidLocation()) continue;

            var feature = new Feature(
                pointBufferInFeet > 0
                    ? PointTools.CreateCircle(location.Longitude!.Value, location.Latitude!.Value, pointBufferInFeet)
                    : PointTools.Wgs84Point(location.Longitude!.Value, location.Latitude!.Value),
                new AttributesTable());

            loopFile.IntersectInformation = new IntersectResult(feature)
                { Description = $"File - {loopFile.FileToTag.FullName}" };
            loopFile.IntersectInformation.OsmIsInPoints.Add(new Coordinate(location.Longitude!.Value,
                location.Latitude!.Value));
        }

        var gpxFiles = sourceFileAndFeatures.Where(x =>
            x.FileToTag.Extension.Equals(".GPX")).ToList();

        foreach (var loopGpx in gpxFiles)
        {
            var bufferedTrackLines = await GpxTools.TrackLinesFromGpxFileBuffered(loopGpx.FileToTag, pointBufferInFeet);
            var bufferedRouteLines = await GpxTools.RouteLinesFromGpxFileBuffered(loopGpx.FileToTag, pointBufferInFeet);
            var waypointPoints =
                await GpxTools.WaypointPointsFromGpxFileAs2DCircles(loopGpx.FileToTag, pointBufferInFeet);

            loopGpx.IntersectInformation = new IntersectResult(bufferedTrackLines.features
                .Select(x => x.BufferedFeature).Cast<IFeature>()
                .Union(bufferedRouteLines.features.Select(x => x.BufferedFeature))
                .Union(waypointPoints.features).ToList()) { Description = loopGpx.FileToTag.FullName };

            foreach (var loopWaypoints in waypointPoints.features)
                loopGpx.IntersectInformation.OsmIsInPoints.Add(new Coordinate(loopWaypoints.Geometry.Coordinate));

            foreach (var loopTracks in bufferedTrackLines.features)
                loopGpx.IntersectInformation.OsmIsInPoints.AddRange(
                    LineTools.GetRepresentativePointsFromLine(loopTracks.Feature.Geometry));

            foreach (var loopRoutes in bufferedRouteLines.features)
                loopGpx.IntersectInformation.OsmIsInPoints.AddRange(
                    LineTools.GetRepresentativePointsFromLine(loopRoutes.Feature.Geometry));
        }

        var geojsonFiles = sourceFileAndFeatures.Where(x =>
            x.FileToTag.Extension.Equals(".GEOJSON")).ToList();

        foreach (var loopGeojson in geojsonFiles)
        {
            var features = GeoJsonTools.DeserializeFileToFeatureCollection(loopGeojson.FileToTag.FullName)!.ToList();

            foreach (var loopFeature in features) loopFeature.Attributes.Add("title", loopGeojson.FileToTag.Name);

            loopGeojson.IntersectInformation = new IntersectResult(features)
                { Description = loopGeojson.FileToTag.FullName };

            foreach (var loopFeature in features)
                loopGeojson.IntersectInformation.OsmIsInPoints.Add(loopFeature.Geometry.InteriorPoint.Coordinate);
        }

        var sourceFileAndFeaturesToProcess = sourceFileAndFeatures.Where(x => x.IntersectInformation != null)
            .Select(x => x.IntersectInformation!).ToList();

        await sourceFileAndFeaturesToProcess.IntersectionTags(settings, cancellationToken, progress);

        List<string> ProcessTagsLocal(List<string> toProcess)
        {
            return ProcessTags(toProcess, tagSpacesToHyphens, sanitizeTags, tagsToLower, tagMaxCharacterLength);
        }

        foreach (var loopIntersects in sourceFileAndFeatures)
        {
            if (loopIntersects.IntersectInformation == null)
            {
                loopIntersects.Result = "No Location Found";
                loopIntersects.Notes = "";
                continue;
            }

            if (!loopIntersects.IntersectInformation.IntersectsWith.Any())
            {
                loopIntersects.Result = "No Intersections";
                loopIntersects.Notes = "";
                continue;
            }

            var existingTags = ProcessTagsLocal(await FileMetadataTools.FileKeywords(loopIntersects.FileToTag, true));

            loopIntersects.ExistingTagString = string.Join(",", existingTags);

            var intersectionTags = ProcessTagsLocal(loopIntersects.IntersectInformation!.Tags);

            loopIntersects.NewTagsString =
                string.Join(",", intersectionTags.Except(existingTags, StringComparer.OrdinalIgnoreCase));

            if (string.IsNullOrWhiteSpace(loopIntersects.NewTagsString))
            {
                loopIntersects.Result = "No New Tags";
                continue;
            }

            var allTags = existingTags.Union(intersectionTags).OrderBy(x => x).ToList();

            loopIntersects.FinalTagString = string.Join(",", allTags);

            loopIntersects.Result = "New Tags Found";
            loopIntersects.Notes = $"New Tags from {string.Join(",", loopIntersects.IntersectInformation.Sources)}";
        }

        sourceFileAndFeatures.Where(x => x.IntersectInformation == null).ToList().ForEach(x =>
        {
            x.Result = "No Location Found";
            x.Notes = "";
        });

        return sourceFileAndFeatures;
    }

    public static async Task<List<IntersectResult>> IntersectionTags(this List<IntersectResult> toCheck,
        IntersectSettings settings,
        CancellationToken cancellationToken, IProgress<string>? progress = null)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (settings.FeatureIntersectFiles.Any())
            try
            {
                toCheck.ProcessFileIntersections(settings.FeatureIntersectFiles,
                    cancellationToken, progress);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing file intersections");
                progress?.Report($"Error processing file intersections: {ex.Message}");
            }

        cancellationToken.ThrowIfCancellationRequested();

        if (!string.IsNullOrWhiteSpace(settings.PadUsDirectory) && settings.PadUsAttributes.Any())
            try
            {
                toCheck.ProcessPadUsIntersections(settings.PadUsAttributes.ToList(),
                    settings.PadUsDirectory, cancellationToken,
                    progress);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing PAD-US intersections");
                progress?.Report($"Error processing PAD-US intersections: {ex.Message}");
            }

        cancellationToken.ThrowIfCancellationRequested();

        if (settings.UseOsmOverpass && !string.IsNullOrWhiteSpace(settings.OsmOverpassUrl))
            try
            {
                await toCheck.ProcessOsmIntersections(settings, cancellationToken, progress);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing OSM Overpass intersections");
                progress?.Report($"Error processing OSM Overpass intersections: {ex.Message}");
            }

        return toCheck;
    }

    /// <summary>
    ///     Checks the submitted List of IFeatures for tags based on the submitted settings file - if the settings
    ///     file is blank of invalid an empty list is returned.
    /// </summary>
    /// <param name="intersectSettingsFile"></param>
    /// <param name="toCheck"></param>
    /// <param name="cancellationToken"></param>
    /// <param name="progress"></param>
    /// <returns></returns>
    public static async Task<List<IntersectResult>> IntersectionTags(this List<IntersectResult> toCheck,
        string intersectSettingsFile,
        CancellationToken cancellationToken, IProgress<string>? progress = null)
    {
        if (string.IsNullOrEmpty(intersectSettingsFile))
        {
            progress?.Report("No Settings File Submitted - returning nothing...");

            return toCheck;
        }

        if (!toCheck.Any())
        {
            progress?.Report("No Features to Check - returning nothing...");

            return toCheck;
        }

        progress?.Report($"Getting Settings from {intersectSettingsFile}");
        var settings =
            JsonSerializer.Deserialize<IntersectSettings>(await File.ReadAllTextAsync(intersectSettingsFile,
                cancellationToken));

        if (settings == null)
        {
            progress?.Report($"The settings file {intersectSettingsFile} did not deserialized to valid settings...");

            return toCheck;
        }

        return await toCheck.IntersectionTags(settings, cancellationToken, progress);
    }

    public static async Task<List<string>> IntersectionTags(this IFeature toCheck, string intersectSettingsFile,
        CancellationToken cancellationToken, IProgress<string>? progress = null)
    {
        if (string.IsNullOrEmpty(intersectSettingsFile))
        {
            progress?.Report("No Settings File Submitted - returning nothing...");

            return [];
        }

        var intersectionResult = new IntersectResult(toCheck) { Description = "IFeature Tagging" };

        progress?.Report($"Getting Settings from {intersectSettingsFile}");
        var settings =
            JsonSerializer.Deserialize<IntersectSettings>(await File.ReadAllTextAsync(intersectSettingsFile,
                cancellationToken));

        if (settings == null)
        {
            progress?.Report($"The settings file {intersectSettingsFile} did not deserialized to valid settings...");

            return [];
        }

        return (await intersectionResult.AsList().IntersectionTags(settings, cancellationToken, progress))
            .SelectMany(x => x.Tags).ToList();
    }

    public static async Task<IntersectResult> IntersectionTags(this IntersectResult toCheck,
        string intersectSettingsFile,
        CancellationToken cancellationToken, IProgress<string>? progress = null)
    {
        if (string.IsNullOrEmpty(intersectSettingsFile))
        {
            progress?.Report("No Settings File Submitted - returning nothing...");

            return toCheck;
        }

        progress?.Report($"Getting Settings from {intersectSettingsFile}");
        var settings =
            JsonSerializer.Deserialize<IntersectSettings>(await File.ReadAllTextAsync(intersectSettingsFile,
                cancellationToken));

        if (settings == null)
        {
            progress?.Report($"The settings file {intersectSettingsFile} did not deserialized to valid settings...");

            return toCheck;
        }

        return (await toCheck.AsList().IntersectionTags(settings, cancellationToken, progress)).Single();
    }

    public static List<IntersectResult> ProcessFileIntersections(this List<IntersectResult> toCheck,
        List<IntersectFile> intersectFiles,
        CancellationToken cancellationToken,
        IProgress<string>? progress = null)
    {
        var counter = 0;

        foreach (var loopIntersectFile in intersectFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            counter++;

            progress?.Report(
                $"Processing Feature Intersect - {loopIntersectFile.Name}, {loopIntersectFile.FileName} - {counter} of {intersectFiles.Count}");

            var intersectFileInfo = new FileInfo(loopIntersectFile.FileName);

            if (!intersectFileInfo.Exists)
            {
                progress?.Report($"  Skipping file {loopIntersectFile.FileName} - Does Not Exist.");
                continue;
            }

            var intersectFeatures = GeoJsonTools.DeserializeFileToFeatureCollection(loopIntersectFile.FileName)!;

            var referenceFeatureCounter = 0;
            progress?.Report(
                $" Processing {intersectFeatures.Count} Reference Features against {toCheck.Count} Submitted Feature Sets");

            foreach (var loopIntersectFeature in intersectFeatures)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (++referenceFeatureCounter % 1000 == 0)
                    progress?.Report(
                        $" Processing {loopIntersectFile.Name} - Feature {referenceFeatureCounter} of {intersectFeatures.Count}");

                foreach (var loopCheck in toCheck)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (loopCheck.Features.Any(x =>
                            x.Geometry.Intersects(loopIntersectFeature.Geometry)
                            || x.Geometry.Crosses(loopIntersectFeature.Geometry)
                            || x.Geometry.Contains(loopIntersectFeature.Geometry)
                            || x.Geometry.Overlaps(loopIntersectFeature.Geometry)
                            || x.Geometry.CoveredBy(loopIntersectFeature.Geometry)
                            || x.Geometry.Touches(loopIntersectFeature.Geometry)
                            || x.Geometry.Within(loopIntersectFeature.Geometry)))
                    {
                        // First check if we have any attributes or TagAll to process
                        var hasAttributesToProcess = loopIntersectFile.AttributesForTags.Any(attr =>
                            loopIntersectFeature.Attributes.GetNames().Contains(attr));
                        var hasTagAll = !string.IsNullOrWhiteSpace(loopIntersectFile.TagAll);

                        if (!hasAttributesToProcess && !hasTagAll) continue;

                        // Get a list of tags that would be added
                        var tagsToAdd = new List<string>();

                        // Add tags from attributes
                        foreach (var loopAttribute in loopIntersectFile.AttributesForTags)
                            if (loopIntersectFeature.Attributes.GetNames().Any(a => a == loopAttribute))
                            {
                                var tagValue = (loopIntersectFeature.Attributes[loopAttribute]?.ToString() ??
                                                string.Empty).Trim();
                                if (!string.IsNullOrWhiteSpace(tagValue) &&
                                    !loopCheck.Tags.Any(x => x.Equals(tagValue, StringComparison.OrdinalIgnoreCase)))
                                    tagsToAdd.Add(tagValue);
                            }

                        // Add the TagAll value if specified
                        if (hasTagAll && !loopCheck.Tags.Any(x =>
                                x.Equals(loopIntersectFile.TagAll, StringComparison.OrdinalIgnoreCase)))
                            tagsToAdd.Add(loopIntersectFile.TagAll);

                        // If we don't have any new tags to add, continue to next feature
                        if (!tagsToAdd.Any())
                            continue;

                        // Create IntersectWithFeature object and add it to the list
                        var intersectWithFeature = new IntersectWithFeature
                            { Feature = loopIntersectFeature, Source = loopIntersectFile.Name, Tags = tagsToAdd };
                        loopCheck.IntersectsWith.Add(intersectWithFeature);

                        // Add the source if it's not already there
                        if (!loopCheck.Sources.Any(x =>
                                loopIntersectFile.Name.Equals(x, StringComparison.OrdinalIgnoreCase)))
                            loopCheck.Sources.Add(loopIntersectFile.Name);

                        // Add all the new tags
                        foreach (var tag in tagsToAdd) loopCheck.Tags.Add(tag);
                    }
                }
            }
        }

        progress?.Report("Returning Features and Tags");

        return toCheck;
    }

    public static List<IntersectResult> ProcessPadUsIntersections(this List<IntersectResult> toCheck,
        List<string> attributesForTags,
        string padUsDirectory, CancellationToken cancellationToken,
        IProgress<string>? progress = null)
    {
        //Check for a valid setup - this requires some searching/checking to make sure the submitted
        //directory contains files that seem to make the by convention/documented patterns.
        if (string.IsNullOrWhiteSpace(padUsDirectory)) return toCheck;

        var padUsDirectoryInfo = new DirectoryInfo(padUsDirectory);

        if (!padUsDirectoryInfo.Exists)
        {
            progress?.Report($"PAD-US directory {padUsDirectory} doesn't exist...");
            return toCheck;
        }

        //Get the distinct list of two letter State Codes that are relevant to each of the Features
        //being checked using the offline State/County data - this is used to limit the PAD-US State
        //Data (which is organized by State) that must be loaded and searched.
        var stateCodesByCheck = new List<(string stateCode, IntersectResult feature)>();

        cancellationToken.ThrowIfCancellationRequested();

        foreach (var loopCheck in toCheck)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var stateCounties = StateCountyService.GetStateCountyOffline(loopCheck.Features);

            foreach (var loopStateCode in stateCounties
                         .Select(x => x.StateCode)
                         .Where(x => !string.IsNullOrWhiteSpace(x))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
                stateCodesByCheck.Add((loopStateCode, loopCheck));
        }

        if (!stateCodesByCheck.Any())
        {
            progress?.Report(
                $"Couldn't determine any US State Codes for the Features being checked against the PAD-US data in {padUsDirectoryInfo.FullName}");
            return toCheck;
        }

        //Group by State Code so each State's data is only located, loaded and searched once. Cache
        //the Features read from each Shapefile for the duration of this method run so that a
        //Shapefile relevant to more than one grouping is only read from disk a single time.
        var stateCodeGroups = stateCodesByCheck.GroupBy(x => x.stateCode, StringComparer.OrdinalIgnoreCase).ToList();

        var shapefileFeatureCache = new Dictionary<string, List<IFeature>>(StringComparer.OrdinalIgnoreCase);

        var counter = 0;

        foreach (var loopStateGroup in stateCodeGroups)
        {
            cancellationToken.ThrowIfCancellationRequested();

            counter++;

            var stateCode = loopStateGroup.Key;

            progress?.Report(
                $"Processing PAD-US State Data for {stateCode} - {counter} of {stateCodeGroups.Count}");

            //Find directories matching PADUS[Digit]_[Digit]_State_[StateCode]_GDB_KMZ
            var stateDirectoryRegex = new Regex(
                $"^PADUS[0-9]+_[0-9]+_State_{Regex.Escape(stateCode)}_GDB_KMZ$",
                RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);

            var stateDirectories = padUsDirectoryInfo
                .EnumerateDirectories("*", SearchOption.TopDirectoryOnly)
                .Where(x => stateDirectoryRegex.IsMatch(x.Name))
                .ToList();

            if (!stateDirectories.Any())
            {
                progress?.Report(
                    $"  No PAD-US State directory matching PADUS#_#_State_{stateCode}_GDB_KMZ found in {padUsDirectoryInfo.FullName}");
                continue;
            }

            //Pull together all the IntersectResults Objects for this State to loop thru
            var stateResults = loopStateGroup.Select(x => x.feature).ToList();

            foreach (var loopStateDirectory in stateDirectories)
            {
                cancellationToken.ThrowIfCancellationRequested();

                //Determine the Shapefile to use - if there is a single .shp use it, if there are
                //multiple prefer a tl_20??_us_state.shp file.
                var shapeFiles = loopStateDirectory
                    .EnumerateFiles("*.shp", SearchOption.TopDirectoryOnly).ToList();

                FileInfo? shapeFile;

                if (shapeFiles.Count == 0)
                {
                    progress?.Report($"  No .shp file found in {loopStateDirectory.FullName}");
                    continue;
                }

                if (shapeFiles.Count == 1)
                {
                    shapeFile = shapeFiles.Single();
                }
                else
                {
                    shapeFile = shapeFiles.FirstOrDefault(x =>
                        Regex.IsMatch(x.Name, @"^tl_20[0-9]{2}_us_state\.shp$",
                            RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture));

                    if (shapeFile == null)
                    {
                        progress?.Report(
                            $"  Multiple .shp files found in {loopStateDirectory.FullName} but none matched tl_20??_us_state.shp - skipping");
                        continue;
                    }
                }

                //Read the Shapefile Features - caching the results for the duration of this run so
                //repeated use of a Shapefile does not re-read it from disk.
                if (!shapefileFeatureCache.TryGetValue(shapeFile.FullName, out var shapeFeatures))
                {
                    try
                    {
                        shapeFeatures = NetTopologySuite.IO.Esri.Shapefile
                            .ReadAllFeatures(shapeFile.FullName).Cast<IFeature>().ToList();
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error reading PAD-US Shapefile {ShapefilePath}", shapeFile.FullName);
                        progress?.Report($"  Error reading Shapefile {shapeFile.FullName} - {ex.Message}");
                        shapeFeatures = [];
                    }

                    shapefileFeatureCache[shapeFile.FullName] = shapeFeatures;
                }

                if (shapeFeatures.Count == 0) continue;

                var referenceFeatureCounter = 0;
                progress?.Report(
                    $" Processing {shapeFeatures.Count} Reference Features from {shapeFile.Name} against {stateResults.Count} Submitted Features");

                //Outer loop is the Shapefile's features, inner loop are the features to check for Intersection
                foreach (var loopShapeFeature in shapeFeatures)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (loopShapeFeature?.Geometry == null || loopShapeFeature.Geometry.IsEmpty) continue;

                    if (++referenceFeatureCounter % 5000 == 0)
                        progress?.Report(
                            $" Processing {shapeFile.Name} - Feature {referenceFeatureCounter} of {shapeFeatures.Count}");

                    foreach (var loopCheck in stateResults)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (loopCheck.Features.Any(x => x.Geometry.Intersects(loopShapeFeature.Geometry)))
                        {
                            // Get a list of tags that would be added
                            var tagsToAdd = new List<string>();

                            // Check each attribute and collect the tags
                            foreach (var loopAttribute in attributesForTags)
                                if (loopShapeFeature.Attributes.GetNames().Any(a => a == loopAttribute))
                                {
                                    var tagValue =
                                        (loopShapeFeature.Attributes[loopAttribute]?.ToString() ?? string.Empty)
                                        .Trim();
                                    if (!string.IsNullOrWhiteSpace(tagValue) &&
                                        !loopCheck.Tags.Any(x =>
                                            x.Equals(tagValue, StringComparison.OrdinalIgnoreCase)))
                                        tagsToAdd.Add(tagValue);
                                }

                            // If we don't have any new tags to add, continue to next feature
                            if (!tagsToAdd.Any())
                                continue;

                            // Create IntersectWithFeature object and add it to the list
                            var intersectWithFeature = new IntersectWithFeature
                            {
                                Feature = loopShapeFeature,
                                Source = shapeFile.Name,
                                Tags = tagsToAdd
                            };
                            loopCheck.IntersectsWith.Add(intersectWithFeature);

                            // Add the source if it's not already there
                            if (!loopCheck.Sources.Any(x =>
                                    shapeFile.Name.Equals(x, StringComparison.OrdinalIgnoreCase)))
                                loopCheck.Sources.Add(shapeFile.Name);

                            // Add all the new tags
                            foreach (var tag in tagsToAdd)
                                loopCheck.Tags.Add(tag);
                        }
                    }
                }
            }

            progress?.Report("Returning PAD-US Features and Tags");
        }

        return toCheck;
    }

    /// <summary>
    ///     Processes a list of tags into a list of tags with options applied - result is sorted before it is returned.
    /// </summary>
    /// <param name="toProcess"></param>
    /// <param name="tagSpacesToHyphens"></param>
    /// <param name="sanitizeTags"></param>
    /// <param name="tagsToLower"></param>
    /// <param name="tagMaxCharacterLength"></param>
    /// <returns></returns>
    internal static List<string> ProcessTags(List<string> toProcess, bool tagSpacesToHyphens, bool sanitizeTags,
        bool tagsToLower, int tagMaxCharacterLength)
    {
        if (tagSpacesToHyphens)
        {
            if (sanitizeTags)
            {
                for (var i = 0; i < toProcess.Count; i++)
                    toProcess[i] =
                        SlugTagTools.CreateSlug(tagsToLower, toProcess[i], tagMaxCharacterLength);
                return toProcess;
            }

            if (tagsToLower)
                for (var i = 0; i < toProcess.Count; i++)
                    toProcess[i] = toProcess[i].ToLowerInvariant();

            for (var i = 0; i < toProcess.Count; i++)
            {
                toProcess[i] = Regex.Replace(toProcess[i], @"\s+", " ").Trim();
                toProcess[i] = toProcess[i].Replace(" ", "-");
                toProcess[i] =
                    toProcess[i][
                        ..Math.Min(tagMaxCharacterLength, toProcess[i].Length)];
            }

            return toProcess.OrderBy(x => x).ToList();
        }

        if (sanitizeTags)
        {
            for (var i = 0; i < toProcess.Count; i++)
                toProcess[i] =
                    SlugTagTools.CreateSpacedString(tagsToLower, toProcess[i], tagMaxCharacterLength);
            return toProcess.OrderBy(x => x).ToList();
        }

        if (tagsToLower)
            for (var i = 0; i < toProcess.Count; i++)
                toProcess[i] = toProcess[i].ToLowerInvariant();

        if (!sanitizeTags)
            for (var i = 0; i < toProcess.Count; i++)
                toProcess[i] =
                    toProcess[i][
                        ..Math.Min(tagMaxCharacterLength, toProcess[i].Length)];

        return toProcess.OrderBy(x => x).ToList();
    }

    public static async Task<List<IntersectFileTaggingResult>> WriteTagsToFiles(
        this List<IntersectFileTaggingResult> toWrite, bool testRun,
        bool createBackupBeforeWritingMetadata, bool backupIntoDefaultStorage, bool tagsToLower, bool sanitizeTags,
        bool tagSpacesToHyphens,
        string? exifToolFullName,
        CancellationToken cancellationToken, int tagMaxCharacterLength = 256,
        IProgress<string>? progress = null)
    {
        //Exit if nothing to process
        if (!toWrite.Any(x => x.IntersectInformation != null && x.IntersectInformation.Tags.Any())) return toWrite;

        //Write a result if there was non-null Intersect Information (unsupported files have null Intersect Information)
        var noIntersections = toWrite.Where(x => x.IntersectInformation != null && !x.IntersectInformation.Tags.Any())
            .ToList();

        noIntersections.ForEach(x => { x.Result = "No Tags Found"; });

        //Get a list to work with where we have tags to try to write - no null Intersections
        var filteredList = toWrite.Where(x => x.IntersectInformation != null && x.IntersectInformation.Tags.Any())
            .ToList();

        var exifToolWrites = filteredList.Where(x =>
            FileMetadataTools.ExifToolWriteSupportedExtensions.Any(y =>
                x.FileToTag.Extension.Equals(y, StringComparison.OrdinalIgnoreCase))).ToList();

        var exifTool = FileMetadataTools.ExifToolExecutable(exifToolFullName);
        var frozenExecutionTime = DateTime.Now;

        //Processes a list of tags based on the sanitize, case and length settings - local method
        //so that this can be used with both the intersect and existing tag lists.
        List<string> ProcessTagsLocal(List<string> toProcess)
        {
            return ProcessTags(toProcess, tagSpacesToHyphens, sanitizeTags, tagsToLower, tagMaxCharacterLength);
        }

        foreach (var loopWrite in exifToolWrites)
        {
            var existingTags = ProcessTagsLocal(await FileMetadataTools.FileKeywords(loopWrite.FileToTag, true));

            loopWrite.ExistingTagString = string.Join(",", existingTags);

            var intersectionTags = ProcessTagsLocal(loopWrite.IntersectInformation!.Tags);

            loopWrite.NewTagsString =
                string.Join(",", intersectionTags.Except(existingTags, StringComparer.OrdinalIgnoreCase));

            if (string.IsNullOrWhiteSpace(loopWrite.NewTagsString))
            {
                loopWrite.Result = "No New Tags";
                continue;
            }

            var allTags = existingTags.Union(loopWrite.IntersectInformation!.Tags).OrderBy(x => x).ToList();

            loopWrite.FinalTagString = string.Join(",", allTags);

            var writeRequest = new ExifToolWriteRequest
            {
                Keywords = allTags
            };

            var argsPreview = string.Join(" ", ExifToolWriter.BuildArguments(writeRequest, loopWrite.FileToTag));

            if (testRun)
            {
                loopWrite.Result = "Test Run Success";
                loopWrite.Notes = $"Test Run - would have run ExifTool with {argsPreview}";
                continue;
            }

            if (!exifTool.isPresent)
            {
                loopWrite.Result = "ExifTool Not Found";
                loopWrite.Notes = $"Would have run ExifTool with {argsPreview}";
                continue;
            }

            if (createBackupBeforeWritingMetadata)
            {
                bool backUpSuccessful;

                if (backupIntoDefaultStorage)
                    backUpSuccessful = UniqueFileTools.WriteFileToDefaultStorageDirectoryBackupDirectory(
                        frozenExecutionTime, "PwFeatureIntersectTag", loopWrite.FileToTag,
                        progress);
                else
                    backUpSuccessful = UniqueFileTools.WriteFileToInPlaceBackupDirectory(frozenExecutionTime,
                        "PwFeatureIntersectTag", loopWrite.FileToTag,
                        progress);

                if (!backUpSuccessful)
                {
                    loopWrite.Result = "Backup Error";
                    loopWrite.Notes = "Backup File could not be written - no attempt to write Tags.";
                    progress?.Report(
                        $"GeoTag - Skipping {loopWrite.FileToTag.FullName} - Found Tag information but could not create a backup - skipping this file.");
                    continue;
                }
            }

            try
            {
                var writeResult =
                    await ExifToolWriter.WriteMetadataAsync(exifTool.exifToolFile!, writeRequest,
                        [loopWrite.FileToTag], progress);

                if (!writeResult.Success)
                {
                    Log.ForContext("writeErrors", writeResult.Errors)
                        .ForContext("intersectResults", loopWrite.SafeObjectDump())
                        .ForContext("exifTool", exifTool.SafeObjectDump())
                        .Error($"Writing with ExifTool did not Succeed - {loopWrite.FileToTag.FullName}");

                    loopWrite.Result = "ExifTool Error";
                    loopWrite.Notes =
                        $"ExifTool Reported an Error - {string.Join("; ", writeResult.Errors)}";

                    continue;
                }
            }
            catch (Exception e)
            {
                Log
                    .ForContext("exifToolArgs", argsPreview)
                    .ForContext("exifTool", exifTool.SafeObjectDump())
                    .ForContext("intersectResults", loopWrite.SafeObjectDump())
                    .Error(e,
                        $"Error Tagging {loopWrite.FileToTag.FullName} with ExifTool");
                loopWrite.Result = "ExifTool Error";
                loopWrite.Notes = $"ExifTool Reported an Error - {e.Message}";
                continue;
            }

            loopWrite.Result = "Success";
            loopWrite.Notes = "Wrote Tags with ExifTool";
        }


        return toWrite;
    }
}