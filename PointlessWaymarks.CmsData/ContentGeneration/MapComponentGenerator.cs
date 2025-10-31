using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using PointlessWaymarks.CmsData.Database;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.CmsData.Json;
using PointlessWaymarks.CmsData.Spatial;

namespace PointlessWaymarks.CmsData.ContentGeneration;

public static class MapComponentGenerator
{
    public static async Task<(GenerationReturn generationReturn, string dataFilePath)> GenerateAllActivityAnonymousDataFile(
        Progress<string>? progress = null)
    {
        var db = await Db.Context();
        var activityLines = await db.LineContents.Where(x =>
            !x.IsDraft && x.ActivityType != null && x.ActivityType != "" && x.RecordingStartedOn != null &&
            x.RecordingEndedOn != null).Select(x => new
        {
            folder = x.Folder,
            activityType = x.ActivityType,
            start = x.RecordingStartedOn,
            end = x.RecordingEndedOn,
            distanceMiles = x.LineDistance,
            lowestElevationFeet = x.MinimumElevation,
            highestElevationFeet = x.MaximumElevation,
            climbFeet = x.ClimbElevation,
            descentFeet = x.DescentElevation
        }).OrderByDescending(x => x.start).AsNoTracking().ToListAsync();

        var jsonFile = new FileInfo(Path.Combine(
            UserSettingsSingleton.CurrentSettings().LocalSiteContentDataDirectory().FullName,
            $"anonymousActivityData.json"));

        var json = JsonSerializer.Serialize(activityLines, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(jsonFile.FullName, json);

        return (
            GenerationReturn.Success(
                $"Saved and Generated Anonymous Activity Data to {jsonFile.FullName}"),
            jsonFile.FullName);
    }

    public static async Task<(GenerationReturn generationReturn, MapComponentDto? mapDto)> GenerateAllLinesData(
        Progress<string>? progress = null)
    {
        var frozenNow = DateTime.Now;
        var allLines = new MapComponent
        {
            Summary = "All Lines",
            Title = "All Lines",
            ContentVersion = Db.ContentVersionDateTime(),
            CreatedBy = "Map Generator",
            CreatedOn = frozenNow,
            ContentId = new Guid("00000000-0000-0000-0000-000000000001")
        };

        var boundsKeeper = new List<Point>();
        var elementList = new List<MapElement>();

        var db = await Db.Context();

        var dbLines = await db.LineContents.Where(x => !x.IsDraft).OrderByDescending(x => x.CreatedOn).AsNoTracking()
            .ToListAsync();

        foreach (var mapLine in dbLines)
        {
            boundsKeeper.Add(new Point(mapLine.InitialViewBoundsMaxLongitude,
                mapLine.InitialViewBoundsMaxLatitude));
            boundsKeeper.Add(new Point(mapLine.InitialViewBoundsMinLongitude,
                mapLine.InitialViewBoundsMinLatitude));

            elementList.Add(new MapElement
            {
                ElementContentId = mapLine.ContentId,
                IncludeInDefaultView = true,
                IsFeaturedElement = false,
                MapComponentContentId = allLines.ContentId
            });
        }

        var bounds = SpatialConverters.PointBoundingBox(boundsKeeper);

        allLines.InitialViewBoundsMaxLatitude = bounds.MaxY;
        allLines.InitialViewBoundsMaxLongitude = bounds.MaxX;
        allLines.InitialViewBoundsMinLatitude = bounds.MinY;
        allLines.InitialViewBoundsMinLongitude = bounds.MinX;
        allLines.UpdateNotesFormat = ContentFormatDefaults.Content.ToString();

        var mapDto = new MapComponentDto(allLines, elementList);

        var validationReturn = await Validate(mapDto).ConfigureAwait(false);

        if (validationReturn.HasError) return (validationReturn, null);

        Db.DefaultPropertyCleanup(mapDto);

        var savedComponent = await Db.SaveMapComponent(mapDto).ConfigureAwait(false);

        await Export.WriteMapComponentContentData(savedComponent, progress).ConfigureAwait(false);

        DataNotifications.PublishDataNotification("Map Component Generator", DataNotificationContentType.Map,
            DataNotificationUpdateType.LocalContent, [mapDto.ContentId]);

        return (
            GenerationReturn.Success(
                $"Saved and Generated Map Component {mapDto.ContentId} - {allLines.Title}"),
            mapDto);
    }

    /// <summary>
    ///     Callers must check the generationReturn for success or failure!
    /// </summary>
    /// <param name="toSave"></param>
    /// <param name="generationVersion"></param>
    /// <param name="progress"></param>
    /// <returns></returns>
    public static async Task<(GenerationReturn generationReturn, MapComponentDto? mapDto)> SaveAndGenerateData(
        MapComponentDto toSave, DateTime? generationVersion, IProgress<string>? progress = null)
    {
        var validationReturn = await Validate(toSave).ConfigureAwait(false);

        if (validationReturn.HasError) return (validationReturn, null);

        MapComponentDto savedComponent;

        try
        {
            Db.DefaultPropertyCleanup(toSave);
            savedComponent = await Db.SaveMapComponent(toSave).ConfigureAwait(false);
            await Export.WriteMapComponentContentData(savedComponent, progress).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            return (
                GenerationReturn.Error(
                    $"Error with Map Component {toSave.Title}",
                    toSave.ContentId,
                    e), toSave);
        }

        DataNotifications.PublishDataNotification("Map Component Generator", DataNotificationContentType.Map,
            DataNotificationUpdateType.LocalContent, [savedComponent.ContentId]);

        return (
            GenerationReturn.Success(
                $"Saved and Generated Map Component {savedComponent.ContentId} - {savedComponent.Title}"),
            savedComponent);
    }

    public static async Task<GenerationReturn> Validate(MapComponentDto mapComponent)
    {
        var rootDirectoryCheck = UserSettingsUtilities.ValidateLocalSiteRootDirectory();

        if (!rootDirectoryCheck.Valid)
            return GenerationReturn.Error($"Problem with Root Directory: {rootDirectoryCheck.Explanation}",
                mapComponent.ContentId);

        var commonContentCheck = await CommonContentValidation.ValidateMapComponent(mapComponent).ConfigureAwait(false);
        if (!commonContentCheck.Valid)
            return GenerationReturn.Error(commonContentCheck.Explanation, mapComponent.ContentId);

        var updateFormatCheck =
            CommonContentValidation.ValidateUpdateContentFormat(mapComponent.UpdateNotesFormat);
        if (!updateFormatCheck.Valid)
            return GenerationReturn.Error(updateFormatCheck.Explanation, mapComponent.ContentId);

        return GenerationReturn.Success("GeoJson Content Validation Successful");
    }
}