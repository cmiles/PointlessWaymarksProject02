using Microsoft.EntityFrameworkCore;
using PointlessWaymarks.CmsData.ContentHtml.TrailHtml;
using PointlessWaymarks.CmsData.Database;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.CmsData.Json;
using PointlessWaymarks.CommonTools;

namespace PointlessWaymarks.CmsData.ContentGeneration;

public static class TrailGenerator
{
    public static async Task GenerateHtml(TrailContent toGenerate, DateTime? generationVersion,
        IProgress<string>? progress = null)
    {
        progress?.Report($"Trail Content - Generate HTML for {toGenerate.Title}");

        var htmlContext = new SingleTrailPage(toGenerate) { GenerationVersion = generationVersion };

        await htmlContext.WriteLocalHtml().ConfigureAwait(false);
    }

    /// <summary>
    ///     Callers must check the generationReturn for success or failure!
    /// </summary>
    /// <param name="toSave"></param>
    /// <param name="generationVersion"></param>
    /// <param name="progress"></param>
    /// <returns></returns>
    public static async Task<(GenerationReturn generationReturn, TrailContent? trailContent)> SaveAndGenerateHtml(
        TrailContent toSave, DateTime? generationVersion, IProgress<string>? progress = null)
    {
        var validationReturn = await Validate(toSave).ConfigureAwait(false);

        if (validationReturn.HasError) return (validationReturn, null);

        try
        {
            Db.DefaultPropertyCleanup(toSave);
            toSave.Tags = SlugTagTools.TagListCleanupToSpacedString(toSave.Tags);

            await Db.SaveTrailContent(toSave).ConfigureAwait(false);
            await GenerateHtml(toSave, generationVersion, progress).ConfigureAwait(false);
            await Export.WriteTrailContentData(toSave, progress).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            return (
                GenerationReturn.Error(
                    $"Error with Trail Content {toSave.Title}",
                    toSave.ContentId,
                    e), toSave);
        }

        DataNotifications.PublishDataNotification("Trail Generator", DataNotificationContentType.Trail,
            DataNotificationUpdateType.LocalContent, [toSave.ContentId]);

        return (GenerationReturn.Success($"Saved and Generated Content And Html for {toSave.Title}"), toSave);
    }

    public static async Task<GenerationReturn> Validate(TrailContent trailContent)
    {
        var rootDirectoryCheck = UserSettingsUtilities.ValidateLocalSiteRootDirectory();

        if (!rootDirectoryCheck.Valid)
            return GenerationReturn.Error($"Problem with Root Directory: {rootDirectoryCheck.Explanation}",
                trailContent.ContentId);

        var commonContentCheck =
            await CommonContentValidation.ValidateContentCommon(trailContent).ConfigureAwait(false);
        if (!commonContentCheck.Valid)
            return GenerationReturn.Error(commonContentCheck.Explanation, trailContent.ContentId);

        var updateFormatCheck = CommonContentValidation.ValidateUpdateContentFormat(trailContent.UpdateNotesFormat);
        if (!updateFormatCheck.Valid)
            return GenerationReturn.Error(updateFormatCheck.Explanation, trailContent.ContentId);

        var db = await Db.Context();

        // Validate MapComponentId exists (if set)
        if (trailContent.MapComponentId is { } mapComponentId)
        {
            var mapComponentExists =
                await db.MapComponents.AnyAsync(x => x.ContentId == mapComponentId).ConfigureAwait(false);
            if (!mapComponentExists)
                return GenerationReturn.Error($"MapComponentId {mapComponentId} does not exist in the database.",
                    trailContent.ContentId);
        }

        // Validate LineContentId exists (if set)
        if (trailContent.LineContentId is { } lineContentId)
        {
            var lineContentExists =
                await db.LineContents.AnyAsync(x => x.ContentId == lineContentId).ConfigureAwait(false);
            if (!lineContentExists)
                return GenerationReturn.Error($"LineContentId {lineContentId} does not exist in the database.",
                    trailContent.ContentId);
        }

        // Validate StartingPointContentId exists (if set)
        if (trailContent.StartingPointContentId is { } startingPointId)
        {
            var startingPointExists =
                await db.PointContents.AnyAsync(x => x.ContentId == startingPointId).ConfigureAwait(false);
            if (!startingPointExists)
                return GenerationReturn.Error(
                    $"StartingPointContentId {startingPointId} does not exist in the database.",
                    trailContent.ContentId);
        }

        // Validate EndingPointContentId exists (if set)
        if (trailContent.EndingPointContentId is { } endingPointId)
        {
            var endingPointExists =
                await db.PointContents.AnyAsync(x => x.ContentId == endingPointId).ConfigureAwait(false);
            if (!endingPointExists)
                return GenerationReturn.Error($"EndingPointContentId {endingPointId} does not exist in the database.",
                    trailContent.ContentId);
        }

        // Validate LineContentId appears in the MapComponent's Elements (if both are set)
        if (trailContent is { MapComponentId: { } mapId, LineContentId: { } lineId })
        {
            var mapComponent = await Db.MapComponentDtoFromContentId(mapId);

            // Assuming MapComponent.Elements is a collection of element objects with an ElementContentId property
            var mapElements = mapComponent.Elements;
            var lineInMap = mapElements.Any(e => e.ElementContentId == lineId);

            if (!lineInMap)
                return GenerationReturn.Error($"LineContentId {lineId} is not present in the MapComponent's elements.",
                    trailContent.ContentId);
        }

        return GenerationReturn.Success("Trail Content Validation Successful");
    }
}