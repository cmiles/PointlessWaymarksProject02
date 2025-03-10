using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Omu.ValueInjecter;
using PointlessWaymarks.CmsData.BracketCodes;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.CmsData.Database.PointDetailDataModels;
using PointlessWaymarks.CmsData.Spatial;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.SpatialTools;
using SQLitePCL;

namespace PointlessWaymarks.CmsData.Database;

public static class Db
{
    public const string ContentTypeDisplayStringForFile = "File";
    public const string ContentTypeDisplayStringForGeoJson = "GeoJson";
    public const string ContentTypeDisplayStringForImage = "Image";
    public const string ContentTypeDisplayStringForLine = "Line";
    public const string ContentTypeDisplayStringForLink = "Link";
    public const string ContentTypeDisplayStringForMap = "Map";
    public const string ContentTypeDisplayStringForNote = "Note";
    public const string ContentTypeDisplayStringForPhoto = "Photo";
    public const string ContentTypeDisplayStringForPoint = "Point";
    public const string ContentTypeDisplayStringForPost = "Post";
    public const string ContentTypeDisplayStringForSnippet = "Snippet";
    public const string ContentTypeDisplayStringForTrail = "Trail";
    public const string ContentTypeDisplayStringForVideo = "Video";

    public static async Task<List<string>> ActivityTypesFromLines()
    {
        var db = await Context().ConfigureAwait(false);

        return await db.LineContents.Where(x => !string.IsNullOrWhiteSpace(x.ActivityType)).GroupBy(x => x.ActivityType)
            .Select(x => x.Key ?? string.Empty).OrderBy(x => x).ToListAsync();
    }

    /// <summary>
    ///     Returns a ContentCommonShell based on the ContentId - all content that types are included but because of the
    ///     transformation to a concrete ContentCommonShell not all data will be available.
    /// </summary>
    /// <param name="db"></param>
    /// <param name="contentId"></param>
    /// <returns></returns>
    public static async Task<ContentCommonShell?> ContentCommonShellFromContentId(this PointlessWaymarksContext db,
        Guid contentId)
    {
        //!!Content Type List!!
        var possibleFile = await db.FileContents.SingleOrDefaultAsync(x => x.ContentId == contentId)
            .ConfigureAwait(false);
        if (possibleFile != null) return (ContentCommonShell)new ContentCommonShell().InjectFrom(possibleFile);

        var possibleGeoJson = await db.GeoJsonContents.SingleOrDefaultAsync(x => x.ContentId == contentId)
            .ConfigureAwait(false);
        if (possibleGeoJson != null)
            return (ContentCommonShell)new ContentCommonShell().InjectFrom(possibleGeoJson);

        var possibleImage = await db.ImageContents.SingleOrDefaultAsync(x => x.ContentId == contentId)
            .ConfigureAwait(false);
        if (possibleImage != null) return (ContentCommonShell)new ContentCommonShell().InjectFrom(possibleImage);

        var possibleLine = await db.LineContents.SingleOrDefaultAsync(x => x.ContentId == contentId)
            .ConfigureAwait(false);
        if (possibleLine != null) return (ContentCommonShell)new ContentCommonShell().InjectFrom(possibleLine);

        var possibleLink = await db.LinkContents.SingleOrDefaultAsync(x => x.ContentId == contentId)
            .ConfigureAwait(false);
        if (possibleLink != null) return (ContentCommonShell)new ContentCommonShell().InjectFrom(possibleLink);

        var possibleNote = await db.NoteContents.SingleOrDefaultAsync(x => x.ContentId == contentId)
            .ConfigureAwait(false);
        if (possibleNote != null) return (ContentCommonShell)new ContentCommonShell().InjectFrom(possibleNote);

        var possiblePhoto = await db.PhotoContents.SingleOrDefaultAsync(x => x.ContentId == contentId)
            .ConfigureAwait(false);
        if (possiblePhoto != null) return (ContentCommonShell)new ContentCommonShell().InjectFrom(possiblePhoto);

        var possiblePoint = await db.PointContents.SingleOrDefaultAsync(x => x.ContentId == contentId)
            .ConfigureAwait(false);
        if (possiblePoint != null) return (ContentCommonShell)new ContentCommonShell().InjectFrom(possiblePoint);

        var possiblePost = await db.PostContents.SingleOrDefaultAsync(x => x.ContentId == contentId)
            .ConfigureAwait(false);
        if (possiblePost != null) return (ContentCommonShell)new ContentCommonShell().InjectFrom(possiblePost);

        var possibleTrail = await db.TrailContents.SingleOrDefaultAsync(x => x.ContentId == contentId)
            .ConfigureAwait(false);
        if (possibleTrail != null) return (ContentCommonShell)new ContentCommonShell().InjectFrom(possibleTrail);

        var possibleVideo = await db.VideoContents.SingleOrDefaultAsync(x => x.ContentId == contentId)
            .ConfigureAwait(false);
        if (possibleVideo != null) return (ContentCommonShell)new ContentCommonShell().InjectFrom(possibleVideo);

        return null;
    }

    public static async Task<List<ContentCommonShell>> ContentCommonShellFromContentIds(
        this PointlessWaymarksContext db, List<Guid>? contentIds)
    {
        if (contentIds == null || !contentIds.Any()) return [];

        var returnList = new List<ContentCommonShell>();

        foreach (var loopIds in contentIds)
        {
            var toAdd = await ContentCommonShellFromContentId(db, loopIds).ConfigureAwait(false);

            if (toAdd != null) returnList.Add(toAdd);
        }

        return returnList;
    }

    /// <summary>
    ///     Returns content where the CreatedOn is in the input date. Return list is sorted ascending by Created On. (Note
    ///     that LastUpdatedOn is not considered).
    /// </summary>
    /// <param name="createdOn"></param>
    /// <returns></returns>
    public static async Task<List<object>> ContentCreatedOnDay(DateTime createdOn)
    {
        var createdOnOnOrAfter = createdOn.Date;
        var createdOnBefore = createdOn.AddDays(1).Date;

        var returnList = new List<(DateTime, object)>();

        var context = await Context();

        //!!Content Type List!!
        returnList.AddRange((await context.FileContents
                .Where(x => x.CreatedOn >= createdOnOnOrAfter && x.CreatedOn < createdOnBefore).ToListAsync())
            .Select(x => (x.CreatedOn, (object)x)));
        returnList.AddRange((await context.GeoJsonContents
                .Where(x => x.CreatedOn >= createdOnOnOrAfter && x.CreatedOn < createdOnBefore).ToListAsync())
            .Select(x => (x.CreatedOn, (object)x)));
        returnList.AddRange((await context.ImageContents
                .Where(x => x.CreatedOn >= createdOnOnOrAfter && x.CreatedOn < createdOnBefore).ToListAsync())
            .Select(x => (x.CreatedOn, (object)x)));
        returnList.AddRange((await context.LineContents
                .Where(x => x.CreatedOn >= createdOnOnOrAfter && x.CreatedOn < createdOnBefore).ToListAsync())
            .Select(x => (x.CreatedOn, (object)x)));
        returnList.AddRange((await context.LinkContents
                .Where(x => x.CreatedOn >= createdOnOnOrAfter && x.CreatedOn < createdOnBefore).ToListAsync())
            .Select(x => (x.CreatedOn, (object)x)));
        returnList.AddRange((await context.NoteContents
                .Where(x => x.CreatedOn >= createdOnOnOrAfter && x.CreatedOn < createdOnBefore).ToListAsync())
            .Select(x => (x.CreatedOn, (object)x)));
        returnList.AddRange((await context.PhotoContents
                .Where(x => x.CreatedOn >= createdOnOnOrAfter && x.CreatedOn < createdOnBefore).ToListAsync())
            .Select(x => (x.CreatedOn, (object)x)));
        returnList.AddRange((await context.PointContents
                .Where(x => x.CreatedOn >= createdOnOnOrAfter && x.CreatedOn < createdOnBefore).ToListAsync())
            .Select(x => (x.CreatedOn, (object)x)));
        returnList.AddRange((await context.PostContents
                .Where(x => x.CreatedOn >= createdOnOnOrAfter && x.CreatedOn < createdOnBefore).ToListAsync())
            .Select(x => (x.CreatedOn, (object)x)));
        returnList.AddRange((await context.TrailContents
                .Where(x => x.CreatedOn >= createdOnOnOrAfter && x.CreatedOn < createdOnBefore).ToListAsync())
            .Select(x => (x.CreatedOn, (object)x)));
        returnList.AddRange((await context.VideoContents
                .Where(x => x.CreatedOn >= createdOnOnOrAfter && x.CreatedOn < createdOnBefore).ToListAsync())
            .Select(x => (x.CreatedOn, (object)x)));

        return returnList.OrderBy(x => x.Item1).Select(x => x.Item2).ToList();
    }

    public static async Task<List<dynamic>> ContentFromBoundingBox(this PointlessWaymarksContext db,
        SpatialBounds bounds)
    {
        var points = (await PointContentFromBoundingBox(db, bounds)).Cast<dynamic>().ToList();
        var lines = (await LineContentFromBoundingBox(db, bounds)).Cast<dynamic>().ToList();
        var geoJson = (await GeoJsonContentFromBoundingBox(db, bounds)).Cast<dynamic>().ToList();
        var optionalLocationContent = await OptionalLocationContentFromBoundingBox(db, bounds);

        return points.Union(lines).Union(geoJson).Union(optionalLocationContent).ToList();
    }

    /// <summary>
    ///     Returns a Bounding Box Search Filtered by Content Type - use the Db.ContentTypeDisplayStringFor_____ strings
    ///     as constants for the contentTypeIdentifiers list.
    /// </summary>
    /// <param name="db"></param>
    /// <param name="bounds"></param>
    /// <param name="contentTypeIdentifiers"></param>
    /// <returns></returns>
    public static async Task<List<dynamic>> ContentFromBoundingBox(this PointlessWaymarksContext db,
        SpatialBounds bounds, List<string> contentTypeIdentifiers)
    {
        var returnList = new List<dynamic>();

        if (contentTypeIdentifiers.Contains(ContentTypeDisplayStringForPoint,
                StringComparer.InvariantCultureIgnoreCase))
            returnList.AddRange(await PointContentFromBoundingBox(db, bounds));
        if (contentTypeIdentifiers.Contains(ContentTypeDisplayStringForLine, StringComparer.InvariantCultureIgnoreCase))
            returnList.AddRange(await LineContentFromBoundingBox(db, bounds));
        if (contentTypeIdentifiers.Contains(ContentTypeDisplayStringForGeoJson,
                StringComparer.InvariantCultureIgnoreCase))
            returnList.AddRange(await GeoJsonContentFromBoundingBox(db, bounds));

        if (contentTypeIdentifiers.Contains(ContentTypeDisplayStringForFile,
                StringComparer.InvariantCultureIgnoreCase))
            returnList.AddRange(await FileContentFromBoundingBox(db, bounds));
        if (contentTypeIdentifiers.Contains(ContentTypeDisplayStringForImage,
                StringComparer.InvariantCultureIgnoreCase))
            returnList.AddRange(await ImageContentFromBoundingBox(db, bounds));
        if (contentTypeIdentifiers.Contains(ContentTypeDisplayStringForPhoto,
                StringComparer.InvariantCultureIgnoreCase))
            returnList.AddRange(await PhotoContentFromBoundingBox(db, bounds));
        if (contentTypeIdentifiers.Contains(ContentTypeDisplayStringForPost,
                StringComparer.InvariantCultureIgnoreCase))
            returnList.AddRange(await PostContentFromBoundingBox(db, bounds));
        if (contentTypeIdentifiers.Contains(ContentTypeDisplayStringForVideo,
                StringComparer.InvariantCultureIgnoreCase))
            returnList.AddRange(await VideoContentFromBoundingBox(db, bounds));

        return returnList;
    }

    /// <summary>
    ///     Takes in a ContentId and returns the Entry as a dynamic, or null if not found. Points are returned as
    ///     a PointContentDto (as dynamic). Because the ContentIds are unique this allows finding content regardless
    ///     of the type.
    /// </summary>
    /// <param name="db"></param>
    /// <param name="contentId"></param>
    /// <returns></returns>
    public static async Task<dynamic?> ContentFromContentId(this PointlessWaymarksContext db, Guid contentId)
    {
        //!!Content Type List!!
        var possibleFile = await db.FileContents.SingleOrDefaultAsync(x => x.ContentId == contentId)
            .ConfigureAwait(false);
        if (possibleFile != null) return possibleFile;

        var possibleGeoJson = await db.GeoJsonContents.SingleOrDefaultAsync(x => x.ContentId == contentId)
            .ConfigureAwait(false);
        if (possibleGeoJson != null) return possibleGeoJson;

        var possibleImage = await db.ImageContents.SingleOrDefaultAsync(x => x.ContentId == contentId)
            .ConfigureAwait(false);
        if (possibleImage != null) return possibleImage;

        var possibleLine = await db.LineContents.SingleOrDefaultAsync(x => x.ContentId == contentId)
            .ConfigureAwait(false);
        if (possibleLine != null) return possibleLine;

        var possibleLink = await db.LinkContents.SingleOrDefaultAsync(x => x.ContentId == contentId)
            .ConfigureAwait(false);
        if (possibleLink != null) return possibleLink;

        var possibleNote = await db.NoteContents.SingleOrDefaultAsync(x => x.ContentId == contentId)
            .ConfigureAwait(false);
        if (possibleNote != null) return possibleNote;

        var possiblePhoto = await db.PhotoContents.SingleOrDefaultAsync(x => x.ContentId == contentId)
            .ConfigureAwait(false);
        if (possiblePhoto != null) return possiblePhoto;

        var possiblePoint = await db.PointContents.SingleOrDefaultAsync(x => x.ContentId == contentId)
            .ConfigureAwait(false);
        if (possiblePoint != null) return await PointContentDto(contentId, db).ConfigureAwait(false);

        var possiblePost = await db.PostContents.SingleOrDefaultAsync(x => x.ContentId == contentId)
            .ConfigureAwait(false);
        if (possiblePost != null) return possiblePost;

        var possibleTrail = await db.TrailContents.SingleOrDefaultAsync(x => x.ContentId == contentId)
            .ConfigureAwait(false);
        if (possibleTrail != null) return possibleTrail;

        var possibleVideo = await db.VideoContents.SingleOrDefaultAsync(x => x.ContentId == contentId)
            .ConfigureAwait(false);
        if (possibleVideo != null) return possibleVideo;

        return null;
    }

    /// <summary>
    ///     Takes in a list of ContentIds and returns matching related Entries as a dynamic, or null if not found.
    ///     Points are returned as a PointContentDto (as dynamic). Note that there is no notification or exception
    ///     from this method if an input ContentId is not matched - only matches are returned.
    ///     of the type.
    /// </summary>
    /// <param name="db"></param>
    /// <param name="contentIds"></param>
    /// <param name="pointsAsDtos"></param>
    /// <returns></returns>
    public static async Task<List<dynamic>> ContentFromContentIds(this PointlessWaymarksContext db,
        List<Guid> contentIds, bool pointsAsDtos = true)
    {
        if (!contentIds.Any()) return [];

        var returnList = new List<dynamic>();

        //!!Content Type List!!
        returnList.AddRange(db.FileContents.Where(x => contentIds.Contains(x.ContentId)));
        returnList.AddRange(db.GeoJsonContents.Where(x => contentIds.Contains(x.ContentId)));
        returnList.AddRange(db.ImageContents.Where(x => contentIds.Contains(x.ContentId)));
        returnList.AddRange(db.LineContents.Where(x => contentIds.Contains(x.ContentId)));
        returnList.AddRange(db.LinkContents.Where(x => contentIds.Contains(x.ContentId)));
        returnList.AddRange(db.NoteContents.Where(x => contentIds.Contains(x.ContentId)));
        returnList.AddRange(db.PhotoContents.Where(x => contentIds.Contains(x.ContentId)));
        if (pointsAsDtos) returnList.AddRange(await PointsAndPointDetails(contentIds).ConfigureAwait(false));
        else returnList.AddRange(db.PointContents.Where(x => contentIds.Contains(x.ContentId)));
        returnList.AddRange(db.PostContents.Where(x => contentIds.Contains(x.ContentId)));
        returnList.AddRange(db.TrailContents.Where(x => contentIds.Contains(x.ContentId)));
        returnList.AddRange(db.VideoContents.Where(x => contentIds.Contains(x.ContentId)));

        return returnList;
    }

    /// <summary>
    ///     Determines if a ContentId is from a Point, Line or GeoJson entry - returns false otherwise (this does not
    ///     validate if the ContentId exists in the database, only if it is present in a spatial type table). Photographs
    ///     ARE NOT considered 'Spatial Content' by this method - this only detects the dedicated Point/Line/GeoJson spatial
    ///     types.
    /// </summary>
    /// <param name="toValidate"></param>
    /// <returns></returns>
    public static async Task<bool> ContentIdIsSpatialContentInDatabase(Guid toValidate)
    {
        var db = await Context().ConfigureAwait(false);

        if (db.PointContents.Any(x => x.ContentId == toValidate)) return true;
        if (db.GeoJsonContents.Any(x => x.ContentId == toValidate)) return true;
        if (db.LineContents.Any(x => x.ContentId == toValidate)) return true;

        return false;
    }

    /// <summary>
    ///     Filters the input list and returns Guids that match the ContentId of a Point, Line, GeoJson or optionally
    ///     photo content with Lat/Long.
    /// </summary>
    /// <param name="toFilter"></param>
    /// <param name="includeOptionalLocationContent"></param>
    /// <returns></returns>
    public static async Task<List<Guid>> ContentIdsAreSpatialContentInDatabase(List<Guid> toFilter,
        bool includeOptionalLocationContent)
    {
        var db = await Context().ConfigureAwait(false);

        var returnList = new List<Guid>();

        returnList.AddRange(await db.PointContents.Where(x => toFilter.Contains(x.ContentId)).Select(x => x.ContentId)
            .ToListAsync());
        returnList.AddRange(await db.LineContents.Where(x => toFilter.Contains(x.ContentId)).Select(x => x.ContentId)
            .ToListAsync());
        returnList.AddRange(await db.GeoJsonContents.Where(x => toFilter.Contains(x.ContentId)).Select(x => x.ContentId)
            .ToListAsync());

        if (includeOptionalLocationContent)
        {
            returnList.AddRange(await db.FileContents
                .Where(x => x.Latitude != null && x.Longitude != null && toFilter.Contains(x.ContentId))
                .Select(x => x.ContentId).ToListAsync());
            returnList.AddRange(await db.ImageContents
                .Where(x => x.Latitude != null && x.Longitude != null && toFilter.Contains(x.ContentId))
                .Select(x => x.ContentId).ToListAsync());
            returnList.AddRange(await db.PhotoContents
                .Where(x => x.Latitude != null && x.Longitude != null && toFilter.Contains(x.ContentId))
                .Select(x => x.ContentId).ToListAsync());
            returnList.AddRange(await db.PostContents
                .Where(x => x.Latitude != null && x.Longitude != null && toFilter.Contains(x.ContentId))
                .Select(x => x.ContentId).ToListAsync());
            returnList.AddRange(await db.VideoContents
                .Where(x => x.Latitude != null && x.Longitude != null && toFilter.Contains(x.ContentId))
                .Select(x => x.ContentId).ToListAsync());
        }

        return returnList;
    }

    public static async Task<List<Guid>> ContentIdsFromBoundingBox(this PointlessWaymarksContext db,
        SpatialBounds bounds)
    {
        var points = (await PointContentIdsFromBoundingBox(db, bounds)).ToList();
        var lines = (await LineContentIdsFromBoundingBox(db, bounds)).ToList();
        var geoJson = (await GeoJsonContentIdsFromBoundingBox(db, bounds)).ToList();
        var optionalLocationContentIds = (await OptionalLocationContentIdsFromBoundingBox(db, bounds)).ToList();

        return points.Union(lines).Union(geoJson).Union(optionalLocationContentIds).ToList();
    }

    public static async Task<List<object>> ContentInFolder(string folderName)
    {
        if (string.IsNullOrWhiteSpace(folderName)) return [];

        var returnList = new List<object>();

        var context = await Context();

        //!!Content Type List!!
        returnList.AddRange(context.FileContents.Where(x => x.Folder == folderName).Cast<object>());
        returnList.AddRange(context.GeoJsonContents.Where(x => x.Folder == folderName).Cast<object>());
        returnList.AddRange(context.ImageContents.Where(x => x.Folder == folderName).Cast<object>());
        returnList.AddRange(context.LineContents.Where(x => x.Folder == folderName).Cast<object>());
        returnList.AddRange(context.NoteContents.Where(x => x.Folder == folderName).Cast<object>());
        returnList.AddRange(context.PhotoContents.Where(x => x.Folder == folderName).Cast<object>());
        returnList.AddRange(context.PointContents.Where(x => x.Folder == folderName).Cast<object>());
        returnList.AddRange(context.PostContents.Where(x => x.Folder == folderName).Cast<object>());
        returnList.AddRange(context.TrailContents.Where(x => x.Folder == folderName).Cast<object>());
        returnList.AddRange(context.VideoContents.Where(x => x.Folder == folderName).Cast<object>());

        return returnList;
    }

    /// <summary>
    ///     Returns Content where it was last updated, or created on and never updated, on the input day. List returns with
    ///     an ascending sort on the LastUpdatedOn or CreatedOn used to filter the item. Drafts and items excluded from
    ///     search are not included.
    /// </summary>
    /// <param name="createdOn"></param>
    /// <returns></returns>
    public static async Task<List<object>> ContentLastUpdatedCreatedOnDayNoDrafts(DateTime createdOn)
    {
        var createdOnOnOrAfter = createdOn.Date;
        var createdOnBefore = createdOn.AddDays(1).Date;

        var returnList = new List<(DateTime, object)>();

        var context = await Context();

        //!!Content Type List!!
        returnList.AddRange((await context.FileContents
            .Where(x => x.LastUpdatedOn == null && x.CreatedOn >= createdOnOnOrAfter && x.CreatedOn < createdOnBefore &&
                        !x.IsDraft && x.ShowInSearch).ToListAsync()).Select(x => (x.CreatedOn, (object)x)));
        returnList.AddRange((await context.GeoJsonContents
            .Where(x => x.LastUpdatedOn == null && x.CreatedOn >= createdOnOnOrAfter && x.CreatedOn < createdOnBefore &&
                        !x.IsDraft && x.ShowInSearch).ToListAsync()).Select(x => (x.CreatedOn, (object)x)));
        returnList.AddRange((await context.ImageContents
            .Where(x => x.LastUpdatedOn == null && x.CreatedOn >= createdOnOnOrAfter && x.CreatedOn < createdOnBefore &&
                        !x.IsDraft && x.ShowInSearch).ToListAsync()).Select(x => (x.CreatedOn, (object)x)));
        returnList.AddRange((await context.LineContents
            .Where(x => x.LastUpdatedOn == null && x.CreatedOn >= createdOnOnOrAfter && x.CreatedOn < createdOnBefore &&
                        !x.IsDraft && x.ShowInSearch).ToListAsync()).Select(x => (x.CreatedOn, (object)x)));
        returnList.AddRange((await context.LinkContents
            .Where(x => x.LastUpdatedOn == null && x.CreatedOn >= createdOnOnOrAfter && x.CreatedOn < createdOnBefore)
            .ToListAsync()).Select(x => (x.CreatedOn, (object)x)));
        returnList.AddRange((await context.NoteContents
            .Where(x => x.LastUpdatedOn == null && x.CreatedOn >= createdOnOnOrAfter && x.CreatedOn < createdOnBefore &&
                        !x.IsDraft && x.ShowInSearch).ToListAsync()).Select(x => (x.CreatedOn, (object)x)));
        returnList.AddRange((await context.PhotoContents
            .Where(x => x.LastUpdatedOn == null && x.CreatedOn >= createdOnOnOrAfter && x.CreatedOn < createdOnBefore &&
                        !x.IsDraft && x.ShowInSearch).ToListAsync()).Select(x => (x.CreatedOn, (object)x)));
        returnList.AddRange((await context.PointContents
            .Where(x => x.LastUpdatedOn == null && x.CreatedOn >= createdOnOnOrAfter && x.CreatedOn < createdOnBefore &&
                        !x.IsDraft && x.ShowInSearch).ToListAsync()).Select(x => (x.CreatedOn, (object)x)));
        returnList.AddRange((await context.PostContents
            .Where(x => x.LastUpdatedOn == null && x.CreatedOn >= createdOnOnOrAfter && x.CreatedOn < createdOnBefore &&
                        !x.IsDraft && x.ShowInSearch).ToListAsync()).Select(x => (x.CreatedOn, (object)x)));
        returnList.AddRange((await context.TrailContents
            .Where(x => x.LastUpdatedOn == null && x.CreatedOn >= createdOnOnOrAfter && x.CreatedOn < createdOnBefore &&
                        !x.IsDraft && x.ShowInSearch).ToListAsync()).Select(x => (x.CreatedOn, (object)x)));
        returnList.AddRange((await context.VideoContents
            .Where(x => x.LastUpdatedOn == null && x.CreatedOn >= createdOnOnOrAfter && x.CreatedOn < createdOnBefore &&
                        !x.IsDraft && x.ShowInSearch).ToListAsync()).Select(x => (x.CreatedOn, (object)x)));

        //!!Content Type List!!
        returnList.AddRange((await context.FileContents
                .Where(x => x.LastUpdatedOn != null && x.LastUpdatedOn >= createdOnOnOrAfter &&
                            x.LastUpdatedOn < createdOnBefore && !x.IsDraft && x.ShowInSearch).ToListAsync())
            .Select(x => (x.LastUpdatedOn!.Value, (object)x)));
        returnList.AddRange((await context.GeoJsonContents
                .Where(x => x.LastUpdatedOn != null && x.LastUpdatedOn >= createdOnOnOrAfter &&
                            x.LastUpdatedOn < createdOnBefore && !x.IsDraft && x.ShowInSearch).ToListAsync())
            .Select(x => (x.LastUpdatedOn!.Value, (object)x)));
        returnList.AddRange((await context.ImageContents
                .Where(x => x.LastUpdatedOn != null && x.LastUpdatedOn >= createdOnOnOrAfter &&
                            x.LastUpdatedOn < createdOnBefore && !x.IsDraft && x.ShowInSearch).ToListAsync())
            .Select(x => (x.LastUpdatedOn!.Value, (object)x)));
        returnList.AddRange((await context.LineContents
                .Where(x => x.LastUpdatedOn != null && x.LastUpdatedOn >= createdOnOnOrAfter &&
                            x.LastUpdatedOn < createdOnBefore && !x.IsDraft && x.ShowInSearch).ToListAsync())
            .Select(x => (x.LastUpdatedOn!.Value, (object)x)));
        returnList.AddRange((await context.LinkContents
                .Where(x => x.LastUpdatedOn != null && x.LastUpdatedOn >= createdOnOnOrAfter &&
                            x.LastUpdatedOn < createdOnBefore).ToListAsync())
            .Select(x => (x.LastUpdatedOn!.Value, (object)x)));
        returnList.AddRange((await context.NoteContents
                .Where(x => x.LastUpdatedOn != null && x.LastUpdatedOn >= createdOnOnOrAfter &&
                            x.LastUpdatedOn < createdOnBefore && !x.IsDraft && x.ShowInSearch).ToListAsync())
            .Select(x => (x.LastUpdatedOn!.Value, (object)x)));
        returnList.AddRange((await context.PhotoContents
                .Where(x => x.LastUpdatedOn != null && x.LastUpdatedOn >= createdOnOnOrAfter &&
                            x.LastUpdatedOn < createdOnBefore && !x.IsDraft && x.ShowInSearch).ToListAsync())
            .Select(x => (x.LastUpdatedOn!.Value, (object)x)));
        returnList.AddRange((await context.PointContents
                .Where(x => x.LastUpdatedOn != null && x.LastUpdatedOn >= createdOnOnOrAfter &&
                            x.LastUpdatedOn < createdOnBefore && !x.IsDraft && x.ShowInSearch).ToListAsync())
            .Select(x => (x.LastUpdatedOn!.Value, (object)x)));
        returnList.AddRange((await context.PostContents
                .Where(x => x.LastUpdatedOn != null && x.LastUpdatedOn >= createdOnOnOrAfter &&
                            x.LastUpdatedOn < createdOnBefore && !x.IsDraft && x.ShowInSearch).ToListAsync())
            .Select(x => (x.LastUpdatedOn!.Value, (object)x)));
        returnList.AddRange((await context.TrailContents
                .Where(x => x.LastUpdatedOn != null && x.LastUpdatedOn >= createdOnOnOrAfter &&
                            x.LastUpdatedOn < createdOnBefore && !x.IsDraft && x.ShowInSearch).ToListAsync())
            .Select(x => (x.LastUpdatedOn!.Value, (object)x)));
        returnList.AddRange((await context.VideoContents
                .Where(x => x.LastUpdatedOn != null && x.LastUpdatedOn >= createdOnOnOrAfter &&
                            x.LastUpdatedOn < createdOnBefore && !x.IsDraft && x.ShowInSearch).ToListAsync())
            .Select(x => (x.LastUpdatedOn!.Value, (object)x)));

        return returnList.OrderBy(x => x.Item1).Select(x => x.Item2).ToList();
    }

    public static async Task<List<object>> ContentNeverUpdated()
    {
        var returnList = new List<object>();

        var context = await Context();

        //!!Content Type List!!
        returnList.AddRange(context.FileContents.Where(x => x.LastUpdatedOn == null).Cast<object>());
        returnList.AddRange(context.GeoJsonContents.Where(x => x.LastUpdatedOn == null).Cast<object>());
        returnList.AddRange(context.ImageContents.Where(x => x.LastUpdatedOn == null).Cast<object>());
        returnList.AddRange(context.LineContents.Where(x => x.LastUpdatedOn == null).Cast<object>());
        returnList.AddRange(context.LinkContents.Where(x => x.LastUpdatedOn == null).Cast<object>());
        returnList.AddRange(context.NoteContents.Where(x => x.LastUpdatedOn == null).Cast<object>());
        returnList.AddRange(context.PhotoContents.Where(x => x.LastUpdatedOn == null).Cast<object>());
        returnList.AddRange(context.PointContents.Where(x => x.LastUpdatedOn == null).Cast<object>());
        returnList.AddRange(context.PostContents.Where(x => x.LastUpdatedOn == null).Cast<object>());
        returnList.AddRange(context.TrailContents.Where(x => x.LastUpdatedOn == null).Cast<object>());
        returnList.AddRange(context.VideoContents.Where(x => x.LastUpdatedOn == null).Cast<object>());

        return returnList;
    }

    /// <summary>
    ///     A standardized conversion of a Content Type into a simple standard display string. In some places in the code
    ///     this string may be used to identify a type (such as a serialized version of the type) BEWARE that for both Point
    ///     and
    ///     Map the poco and DTO versions will return the same string and you will need to detect or know from context which
    ///     type is being used.
    /// </summary>
    /// <param name="content"></param>
    /// <returns></returns>
    public static string ContentTypeDisplayString(dynamic content)
    {
        //!!Content Type List!!
        return content switch
        {
            FileContent => ContentTypeDisplayStringForFile,
            GeoJsonContent => ContentTypeDisplayStringForGeoJson,
            ImageContent => ContentTypeDisplayStringForImage,
            LineContent => ContentTypeDisplayStringForLine,
            LinkContent => ContentTypeDisplayStringForLink,
            MapComponent => ContentTypeDisplayStringForMap,
            MapComponentDto => ContentTypeDisplayStringForMap,
            NoteContent => ContentTypeDisplayStringForNote,
            PhotoContent => ContentTypeDisplayStringForPhoto,
            PostContent => ContentTypeDisplayStringForPost,
            PointContent => ContentTypeDisplayStringForPoint,
            Models.PointContentDto => ContentTypeDisplayStringForPoint,
            Snippet => ContentTypeDisplayStringForSnippet,
            TrailContent => ContentTypeDisplayStringForTrail,
            VideoContent => ContentTypeDisplayStringForVideo,
            _ => string.Empty
        };
    }

    /// <summary>
    ///     A standardized conversion of a Guid into a simple standard display string. Both Point and
    ///     PointDto will return the same string.
    /// </summary>
    /// <param name="db"></param>
    /// <param name="contentGuid"></param>
    /// <returns></returns>
    public static async Task<string> ContentTypeDisplayString(this PointlessWaymarksContext db, Guid contentGuid)
    {
        return ContentTypeDisplayString(await db.ContentFromContentId(contentGuid));
    }

    public static async Task<List<object>> ContentUpdatedOnDay(DateTime updatedOn)
    {
        var updatedOnOnOrAfter = updatedOn.Date;
        var updatedOnBefore = updatedOn.AddDays(1).Date;

        var returnList = new List<object>();

        var context = await Context();

        //!!Content Type List!!

        returnList.AddRange(context.FileContents
            .Where(x => x.LastUpdatedOn >= updatedOnOnOrAfter && x.LastUpdatedOn < updatedOnBefore).Cast<object>());
        returnList.AddRange(context.GeoJsonContents
            .Where(x => x.LastUpdatedOn >= updatedOnOnOrAfter && x.LastUpdatedOn < updatedOnBefore).Cast<object>());
        returnList.AddRange(context.ImageContents
            .Where(x => x.LastUpdatedOn >= updatedOnOnOrAfter && x.LastUpdatedOn < updatedOnBefore).Cast<object>());
        returnList.AddRange(context.LineContents
            .Where(x => x.LastUpdatedOn >= updatedOnOnOrAfter && x.LastUpdatedOn < updatedOnBefore).Cast<object>());
        returnList.AddRange(context.LinkContents
            .Where(x => x.LastUpdatedOn >= updatedOnOnOrAfter && x.LastUpdatedOn < updatedOnBefore).Cast<object>());
        returnList.AddRange(context.NoteContents
            .Where(x => x.LastUpdatedOn >= updatedOnOnOrAfter && x.LastUpdatedOn < updatedOnBefore).Cast<object>());
        returnList.AddRange(context.PhotoContents
            .Where(x => x.LastUpdatedOn >= updatedOnOnOrAfter && x.LastUpdatedOn < updatedOnBefore).Cast<object>());
        returnList.AddRange(context.PointContents
            .Where(x => x.LastUpdatedOn >= updatedOnOnOrAfter && x.LastUpdatedOn < updatedOnBefore).Cast<object>());
        returnList.AddRange(context.PostContents
            .Where(x => x.LastUpdatedOn >= updatedOnOnOrAfter && x.LastUpdatedOn < updatedOnBefore).Cast<object>());
        returnList.AddRange(context.TrailContents
            .Where(x => x.LastUpdatedOn >= updatedOnOnOrAfter && x.LastUpdatedOn < updatedOnBefore).Cast<object>());
        returnList.AddRange(context.VideoContents
            .Where(x => x.LastUpdatedOn >= updatedOnOnOrAfter && x.LastUpdatedOn < updatedOnBefore).Cast<object>());

        return returnList;
    }

    /// <summary>
    ///     Returns a new UTC datetime trimmed to the second - more importantly this is a wrapper around the conventions
    ///     for 'ContentVersion' and be used so that those are consistent.
    /// </summary>
    /// <returns></returns>
    public static DateTime ContentVersionDateTime()
    {
        var frozenNow = DateTime.Now.ToUniversalTime();
        return new DateTime(frozenNow.Year, frozenNow.Month, frozenNow.Day, frozenNow.Hour, frozenNow.Minute,
            frozenNow.Second, frozenNow.Kind);
    }

    /// <summary>
    ///     Returns a database context based on the current settings.
    /// </summary>
    /// <returns></returns>
    public static Task<PointlessWaymarksContext> Context()
    {
        // https://github.com/aspnet/EntityFrameworkCore/issues/9994#issuecomment-508588678
        Batteries_V2.Init();
        raw.sqlite3_config(2 /*SQLITE_CONFIG_MULTITHREAD*/);

        var optionsBuilder = new DbContextOptionsBuilder<PointlessWaymarksContext>();
        var dbPath = UserSettingsSingleton.CurrentSettings().DatabaseFileFullName();
        return Task.FromResult(new PointlessWaymarksContext(optionsBuilder.UseSqlite($"Data Source={dbPath}").Options));
    }

    /// <summary>
    ///     Uses reflection to Trim and Convert Nulls to Empty on all string properties, truncate DateTimes to the
    ///     second and round Spatial values to an appropriate value.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="toProcess"></param>
    public static void DefaultPropertyCleanup<T>(T toProcess)
    {
        StringTools.TrimNullToEmptyAllStringProperties(toProcess);
        DateTimeTools.TrimDateTimesToSeconds(toProcess);
        SpatialHelpers.RoundSpatialValues(toProcess);
    }

    /// <summary>
    ///     Returns a List of 'Fully Deleted' Content - ie where the ContentId is no longer present in the related Content
    ///     table.
    /// </summary>
    /// <returns></returns>
    public static async Task<List<HistoricFileContent>> DeletedFileContent()
    {
        var db = await Context().ConfigureAwait(false);

        var deletedContent = await (from h in db.HistoricFileContents
            where !db.FileContents.Any(x => x.ContentId == h.ContentId)
            select h).ToListAsync().ConfigureAwait(false);

        return deletedContent.GroupBy(x => x.ContentId).Select(x => x.OrderByDescending(y => y.ContentVersion).First())
            .ToList();
    }

    /// <summary>
    ///     Returns a List of 'Fully Deleted' Content - ie where the ContentId is no longer present in the related Content
    ///     table.
    /// </summary>
    /// <returns></returns>
    public static async Task<List<HistoricGeoJsonContent>> DeletedGeoJsonContent()
    {
        var db = await Context().ConfigureAwait(false);

        var deletedContent = await (from h in db.HistoricGeoJsonContents
            where !db.GeoJsonContents.Any(x => x.ContentId == h.ContentId)
            select h).ToListAsync().ConfigureAwait(false);

        return deletedContent.GroupBy(x => x.ContentId).Select(x => x.OrderByDescending(y => y.ContentVersion).First())
            .ToList();
    }

    /// <summary>
    ///     Returns a List of 'Fully Deleted' Content - ie where the ContentId is no longer present in the related Content
    ///     table.
    /// </summary>
    /// <returns></returns>
    public static async Task<List<HistoricImageContent>> DeletedImageContent()
    {
        var db = await Context().ConfigureAwait(false);

        var deletedContent = await (from h in db.HistoricImageContents
            where !db.ImageContents.Any(x => x.ContentId == h.ContentId)
            select h).ToListAsync().ConfigureAwait(false);

        return deletedContent.GroupBy(x => x.ContentId).Select(x => x.OrderByDescending(y => y.ContentVersion).First())
            .ToList();
    }

    /// <summary>
    ///     Returns a List of 'Fully Deleted' Content - ie where the ContentId is no longer present in the related Content
    ///     table.
    /// </summary>
    /// <returns></returns>
    public static async Task<List<HistoricLineContent>> DeletedLineContent()
    {
        var db = await Context().ConfigureAwait(false);

        var deletedContent = await (from h in db.HistoricLineContents
            where !db.LineContents.Any(x => x.ContentId == h.ContentId)
            select h).ToListAsync().ConfigureAwait(false);

        return deletedContent.GroupBy(x => x.ContentId).Select(x => x.OrderByDescending(y => y.ContentVersion).First())
            .ToList();
    }

    /// <summary>
    ///     Returns a List of 'Fully Deleted' Content - ie where the ContentId is no longer present in the related Content
    ///     table.
    /// </summary>
    /// <returns></returns>
    public static async Task<List<HistoricLinkContent>> DeletedLinkContent()
    {
        var db = await Context().ConfigureAwait(false);

        var deletedContent = await (from h in db.HistoricLinkContents
            where !db.LinkContents.Any(x => x.ContentId == h.ContentId)
            select h).ToListAsync().ConfigureAwait(false);

        return deletedContent.GroupBy(x => x.ContentId).Select(x => x.OrderByDescending(y => y.ContentVersion).First())
            .ToList();
    }

    /// <summary>
    ///     Returns a List of 'Fully Deleted' Content - ie where the ContentId is no longer present in the related Content
    ///     table.
    /// </summary>
    /// <returns></returns>
    public static async Task<List<HistoricNoteContent>> DeletedNoteContent()
    {
        var db = await Context().ConfigureAwait(false);

        var deletedContent = await (from h in db.HistoricNoteContents
            where !db.NoteContents.Any(x => x.ContentId == h.ContentId)
            select h).ToListAsync().ConfigureAwait(false);

        return deletedContent.GroupBy(x => x.ContentId).Select(x => x.OrderByDescending(y => y.ContentVersion).First())
            .ToList();
    }

    /// <summary>
    ///     Returns a List of 'Fully Deleted' Content - ie where the ContentId is no longer present in the related Content
    ///     table.
    /// </summary>
    /// <returns></returns>
    public static async Task<List<HistoricPhotoContent>> DeletedPhotoContent()
    {
        var db = await Context().ConfigureAwait(false);

        var deletedContent = await (from h in db.HistoricPhotoContents
            where !db.PhotoContents.Any(x => x.ContentId == h.ContentId)
            select h).ToListAsync().ConfigureAwait(false);

        return deletedContent.GroupBy(x => x.ContentId).Select(x => x.OrderByDescending(y => y.ContentVersion).First())
            .ToList();
    }

    /// <summary>
    ///     Returns a List of 'Fully Deleted' Content - ie where the ContentId is no longer present in the related Content
    ///     table.
    /// </summary>
    /// <returns></returns>
    public static async Task<List<HistoricPointContent>> DeletedPointContent()
    {
        var db = await Context().ConfigureAwait(false);

        var deletedContent = await (from h in db.HistoricPointContents
            where !db.PointContents.Any(x => x.ContentId == h.ContentId)
            select h).ToListAsync().ConfigureAwait(false);

        return deletedContent.GroupBy(x => x.ContentId).Select(x => x.OrderByDescending(y => y.ContentVersion).First())
            .ToList();
    }

    /// <summary>
    ///     Returns a List of 'Fully Deleted' Content - ie where the ContentId is no longer present in the related Content
    ///     table.
    /// </summary>
    /// <returns></returns>
    public static async Task<List<HistoricPostContent>> DeletedPostContent()
    {
        var db = await Context().ConfigureAwait(false);

        var deletedContent = await (from h in db.HistoricPostContents
            where !db.PostContents.Any(x => x.ContentId == h.ContentId)
            select h).ToListAsync().ConfigureAwait(false);

        return deletedContent.GroupBy(x => x.ContentId).Select(x => x.OrderByDescending(y => y.ContentVersion).First())
            .ToList();
    }

    /// <summary>
    ///     Returns a List of 'Fully Deleted' Content - ie where the ContentId is no longer present in the related Content
    ///     table.
    /// </summary>
    /// <returns></returns>
    public static async Task<List<HistoricTrailContent>> DeletedTrailContent()
    {
        var db = await Context().ConfigureAwait(false);

        var deletedContent = await (from h in db.HistoricTrailContents
            where !db.TrailContents.Any(x => x.ContentId == h.ContentId)
            select h).ToListAsync().ConfigureAwait(false);

        return deletedContent.GroupBy(x => x.ContentId).Select(x => x.OrderByDescending(y => y.ContentVersion).First())
            .ToList();
    }

    /// <summary>
    ///     Returns a List of 'Fully Deleted' Content - ie where the ContentId is no longer present in the related Content
    ///     table.
    /// </summary>
    /// <returns></returns>
    public static async Task<List<HistoricVideoContent>> DeletedVideoContent()
    {
        var db = await Context().ConfigureAwait(false);

        var deletedContent = await (from h in db.HistoricVideoContents
            where !db.VideoContents.Any(x => x.ContentId == h.ContentId)
            select h).ToListAsync().ConfigureAwait(false);

        return deletedContent.GroupBy(x => x.ContentId).Select(x => x.OrderByDescending(y => y.ContentVersion).First())
            .ToList();
    }

    /// <summary>
    ///     Deletes a Content Entry writing Historic Content, showing progress and publishing Data Notifications.
    ///     In general use this rather deleting content directly...
    /// </summary>
    /// <returns></returns>
    public static async Task DeleteFileContent(Guid contentId, IProgress<string>? progress = null)
    {
        var context = await Context().ConfigureAwait(false);

        var toHistoric = await context.FileContents.Where(x => x.ContentId == contentId).ToListAsync()
            .ConfigureAwait(false);

        if (!toHistoric.Any())
        {
            DataNotifications.PublishDataNotification("Db", DataNotificationContentType.File,
                DataNotificationUpdateType.Delete, contentId.AsList());
            return;
        }

        progress?.Report($"Writing {toHistoric.First().Title} Last Historic Entry");

        var contentIdsToDelete = toHistoric.Select(x => x.ContentId).ToList();

        foreach (var loopToHistoric in toHistoric)
        {
            var newHistoric = new HistoricFileContent();
            newHistoric.InjectFrom(loopToHistoric);
            newHistoric.Id = 0;
            newHistoric.LastUpdatedOn = DateTime.Now;
            if (string.IsNullOrWhiteSpace(newHistoric.LastUpdatedBy))
                newHistoric.LastUpdatedBy = "Historic Entry Archivist";
            await context.HistoricFileContents.AddAsync(newHistoric).ConfigureAwait(false);
            context.FileContents.Remove(loopToHistoric);
        }

        await context.SaveChangesAsync(true).ConfigureAwait(false);

        progress?.Report($"{toHistoric.First().Title} Deleted");

        DataNotifications.PublishDataNotification("Db", DataNotificationContentType.File,
            DataNotificationUpdateType.Delete, contentIdsToDelete);
    }

    /// <summary>
    ///     Deletes a Content Entry writing Historic Content, showing progress and publishing Data Notifications.
    ///     In general use this rather deleting content directly...
    /// </summary>
    /// <returns></returns>
    public static async Task DeleteGeoJsonContent(Guid contentId, IProgress<string>? progress = null)
    {
        var context = await Context().ConfigureAwait(false);

        var toHistoric = await context.GeoJsonContents.Where(x => x.ContentId == contentId).ToListAsync()
            .ConfigureAwait(false);

        if (!toHistoric.Any())
        {
            DataNotifications.PublishDataNotification("Db", DataNotificationContentType.GeoJson,
                DataNotificationUpdateType.Delete, contentId.AsList());
            return;
        }

        progress?.Report($"Writing {toHistoric.First().Title} Last Historic Entry");

        var contentIdsToDelete = toHistoric.Select(x => x.ContentId).ToList();

        foreach (var loopToHistoric in toHistoric)
        {
            var newHistoric = new HistoricGeoJsonContent();
            newHistoric.InjectFrom(loopToHistoric);
            newHistoric.Id = 0;
            newHistoric.LastUpdatedOn = DateTime.Now;
            if (string.IsNullOrWhiteSpace(newHistoric.LastUpdatedBy))
                newHistoric.LastUpdatedBy = "Historic Entry Archivist";
            await context.HistoricGeoJsonContents.AddAsync(newHistoric).ConfigureAwait(false);
            context.GeoJsonContents.Remove(loopToHistoric);
        }

        await context.SaveChangesAsync(true).ConfigureAwait(false);

        progress?.Report($"{toHistoric.First().Title} Deleted");

        DataNotifications.PublishDataNotification("Db", DataNotificationContentType.GeoJson,
            DataNotificationUpdateType.Delete, contentIdsToDelete);
    }

    /// <summary>
    ///     Deletes a Content Entry writing Historic Content, showing progress and publishing Data Notifications.
    ///     In general use this rather deleting content directly...
    /// </summary>
    /// <returns></returns>
    public static async Task DeleteImageContent(Guid contentId, IProgress<string>? progress = null)
    {
        var context = await Context().ConfigureAwait(false);

        var toHistoric = await context.ImageContents.Where(x => x.ContentId == contentId).ToListAsync()
            .ConfigureAwait(false);

        if (!toHistoric.Any())
        {
            DataNotifications.PublishDataNotification("Db", DataNotificationContentType.Image,
                DataNotificationUpdateType.Delete, contentId.AsList());
            return;
        }

        progress?.Report($"Writing {toHistoric.First().Title} Last Historic Entry");

        var contentIdsToDelete = toHistoric.Select(x => x.ContentId).ToList();

        foreach (var loopToHistoric in toHistoric)
        {
            var newHistoric = new HistoricImageContent();
            newHistoric.InjectFrom(loopToHistoric);
            newHistoric.Id = 0;
            newHistoric.LastUpdatedOn = DateTime.Now;
            if (string.IsNullOrWhiteSpace(newHistoric.LastUpdatedBy))
                newHistoric.LastUpdatedBy = "Historic Entry Archivist";
            await context.HistoricImageContents.AddAsync(newHistoric).ConfigureAwait(false);
            context.ImageContents.Remove(loopToHistoric);
        }

        await context.SaveChangesAsync(true).ConfigureAwait(false);

        progress?.Report($"{toHistoric.First().Title} Deleted");

        DataNotifications.PublishDataNotification("Db", DataNotificationContentType.Image,
            DataNotificationUpdateType.Delete, contentIdsToDelete);
    }

    /// <summary>
    ///     Deletes a Content Entry writing Historic Content, showing progress and publishing Data Notifications.
    ///     In general use this rather deleting content directly...
    /// </summary>
    /// <returns></returns>
    public static async Task DeleteLineContent(Guid contentId, IProgress<string>? progress = null)
    {
        var context = await Context().ConfigureAwait(false);

        var toHistoric = await context.LineContents.Where(x => x.ContentId == contentId).ToListAsync()
            .ConfigureAwait(false);

        if (!toHistoric.Any())
        {
            DataNotifications.PublishDataNotification("Db", DataNotificationContentType.Line,
                DataNotificationUpdateType.Delete, contentId.AsList());
            return;
        }

        progress?.Report($"Writing {toHistoric.First().Title} Last Historic Entry");

        var contentIdsToDelete = toHistoric.Select(x => x.ContentId).ToList();

        foreach (var loopToHistoric in toHistoric)
        {
            var newHistoric = new HistoricLineContent();
            newHistoric.InjectFrom(loopToHistoric);
            newHistoric.Id = 0;
            newHistoric.LastUpdatedOn = DateTime.Now;
            if (string.IsNullOrWhiteSpace(newHistoric.LastUpdatedBy))
                newHistoric.LastUpdatedBy = "Historic Entry Archivist";
            await context.HistoricLineContents.AddAsync(newHistoric).ConfigureAwait(false);
            context.LineContents.Remove(loopToHistoric);
        }

        await context.SaveChangesAsync(true).ConfigureAwait(false);

        progress?.Report($"{toHistoric.First().Title} Deleted");

        DataNotifications.PublishDataNotification("Db", DataNotificationContentType.Line,
            DataNotificationUpdateType.Delete, contentIdsToDelete);
    }

    /// <summary>
    ///     Deletes a Content Entry writing Historic Content, showing progress and publishing Data Notifications.
    ///     In general use this rather deleting content directly...
    /// </summary>
    /// <returns></returns>
    public static async Task DeleteLinkContent(Guid contentId, IProgress<string>? progress = null)
    {
        var context = await Context().ConfigureAwait(false);

        var toHistoric = await context.LinkContents.Where(x => x.ContentId == contentId).ToListAsync()
            .ConfigureAwait(false);

        if (!toHistoric.Any())
            DataNotifications.PublishDataNotification("Db", DataNotificationContentType.Link,
                DataNotificationUpdateType.Delete, contentId.AsList());

        progress?.Report($"Writing {toHistoric.First().Title} Last Historic Entry");

        var contentIdsToDelete = toHistoric.Select(x => x.ContentId).ToList();

        foreach (var loopToHistoric in toHistoric)
        {
            var newHistoric = new HistoricLinkContent();
            newHistoric.InjectFrom(loopToHistoric);
            newHistoric.Id = 0;
            newHistoric.LastUpdatedOn = DateTime.Now;
            if (string.IsNullOrWhiteSpace(newHistoric.LastUpdatedBy))
                newHistoric.LastUpdatedBy = "Historic Entry Archivist";
            await context.HistoricLinkContents.AddAsync(newHistoric).ConfigureAwait(false);
            context.LinkContents.Remove(loopToHistoric);
        }

        await context.SaveChangesAsync(true).ConfigureAwait(false);

        progress?.Report($"{toHistoric.First().Title} Deleted");

        DataNotifications.PublishDataNotification("Db", DataNotificationContentType.Link,
            DataNotificationUpdateType.Delete, contentIdsToDelete);
    }

    /// <summary>
    ///     Deletes a Content Entry writing Historic Content, showing progress and publishing Data Notifications.
    ///     In general use this rather deleting content directly...
    /// </summary>
    /// <returns></returns>
    public static async Task DeleteMapComponent(Guid contentId, IProgress<string>? progress = null)
    {
        var context = await Context().ConfigureAwait(false);

        var lastUpdatedOnForHistoric = DateTime.Now;

        var toHistoric = await context.MapComponents.Where(x => x.ContentId == contentId).ToListAsync()
            .ConfigureAwait(false);

        if (!toHistoric.Any())
        {
            DataNotifications.PublishDataNotification("Db", DataNotificationContentType.Map,
                DataNotificationUpdateType.Delete, contentId.AsList());

            return;
        }

        progress?.Report($"Writing {toHistoric.First().Title} Last Historic Entry");

        var contentIdsToDelete = toHistoric.Select(x => x.ContentId).ToList();

        foreach (var loopToHistoric in toHistoric)
        {
            var newHistoric = new HistoricMapComponent();
            newHistoric.InjectFrom(loopToHistoric);
            newHistoric.Id = 0;
            newHistoric.LastUpdatedOn = lastUpdatedOnForHistoric;
            if (string.IsNullOrWhiteSpace(newHistoric.LastUpdatedBy))
                newHistoric.LastUpdatedBy = "Historic Entry Archivist";

            var elements = await context.MapComponentElements
                .Where(x => x.MapComponentContentId == loopToHistoric.ContentId).ToListAsync().ConfigureAwait(false);
            newHistoric.Elements = JsonSerializer.Serialize(elements);

            await context.HistoricMapComponents.AddAsync(newHistoric).ConfigureAwait(false);
            context.MapComponents.Remove(loopToHistoric);
        }

        var elementsToDelete = await context.MapComponentElements.Where(x => x.MapComponentContentId == contentId)
            .ToListAsync().ConfigureAwait(false);
        var elementsToDeleteContentIds = elementsToDelete.Select(x => x.ElementContentId).ToList();

        foreach (var loopElements in elementsToDelete)
        {
            await context.HistoricMapComponentElements.AddAsync(new HistoricMapElement
            {
                ElementContentId = loopElements.ElementContentId,
                ShowDetailsDefault = loopElements.ShowDetailsDefault,
                IncludeInDefaultView = loopElements.IncludeInDefaultView,
                LastUpdateOn = lastUpdatedOnForHistoric,
                MapComponentContentId = loopElements.MapComponentContentId,
                LinksTo = loopElements.LinksTo
            }).ConfigureAwait(false);

            context.MapComponentElements.Remove(loopElements);
        }

        await context.SaveChangesAsync().ConfigureAwait(false);

        progress?.Report($"{toHistoric.First().Title} Deleted");

        DataNotifications.PublishDataNotification("Db", DataNotificationContentType.Map,
            DataNotificationUpdateType.Delete, contentIdsToDelete);

        if (elementsToDeleteContentIds.Any())
            DataNotifications.PublishDataNotification("Db", DataNotificationContentType.MapElement,
                DataNotificationUpdateType.Delete, elementsToDeleteContentIds);
    }

    /// <summary>
    ///     Deletes a Content Entry writing Historic Content, showing progress and publishing Data Notifications.
    ///     In general use this rather deleting content directly...
    /// </summary>
    /// <returns></returns>
    public static async Task DeleteNoteContent(Guid contentId, IProgress<string>? progress = null)
    {
        var context = await Context().ConfigureAwait(false);

        var toHistoric = await context.NoteContents.Where(x => x.ContentId == contentId).ToListAsync()
            .ConfigureAwait(false);

        if (!toHistoric.Any())
        {
            DataNotifications.PublishDataNotification("Db", DataNotificationContentType.Note,
                DataNotificationUpdateType.Delete, contentId.AsList());
            return;
        }

        progress?.Report($"Writing {toHistoric.First().Title} Last Historic Entry");

        var contentIdsToDelete = toHistoric.Select(x => x.ContentId).ToList();

        foreach (var loopToHistoric in toHistoric)
        {
            var newHistoric = new HistoricNoteContent();
            newHistoric.InjectFrom(loopToHistoric);
            newHistoric.Id = 0;
            newHistoric.LastUpdatedOn = DateTime.Now;
            if (string.IsNullOrWhiteSpace(newHistoric.LastUpdatedBy))
                newHistoric.LastUpdatedBy = "Historic Entry Archivist";
            await context.HistoricNoteContents.AddAsync(newHistoric).ConfigureAwait(false);
            context.NoteContents.Remove(loopToHistoric);
        }

        await context.SaveChangesAsync(true).ConfigureAwait(false);

        progress?.Report($"{toHistoric.First().Title} Deleted");

        DataNotifications.PublishDataNotification("Db", DataNotificationContentType.Note,
            DataNotificationUpdateType.Delete, contentIdsToDelete);
    }

    /// <summary>
    ///     Deletes a Content Entry writing Historic Content, showing progress and publishing Data Notifications.
    ///     In general use this rather deleting content directly...
    /// </summary>
    /// <returns></returns>
    public static async Task DeletePhotoContent(Guid contentId, IProgress<string>? progress = null)
    {
        var context = await Context().ConfigureAwait(false);

        var toHistoric = await context.PhotoContents.Where(x => x.ContentId == contentId).ToListAsync()
            .ConfigureAwait(false);

        if (!toHistoric.Any())
        {
            DataNotifications.PublishDataNotification("Db", DataNotificationContentType.Photo,
                DataNotificationUpdateType.Delete, contentId.AsList());
            return;
        }

        progress?.Report($"Writing {toHistoric.First().Title} Last Historic Entry");

        var contentIdsToDelete = toHistoric.Select(x => x.ContentId).ToList();

        foreach (var loopToHistoric in toHistoric)
        {
            var newHistoric = new HistoricPhotoContent();
            newHistoric.InjectFrom(loopToHistoric);
            newHistoric.Id = 0;
            newHistoric.LastUpdatedOn = DateTime.Now;
            if (string.IsNullOrWhiteSpace(newHistoric.LastUpdatedBy))
                newHistoric.LastUpdatedBy = "Historic Entry Archivist";
            await context.HistoricPhotoContents.AddAsync(newHistoric).ConfigureAwait(false);
            context.PhotoContents.Remove(loopToHistoric);
        }

        await context.SaveChangesAsync(true).ConfigureAwait(false);

        progress?.Report($"{toHistoric.First().Title} Deleted");

        DataNotifications.PublishDataNotification("Db", DataNotificationContentType.Photo,
            DataNotificationUpdateType.Delete, contentIdsToDelete);
    }

    /// <summary>
    ///     Deletes a Content Entry writing Historic Content, showing progress and publishing Data Notifications.
    ///     In general use this rather deleting content directly...
    /// </summary>
    /// <returns></returns>
    public static async Task DeletePointContent(Guid contentId, IProgress<string>? progress = null)
    {
        var context = await Context().ConfigureAwait(false);

        var groupLastUpdateOn = DateTime.Now;
        var updateGroup = Guid.NewGuid();

        var toHistoric = await context.PointContents.Where(x => x.ContentId == contentId).ToListAsync()
            .ConfigureAwait(false);

        if (!toHistoric.Any())
        {
            DataNotifications.PublishDataNotification("Db", DataNotificationContentType.Point,
                DataNotificationUpdateType.Delete, contentId.AsList());
            return;
        }

        progress?.Report($"Writing {toHistoric.First().Title} Last Historic Entry");

        var contentIdsToDelete = toHistoric.Select(x => x.ContentId).ToList();

        foreach (var loopToHistoric in toHistoric)
        {
            var newHistoric = new HistoricPointContent();
            newHistoric.InjectFrom(loopToHistoric);
            newHistoric.Id = 0;
            newHistoric.LastUpdatedOn = DateTime.Now;
            if (string.IsNullOrWhiteSpace(newHistoric.LastUpdatedBy))
                newHistoric.LastUpdatedBy = "Historic Entry Archivist";

            var pointDetails = await context.PointDetails.Where(x => x.PointContentId == loopToHistoric.ContentId)
                .ToListAsync().ConfigureAwait(false);
            newHistoric.PointDetails = JsonSerializer.Serialize(pointDetails);

            await context.HistoricPointContents.AddAsync(newHistoric).ConfigureAwait(false);
            context.PointContents.Remove(loopToHistoric);
        }

        await context.SaveChangesAsync(true).ConfigureAwait(false);

        progress?.Report($"{toHistoric.First().Title} Deleted");

        var relatedDetails = await context.PointDetails.Where(x => x.PointContentId == contentId).ToListAsync()
            .ConfigureAwait(false);

        var relatedDetailsContentIdsToDelete = relatedDetails.Select(x => x.ContentId).ToList();

        foreach (var loopToHistoric in relatedDetails)
        {
            var newHistoric = new HistoricPointDetail();
            newHistoric.InjectFrom(loopToHistoric);
            newHistoric.Id = 0;
            newHistoric.LastUpdatedOn = groupLastUpdateOn;
            newHistoric.HistoricGroupId = updateGroup;
            await context.HistoricPointDetails.AddAsync(newHistoric).ConfigureAwait(false);
        }

        context.PointDetails.RemoveRange(relatedDetails);

        await context.SaveChangesAsync().ConfigureAwait(false);

        DataNotifications.PublishDataNotification("Db", DataNotificationContentType.Point,
            DataNotificationUpdateType.Delete, contentIdsToDelete);

        DataNotifications.PublishDataNotification("Db", DataNotificationContentType.PointDetail,
            DataNotificationUpdateType.Delete, relatedDetailsContentIdsToDelete);
    }

    /// <summary>
    ///     Deletes a Content Entry writing Historic Content, showing progress and publishing Data Notifications.
    ///     In general use this rather deleting content directly...
    /// </summary>
    /// <returns></returns>
    public static async Task DeletePostContent(Guid contentId, IProgress<string>? progress = null)
    {
        var context = await Context().ConfigureAwait(false);

        var toHistoric = await context.PostContents.Where(x => x.ContentId == contentId).ToListAsync()
            .ConfigureAwait(false);

        if (!toHistoric.Any())
        {
            DataNotifications.PublishDataNotification("Db", DataNotificationContentType.Post,
                DataNotificationUpdateType.Delete, contentId.AsList());
            return;
        }

        progress?.Report($"Writing {toHistoric.First().Title} Last Historic Entry");

        var contentIdsToDelete = toHistoric.Select(x => x.ContentId).ToList();

        foreach (var loopToHistoric in toHistoric)
        {
            var newHistoric = new HistoricPostContent();
            newHistoric.InjectFrom(loopToHistoric);
            newHistoric.Id = 0;
            newHistoric.LastUpdatedOn = DateTime.Now;
            if (string.IsNullOrWhiteSpace(newHistoric.LastUpdatedBy))
                newHistoric.LastUpdatedBy = "Historic Entry Archivist";
            await context.HistoricPostContents.AddAsync(newHistoric).ConfigureAwait(false);
            context.PostContents.Remove(loopToHistoric);
        }

        await context.SaveChangesAsync(true).ConfigureAwait(false);

        progress?.Report($"{toHistoric.First().Title} Deleted");

        DataNotifications.PublishDataNotification("Db", DataNotificationContentType.Post,
            DataNotificationUpdateType.Delete, contentIdsToDelete);
    }

    /// <summary>
    ///     Deletes a Content Entry writing Historic Content, showing progress and publishing Data Notifications.
    ///     In general use this rather deleting content directly...
    /// </summary>
    /// <returns></returns>
    public static async Task DeleteSnippet(Guid contentId, IProgress<string>? progress = null)
    {
        var context = await Context().ConfigureAwait(false);

        var toHistoric = await context.Snippets.Where(x => x.ContentId == contentId).ToListAsync()
            .ConfigureAwait(false);

        if (!toHistoric.Any())
        {
            DataNotifications.PublishDataNotification("Db", DataNotificationContentType.Snippet,
                DataNotificationUpdateType.Delete, contentId.AsList());
            return;
        }

        progress?.Report($"Writing {toHistoric.First().Title} Last Historic Entry");

        var contentIdsToDelete = toHistoric.Select(x => x.ContentId).ToList();

        foreach (var loopToHistoric in toHistoric)
        {
            var newHistoric = new HistoricSnippet
            {
                ContentId = loopToHistoric.ContentId,
                ContentVersion = loopToHistoric.ContentVersion,
                CreatedOn = loopToHistoric.CreatedOn
            };
            newHistoric.InjectFrom(loopToHistoric);
            newHistoric.Id = 0;
            newHistoric.LastUpdatedOn = DateTime.Now;
            if (string.IsNullOrWhiteSpace(newHistoric.LastUpdatedBy))
                newHistoric.LastUpdatedBy = "Historic Entry Archivist";
            await context.HistoricSnippets.AddAsync(newHistoric).ConfigureAwait(false);
            context.Snippets.Remove(loopToHistoric);
        }

        await context.SaveChangesAsync(true).ConfigureAwait(false);

        progress?.Report($"{toHistoric.First().Title} Deleted");

        DataNotifications.PublishDataNotification("Db", DataNotificationContentType.Snippet,
            DataNotificationUpdateType.Delete, contentIdsToDelete);
    }

    /// <summary>
    ///     Deletes a Tag Exclusion and publishes Data Notifications.
    ///     In general use this rather deleting content directly...
    /// </summary>
    /// <returns></returns>
    public static async Task DeleteTagExclusion(int tagExclusionDbEntryId, IProgress<string>? progress = null)
    {
        var context = await Context().ConfigureAwait(false);

        var toDelete = context.TagExclusions.Single(x => x.Id == tagExclusionDbEntryId);

        context.TagExclusions.Remove(toDelete);

        await context.SaveChangesAsync(true).ConfigureAwait(false);

        progress?.Report($"Tag Exclusion {toDelete.Tag} Deleted");

        DataNotifications.PublishDataNotification("Db", DataNotificationContentType.TagExclusion,
            DataNotificationUpdateType.Delete, null);
    }

    /// <summary>
    ///     Deletes a Content Entry writing Historic Content, showing progress and publishing Data Notifications.
    ///     In general use this rather deleting content directly...
    /// </summary>
    /// <returns></returns>
    public static async Task DeleteTrailContent(Guid contentId, IProgress<string>? progress = null)
    {
        var context = await Context().ConfigureAwait(false);

        var toHistoric = await context.TrailContents.Where(x => x.ContentId == contentId).ToListAsync()
            .ConfigureAwait(false);

        if (!toHistoric.Any())
        {
            DataNotifications.PublishDataNotification("Db", DataNotificationContentType.Trail,
                DataNotificationUpdateType.Delete, contentId.AsList());
            return;
        }

        progress?.Report($"Writing {toHistoric.First().Title} Last Historic Entry");

        var contentIdsToDelete = toHistoric.Select(x => x.ContentId).ToList();

        foreach (var loopToHistoric in toHistoric)
        {
            var newHistoric = new HistoricTrailContent();
            newHistoric.InjectFrom(loopToHistoric);
            newHistoric.Id = 0;
            newHistoric.LastUpdatedOn = DateTime.Now;
            if (string.IsNullOrWhiteSpace(newHistoric.LastUpdatedBy))
                newHistoric.LastUpdatedBy = "Historic Entry Archivist";
            await context.HistoricTrailContents.AddAsync(newHistoric).ConfigureAwait(false);
            context.TrailContents.Remove(loopToHistoric);
        }

        await context.SaveChangesAsync(true).ConfigureAwait(false);

        progress?.Report($"{toHistoric.First().Title} Deleted");

        DataNotifications.PublishDataNotification("Db", DataNotificationContentType.Trail,
            DataNotificationUpdateType.Delete, contentIdsToDelete);
    }

    /// <summary>
    ///     Deletes a Content Entry writing Historic Content, showing progress and publishing Data Notifications.
    ///     In general use this rather deleting content directly...
    /// </summary>
    /// <returns></returns>
    public static async Task DeleteVideoContent(Guid contentId, IProgress<string>? progress = null)
    {
        var context = await Context().ConfigureAwait(false);

        var toHistoric = await context.VideoContents.Where(x => x.ContentId == contentId).ToListAsync()
            .ConfigureAwait(false);

        if (!toHistoric.Any())
        {
            DataNotifications.PublishDataNotification("Db", DataNotificationContentType.Video,
                DataNotificationUpdateType.Delete, contentId.AsList());
            return;
        }

        progress?.Report($"Writing {toHistoric.First().Title} Last Historic Entry");

        var contentIdsToDelete = toHistoric.Select(x => x.ContentId).ToList();

        foreach (var loopToHistoric in toHistoric)
        {
            var newHistoric = new HistoricVideoContent();
            newHistoric.InjectFrom(loopToHistoric);
            newHistoric.Id = 0;
            newHistoric.LastUpdatedOn = DateTime.Now;
            if (string.IsNullOrWhiteSpace(newHistoric.LastUpdatedBy))
                newHistoric.LastUpdatedBy = "Historic Entry Archivist";
            await context.HistoricVideoContents.AddAsync(newHistoric).ConfigureAwait(false);
            context.VideoContents.Remove(loopToHistoric);
        }

        await context.SaveChangesAsync(true).ConfigureAwait(false);

        progress?.Report($"{toHistoric.First().Title} Deleted");

        DataNotifications.PublishDataNotification("Db", DataNotificationContentType.Video,
            DataNotificationUpdateType.Delete, contentIdsToDelete);
    }

    public static async Task<List<FileContent>> FileContentFromBoundingBox(this PointlessWaymarksContext db,
        SpatialBounds bounds)
    {
        var returnList = new List<FileContent>();

        returnList.AddRange(await db.FileContents.Where(x =>
            x.Latitude != null && x.Longitude != null
                               && x.Latitude >= bounds.MinLatitude
                               && x.Latitude <= bounds.MaxLatitude
                               && x.Longitude >= bounds.MinLongitude
                               && x.Longitude <= bounds.MaxLongitude).ToListAsync());

        return returnList;
    }

    /// <summary>
    ///     Takes in a Content Type that has a Folder (note Links do not have Folders) and returns a list of ALL folders
    ///     currently in the database for that Content Type (ie pass in a Post and get back a list of all folders used in
    ///     all Posts - can be used to get choices for existing folders).
    /// </summary>
    /// <param name="content"></param>
    /// <returns></returns>
    public static async Task<List<string>> FolderNamesFromContent(dynamic? content)
    {
        var db = await Context().ConfigureAwait(false);

        return content switch
        {
            FileContent => await db.FileContents.Where(x => !string.IsNullOrWhiteSpace(x.Folder)).Select(x => x.Folder)
                .Distinct().OrderBy(x => x).Cast<string>().ToListAsync(),
            GeoJsonContent => await db.GeoJsonContents.Where(x => !string.IsNullOrWhiteSpace(x.Folder))
                .Select(x => x.Folder)
                .Distinct().OrderBy(x => x).Cast<string>().ToListAsync(),
            ImageContent => await db.ImageContents.Where(x => !string.IsNullOrWhiteSpace(x.Folder))
                .Select(x => x.Folder)
                .Distinct().OrderBy(x => x).Cast<string>().ToListAsync(),
            LineContent => await db.LineContents.Where(x => !string.IsNullOrWhiteSpace(x.Folder)).Select(x => x.Folder)
                .Distinct().OrderBy(x => x).Cast<string>().ToListAsync(),
            NoteContent => await db.NoteContents.Where(x => !string.IsNullOrWhiteSpace(x.Folder)).Select(x => x.Folder)
                .Distinct().OrderBy(x => x).Cast<string>().ToListAsync(),
            PhotoContent => await db.PhotoContents.Where(x => !string.IsNullOrWhiteSpace(x.Folder))
                .Select(x => x.Folder)
                .Distinct().OrderBy(x => x).Cast<string>().ToListAsync(),
            PointContent => await db.PointContents.Where(x => !string.IsNullOrWhiteSpace(x.Folder))
                .Select(x => x.Folder)
                .Distinct().OrderBy(x => x).Cast<string>().ToListAsync(),
            Models.PointContentDto => await db.PointContents.Where(x => !string.IsNullOrWhiteSpace(x.Folder))
                .Select(x => x.Folder)
                .Distinct().OrderBy(x => x).Cast<string>().ToListAsync(),
            PostContent => await db.PostContents.Where(x => !string.IsNullOrWhiteSpace(x.Folder)).Select(x => x.Folder)
                .Distinct().OrderBy(x => x).Cast<string>().ToListAsync(),
            TrailContent => await db.TrailContents.Where(x => !string.IsNullOrWhiteSpace(x.Folder)).Select(x => x.Folder)
                .Distinct().OrderBy(x => x).Cast<string>().ToListAsync(),
            VideoContent => await db.VideoContents.Where(x => !string.IsNullOrWhiteSpace(x.Folder)).Select(x => x.Folder)
                .Distinct().OrderBy(x => x).Cast<string>().ToListAsync(),
            _ => []
        };
    }

    /// <summary>
    ///     Returns the folders currently in use by Geo Content - Points, Lines and Geojson.
    /// </summary>
    /// <returns></returns>
    public static async Task<List<string>> FolderNamesFromGeoContent()
    {
        var db = await Context().ConfigureAwait(false);

        var compiledList = new List<string>();

        compiledList.AddRange(await db.GeoJsonContents.Where(x => !string.IsNullOrWhiteSpace(x.Folder))
            .Select(x => x.Folder)
            .Distinct().Cast<string>().ToListAsync());

        compiledList.AddRange(await db.LineContents.Where(x => !string.IsNullOrWhiteSpace(x.Folder))
            .Select(x => x.Folder)
            .Distinct().Cast<string>().ToListAsync());

        compiledList.AddRange(await db.PointContents.Where(x => !string.IsNullOrWhiteSpace(x.Folder))
            .Select(x => x.Folder)
            .Distinct().Cast<string>().ToListAsync());

        compiledList = compiledList.Distinct().OrderBy(x => x).ToList();

        return compiledList;
    }

    public static bool GeoJsonBoundingBoxOverlaps(GeoJsonContent content, SpatialBounds bounds)
    {
        return (
                   (bounds.MinLatitude >= content.InitialViewBoundsMinLatitude
                    && bounds.MinLatitude <= content.InitialViewBoundsMaxLatitude)
                   || (bounds.MaxLatitude >= content.InitialViewBoundsMinLatitude
                       && bounds.MaxLatitude <= content.InitialViewBoundsMaxLatitude)
                   || (bounds.MinLatitude <= content.InitialViewBoundsMinLatitude &&
                       bounds.MaxLatitude >= content.InitialViewBoundsMaxLatitude)
               )
               &&
               (
                   (bounds.MinLongitude >= content.InitialViewBoundsMinLongitude
                    && bounds.MinLongitude <= content.InitialViewBoundsMaxLongitude)
                   || (bounds.MaxLongitude >= content.InitialViewBoundsMinLongitude
                       && bounds.MaxLongitude <= content.InitialViewBoundsMaxLongitude)
                   || (bounds.MinLongitude <= content.InitialViewBoundsMinLongitude &&
                       bounds.MaxLongitude >= content.InitialViewBoundsMaxLongitude)
               );
    }

    public static async Task<List<GeoJsonContent>> GeoJsonContentFromBoundingBox(this PointlessWaymarksContext db,
        SpatialBounds bounds)
    {
        var returnList = new List<GeoJsonContent>();

        returnList.AddRange(await db.GeoJsonContents.Where(x =>
            (
                (bounds.MinLatitude >= x.InitialViewBoundsMinLatitude
                 && bounds.MinLatitude <= x.InitialViewBoundsMaxLatitude)
                || (bounds.MaxLatitude >= x.InitialViewBoundsMinLatitude
                    && bounds.MaxLatitude <= x.InitialViewBoundsMaxLatitude)
                || (bounds.MinLatitude <= x.InitialViewBoundsMinLatitude &&
                    bounds.MaxLatitude >= x.InitialViewBoundsMaxLatitude)
            )
            &&
            (
                (bounds.MinLongitude >= x.InitialViewBoundsMinLongitude
                 && bounds.MinLongitude <= x.InitialViewBoundsMaxLongitude)
                || (bounds.MaxLongitude >= x.InitialViewBoundsMinLongitude
                    && bounds.MaxLongitude <= x.InitialViewBoundsMaxLongitude)
                || (bounds.MinLongitude <= x.InitialViewBoundsMinLongitude &&
                    bounds.MaxLongitude >= x.InitialViewBoundsMaxLongitude)
            )
        ).ToListAsync());

        return returnList;
    }

    public static async Task<List<Guid>> GeoJsonContentIdsFromBoundingBox(this PointlessWaymarksContext db,
        SpatialBounds bounds)
    {
        var returnList = new List<Guid>();

        returnList.AddRange(await db.GeoJsonContents.Where(x =>
            (
                (bounds.MinLatitude >= x.InitialViewBoundsMinLatitude
                 && bounds.MinLatitude <= x.InitialViewBoundsMaxLatitude)
                || (bounds.MaxLatitude >= x.InitialViewBoundsMinLatitude
                    && bounds.MaxLatitude <= x.InitialViewBoundsMaxLatitude)
                || (bounds.MinLatitude <= x.InitialViewBoundsMinLatitude &&
                    bounds.MaxLatitude >= x.InitialViewBoundsMaxLatitude)
            )
            &&
            (
                (bounds.MinLongitude >= x.InitialViewBoundsMinLongitude
                 && bounds.MinLongitude <= x.InitialViewBoundsMaxLongitude)
                || (bounds.MaxLongitude >= x.InitialViewBoundsMinLongitude
                    && bounds.MaxLongitude <= x.InitialViewBoundsMaxLongitude)
                || (bounds.MinLongitude <= x.InitialViewBoundsMinLongitude &&
                    bounds.MaxLongitude >= x.InitialViewBoundsMaxLongitude)
            )
        ).Select(x => x.ContentId).ToListAsync());

        return returnList;
    }

    /// <summary>
    ///     Returns up to the specified number of historic details for a Point.
    /// </summary>
    /// <param name="pointContentId"></param>
    /// <param name="db"></param>
    /// <param name="entriesToReturn"></param>
    /// <returns></returns>
    public static async Task<List<HistoricPointDetail>> HistoricPointDetailsForPoint(Guid pointContentId,
        PointlessWaymarksContext db, int entriesToReturn)
    {
        return await db.HistoricPointDetails.Where(x => x.PointContentId == pointContentId).Take(entriesToReturn)
            .ToListAsync().ConfigureAwait(false);
    }

    public static async Task<List<ImageContent>> ImageContentFromBoundingBox(this PointlessWaymarksContext db,
        SpatialBounds bounds)
    {
        var returnList = new List<ImageContent>();

        returnList.AddRange(await db.ImageContents.Where(x =>
            x.Latitude != null && x.Longitude != null
                               && x.Latitude >= bounds.MinLatitude
                               && x.Latitude <= bounds.MaxLatitude
                               && x.Longitude >= bounds.MinLongitude
                               && x.Longitude <= bounds.MaxLongitude).ToListAsync());

        return returnList;
    }

    public static bool LineContentBoundingBoxOverlaps(LineContent content, SpatialBounds bounds)
    {
        return (
                   (bounds.MinLatitude >= content.InitialViewBoundsMinLatitude
                    && bounds.MinLatitude <= content.InitialViewBoundsMaxLatitude)
                   || (bounds.MaxLatitude >= content.InitialViewBoundsMinLatitude
                       && bounds.MaxLatitude <= content.InitialViewBoundsMaxLatitude)
                   || (bounds.MinLatitude <= content.InitialViewBoundsMinLatitude &&
                       bounds.MaxLatitude >= content.InitialViewBoundsMaxLatitude)
               )
               &&
               (
                   (bounds.MinLongitude >= content.InitialViewBoundsMinLongitude
                    && bounds.MinLongitude <= content.InitialViewBoundsMaxLongitude)
                   || (bounds.MaxLongitude >= content.InitialViewBoundsMinLongitude
                       && bounds.MaxLongitude <= content.InitialViewBoundsMaxLongitude)
                   || (bounds.MinLongitude <= content.InitialViewBoundsMinLongitude &&
                       bounds.MaxLongitude >= content.InitialViewBoundsMaxLongitude)
               );
    }

    public static IQueryable<LineContent> LineContentFilteredForActivities(this IQueryable<LineContent> lineContent)
    {
        return lineContent.Where(x => x.RecordingStartedOn != null
                                      && x.RecordingEndedOn != null
                                      && x.RecordingStartedOn < x.RecordingEndedOn
                                      && !x.IsDraft);
    }

    public static async Task<List<LineContent>> LineContentFromBoundingBox(this PointlessWaymarksContext db,
        SpatialBounds bounds)
    {
        var returnList = new List<LineContent>();

        returnList.AddRange(await db.LineContents.Where(x =>
            (
                (bounds.MinLatitude >= x.InitialViewBoundsMinLatitude
                 && bounds.MinLatitude <= x.InitialViewBoundsMaxLatitude)
                || (bounds.MaxLatitude >= x.InitialViewBoundsMinLatitude
                    && bounds.MaxLatitude <= x.InitialViewBoundsMaxLatitude)
                || (bounds.MinLatitude <= x.InitialViewBoundsMinLatitude &&
                    bounds.MaxLatitude >= x.InitialViewBoundsMaxLatitude)
            )
            &&
            (
                (bounds.MinLongitude >= x.InitialViewBoundsMinLongitude
                 && bounds.MinLongitude <= x.InitialViewBoundsMaxLongitude)
                || (bounds.MaxLongitude >= x.InitialViewBoundsMinLongitude
                    && bounds.MaxLongitude <= x.InitialViewBoundsMaxLongitude)
                || (bounds.MinLongitude <= x.InitialViewBoundsMinLongitude &&
                    bounds.MaxLongitude >= x.InitialViewBoundsMaxLongitude)
            )
        ).ToListAsync());

        return returnList;
    }

    public static async Task<List<Guid>> LineContentIdsFromBoundingBox(this PointlessWaymarksContext db,
        SpatialBounds bounds)
    {
        var returnList = new List<Guid>();

        returnList.AddRange(await db.LineContents.Where(x =>
            (
                (bounds.MinLatitude >= x.InitialViewBoundsMinLatitude
                 && bounds.MinLatitude <= x.InitialViewBoundsMaxLatitude)
                || (bounds.MaxLatitude >= x.InitialViewBoundsMinLatitude
                    && bounds.MaxLatitude <= x.InitialViewBoundsMaxLatitude)
                || (bounds.MinLatitude <= x.InitialViewBoundsMinLatitude &&
                    bounds.MaxLatitude >= x.InitialViewBoundsMaxLatitude)
            )
            &&
            (
                (bounds.MinLongitude >= x.InitialViewBoundsMinLongitude
                 && bounds.MinLongitude <= x.InitialViewBoundsMaxLongitude)
                || (bounds.MaxLongitude >= x.InitialViewBoundsMinLongitude
                    && bounds.MaxLongitude <= x.InitialViewBoundsMaxLongitude)
                || (bounds.MinLongitude <= x.InitialViewBoundsMinLongitude &&
                    bounds.MaxLongitude >= x.InitialViewBoundsMaxLongitude)
            )
        ).Select(x => x.ContentId).ToListAsync());

        return returnList;
    }

    /// <summary>
    ///     Queries the 'Contents' Tables for entries marked ShowInMainSiteFeed, not IsDraft and FeedOn less that now returning
    ///     them as an IContentCommon.
    /// </summary>
    /// <returns></returns>
    public static async Task<List<IContentCommon>> MainFeedCommonContent()
    {
        var db = await Context().ConfigureAwait(false);

        var nowCutoff = DateTime.Now;

        //!!Content List
        var fileContent = await db.FileContents.Where(x => x.ShowInMainSiteFeed && !x.IsDraft && x.FeedOn < nowCutoff)
            .Cast<IContentCommon>().ToListAsync().ConfigureAwait(false);
        var geoJsonContent = await db.GeoJsonContents
            .Where(x => x.ShowInMainSiteFeed && !x.IsDraft && x.FeedOn < nowCutoff).Cast<IContentCommon>().ToListAsync()
            .ConfigureAwait(false);
        var imageContent = await db.ImageContents.Where(x => x.ShowInMainSiteFeed && !x.IsDraft && x.FeedOn < nowCutoff)
            .Cast<IContentCommon>().ToListAsync().ConfigureAwait(false);
        var lineContent = await db.LineContents.Where(x => x.ShowInMainSiteFeed && !x.IsDraft && x.FeedOn < nowCutoff)
            .Cast<IContentCommon>().ToListAsync().ConfigureAwait(false);
        var noteContent = await db.NoteContents.Where(x => x.ShowInMainSiteFeed && !x.IsDraft && x.FeedOn < nowCutoff)
            .Cast<IContentCommon>().ToListAsync().ConfigureAwait(false);
        var photoContent = await db.PhotoContents.Where(x => x.ShowInMainSiteFeed && !x.IsDraft && x.FeedOn < nowCutoff)
            .Cast<IContentCommon>().ToListAsync().ConfigureAwait(false);
        var pointContent = await db.PointContents.Where(x => x.ShowInMainSiteFeed && !x.IsDraft && x.FeedOn < nowCutoff)
            .Cast<IContentCommon>().ToListAsync().ConfigureAwait(false);
        var postContent = await db.PostContents.Where(x => x.ShowInMainSiteFeed && !x.IsDraft && x.FeedOn < nowCutoff)
            .Cast<IContentCommon>().ToListAsync().ConfigureAwait(false);
        var trailContent = await db.TrailContents.Where(x => x.ShowInMainSiteFeed && !x.IsDraft && x.FeedOn < nowCutoff)
            .Cast<IContentCommon>().ToListAsync().ConfigureAwait(false);
        var videoContent = await db.VideoContents.Where(x => x.ShowInMainSiteFeed && !x.IsDraft && x.FeedOn < nowCutoff)
            .Cast<IContentCommon>().ToListAsync().ConfigureAwait(false);

        return fileContent.Concat(geoJsonContent).Concat(imageContent).Concat(lineContent).Concat(noteContent)
            .Concat(postContent).Concat(photoContent).Concat(pointContent).Concat(trailContent).Concat(videoContent)
            .OrderByDescending(x => x.FeedOn).ToList();
    }

    /// <summary>
    ///     Queries the 'Contents' Tables for entries marked ShowInMainSiteFeed, not IsDraft and having a FeedOn date after
    ///     the input datetime and before now and the returns the most recent entries.
    /// </summary>
    /// <param name="after"></param>
    /// <param name="numberOfEntries"></param>
    /// <returns></returns>
    public static async Task<List<IContentCommon>> MainFeedCommonContentAfter(DateTime after, int numberOfEntries)
    {
        var db = await Context().ConfigureAwait(false);

        var nowCutoff = DateTime.Now;

        //!!Content List
        var fileContent = await db.FileContents
            .Where(x => x.ShowInMainSiteFeed && !x.IsDraft && x.FeedOn > after && x.FeedOn < nowCutoff)
            .OrderByDescending(x => x.FeedOn).Cast<IContentCommon>().ToListAsync().ConfigureAwait(false);
        var geoJsonContent = await db.GeoJsonContents
            .Where(x => x.ShowInMainSiteFeed && !x.IsDraft && x.FeedOn > after && x.FeedOn < nowCutoff)
            .OrderByDescending(x => x.FeedOn).Cast<IContentCommon>().ToListAsync().ConfigureAwait(false);
        var imageContent = await db.ImageContents
            .Where(x => x.ShowInMainSiteFeed && !x.IsDraft && x.FeedOn > after && x.FeedOn < nowCutoff)
            .OrderByDescending(x => x.FeedOn).Cast<IContentCommon>().ToListAsync().ConfigureAwait(false);
        var lineContent = await db.LineContents
            .Where(x => x.ShowInMainSiteFeed && !x.IsDraft && x.FeedOn > after && x.FeedOn < nowCutoff)
            .OrderByDescending(x => x.FeedOn).Cast<IContentCommon>().ToListAsync().ConfigureAwait(false);
        var noteContent = await db.NoteContents
            .Where(x => x.ShowInMainSiteFeed && !x.IsDraft && x.FeedOn > after && x.FeedOn < nowCutoff)
            .OrderByDescending(x => x.FeedOn).Cast<IContentCommon>().ToListAsync().ConfigureAwait(false);
        var photoContent = await db.PhotoContents
            .Where(x => x.ShowInMainSiteFeed && !x.IsDraft && x.FeedOn > after && x.FeedOn < nowCutoff)
            .OrderByDescending(x => x.FeedOn).Cast<IContentCommon>().ToListAsync().ConfigureAwait(false);
        var pointContent = await db.PointContents
            .Where(x => x.ShowInMainSiteFeed && !x.IsDraft && x.FeedOn > after && x.FeedOn < nowCutoff)
            .OrderByDescending(x => x.FeedOn).Cast<IContentCommon>().ToListAsync().ConfigureAwait(false);
        var postContent = await db.PostContents
            .Where(x => x.ShowInMainSiteFeed && !x.IsDraft && x.FeedOn > after && x.FeedOn < nowCutoff)
            .OrderByDescending(x => x.FeedOn).Cast<IContentCommon>().ToListAsync().ConfigureAwait(false);
        var trailContent = await db.TrailContents
            .Where(x => x.ShowInMainSiteFeed && !x.IsDraft && x.FeedOn > after && x.FeedOn < nowCutoff)
            .OrderByDescending(x => x.FeedOn).Cast<IContentCommon>().ToListAsync().ConfigureAwait(false);
        var videoContent = await db.VideoContents
            .Where(x => x.ShowInMainSiteFeed && !x.IsDraft && x.FeedOn > after && x.FeedOn < nowCutoff)
            .OrderByDescending(x => x.FeedOn).Cast<IContentCommon>().ToListAsync().ConfigureAwait(false);

        return fileContent.Concat(geoJsonContent).Concat(imageContent).Concat(lineContent).Concat(noteContent)
            .Concat(photoContent).Concat(postContent).Concat(pointContent).Concat(trailContent).Concat(videoContent).OrderBy(x => x.FeedOn)
            .Take(numberOfEntries)
            .ToList();
    }

    /// <summary>
    ///     Queries the 'Contents' Tables for entries marked ShowInMainSiteFeed, not IsDraft and having a FeedOn date before
    ///     the input datetime and before now and the returns the most recent entries.
    /// </summary>
    /// <param name="before"></param>
    /// <param name="numberOfEntries"></param>
    /// <returns></returns>
    public static async Task<List<IContentCommon>> MainFeedCommonContentBefore(DateTime before, int numberOfEntries)
    {
        var db = await Context().ConfigureAwait(false);

        var nowCutoff = DateTime.Now;

        //!!Content List
        var fileContent = await db.FileContents
            .Where(x => x.ShowInMainSiteFeed && !x.IsDraft && x.FeedOn < before && x.FeedOn < nowCutoff &&
                        x.FeedOn < nowCutoff).OrderByDescending(x => x.FeedOn).Cast<IContentCommon>().ToListAsync()
            .ConfigureAwait(false);
        var geoJsonContent = await db.GeoJsonContents
            .Where(x => x.ShowInMainSiteFeed && !x.IsDraft && x.FeedOn < before && x.FeedOn < nowCutoff)
            .OrderByDescending(x => x.FeedOn).Cast<IContentCommon>().ToListAsync().ConfigureAwait(false);
        var imageContent = await db.ImageContents
            .Where(x => x.ShowInMainSiteFeed && !x.IsDraft && x.FeedOn < before && x.FeedOn < nowCutoff)
            .OrderByDescending(x => x.FeedOn).Cast<IContentCommon>().ToListAsync().ConfigureAwait(false);
        var lineContent = await db.LineContents
            .Where(x => x.ShowInMainSiteFeed && !x.IsDraft && x.FeedOn < before && x.FeedOn < nowCutoff)
            .OrderByDescending(x => x.FeedOn).Cast<IContentCommon>().ToListAsync().ConfigureAwait(false);
        var noteContent = await db.NoteContents
            .Where(x => x.ShowInMainSiteFeed && !x.IsDraft && x.FeedOn < before && x.FeedOn < nowCutoff)
            .OrderByDescending(x => x.FeedOn).Cast<IContentCommon>().ToListAsync().ConfigureAwait(false);
        var photoContent = await db.PhotoContents
            .Where(x => x.ShowInMainSiteFeed && !x.IsDraft && x.FeedOn < before && x.FeedOn < nowCutoff)
            .OrderByDescending(x => x.FeedOn).Cast<IContentCommon>().ToListAsync().ConfigureAwait(false);
        var pointContent = await db.PointContents
            .Where(x => x.ShowInMainSiteFeed && !x.IsDraft && x.FeedOn < before && x.FeedOn < nowCutoff)
            .OrderByDescending(x => x.FeedOn).Cast<IContentCommon>().ToListAsync().ConfigureAwait(false);
        var postContent = await db.PostContents
            .Where(x => x.ShowInMainSiteFeed && !x.IsDraft && x.FeedOn < before && x.FeedOn < nowCutoff)
            .OrderByDescending(x => x.FeedOn).Cast<IContentCommon>().ToListAsync().ConfigureAwait(false);
        var trailContent = await db.TrailContents
            .Where(x => x.ShowInMainSiteFeed && !x.IsDraft && x.FeedOn < before && x.FeedOn < nowCutoff)
            .OrderByDescending(x => x.FeedOn).Cast<IContentCommon>().ToListAsync().ConfigureAwait(false);
        var videoContent = await db.VideoContents
            .Where(x => x.ShowInMainSiteFeed && !x.IsDraft && x.FeedOn < before && x.FeedOn < nowCutoff)
            .OrderByDescending(x => x.FeedOn).Cast<IContentCommon>().ToListAsync().ConfigureAwait(false);

        return fileContent.Concat(geoJsonContent).Concat(imageContent).Concat(lineContent).Concat(noteContent)
            .Concat(postContent).Concat(photoContent).Concat(pointContent).Concat(trailContent).Concat(videoContent)
            .OrderByDescending(x => x.FeedOn)
            .Take(numberOfEntries).ToList();
    }

    /// <summary>
    ///     Queries the 'Contents' Tables for entries marked ShowInMainSiteFeed, not IsDraft and before now
    ///     and the returns the most recent entries.
    /// </summary>
    /// <param name="topNumberOfEntries"></param>
    /// <returns></returns>
    public static async Task<List<dynamic>> MainFeedRecentDynamicContent(int topNumberOfEntries)
    {
        var db = await Context().ConfigureAwait(false);

        var nowCutoff = DateTime.Now;

        //!!Content List
        //Query the content and the dates first before returning the full content entry. This will result in an entry
        //being returned for every single content entry and this could be optimized - but in this system I think the
        //realistic total number of entries and low usage of this method makes it unlikely this is a problem.
        var fileContentDateList = await db.FileContents
            .Where(x => x.ShowInMainSiteFeed && !x.IsDraft && x.FeedOn < nowCutoff)
            .Select(x => new { x.ContentId, x.FeedOn }).ToListAsync().ConfigureAwait(false);
        var geoJsonContentDateList = await db.GeoJsonContents
            .Where(x => x.ShowInMainSiteFeed && !x.IsDraft && x.FeedOn < nowCutoff)
            .Select(x => new { x.ContentId, x.FeedOn }).ToListAsync().ConfigureAwait(false);
        var imageContentDateList = await db.ImageContents
            .Where(x => x.ShowInMainSiteFeed && !x.IsDraft && x.FeedOn < nowCutoff)
            .Select(x => new { x.ContentId, x.FeedOn }).ToListAsync().ConfigureAwait(false);
        var lineContentDateList = await db.LineContents
            .Where(x => x.ShowInMainSiteFeed && !x.IsDraft && x.FeedOn < nowCutoff)
            .Select(x => new { x.ContentId, x.FeedOn }).ToListAsync().ConfigureAwait(false);
        var noteContentDateList = await db.NoteContents
            .Where(x => x.ShowInMainSiteFeed && !x.IsDraft && x.FeedOn < nowCutoff)
            .Select(x => new { x.ContentId, x.FeedOn }).ToListAsync().ConfigureAwait(false);
        var photoContentDateList = await db.PhotoContents
            .Where(x => x.ShowInMainSiteFeed && !x.IsDraft && x.FeedOn < nowCutoff)
            .Select(x => new { x.ContentId, x.FeedOn }).ToListAsync().ConfigureAwait(false);
        var pointContentDateList = await db.PointContents
            .Where(x => x.ShowInMainSiteFeed && !x.IsDraft && x.FeedOn < nowCutoff)
            .Select(x => new { x.ContentId, x.FeedOn }).ToListAsync().ConfigureAwait(false);
        var postContentDateList = await db.PostContents
            .Where(x => x.ShowInMainSiteFeed && !x.IsDraft && x.FeedOn < nowCutoff)
            .Select(x => new { x.ContentId, x.FeedOn }).ToListAsync().ConfigureAwait(false);
        var trailContentDateList = await db.TrailContents
            .Where(x => x.ShowInMainSiteFeed && !x.IsDraft && x.FeedOn < nowCutoff)
            .Select(x => new { x.ContentId, x.FeedOn }).ToListAsync().ConfigureAwait(false);
        var videoContentDateList = await db.VideoContents
            .Where(x => x.ShowInMainSiteFeed && !x.IsDraft && x.FeedOn < nowCutoff)
            .Select(x => new { x.ContentId, x.FeedOn }).ToListAsync().ConfigureAwait(false);

        var contentIdListForFeed = fileContentDateList.Concat(geoJsonContentDateList).Concat(imageContentDateList)
            .Concat(lineContentDateList).Concat(noteContentDateList).Concat(photoContentDateList)
            .Concat(pointContentDateList).Concat(postContentDateList).Concat(trailContentDateList).Concat(videoContentDateList)
            .OrderByDescending(x => x.FeedOn)
            .Take(topNumberOfEntries).Select(x => x.ContentId).ToList();

        var dynamicContent = await db.ContentFromContentIds(contentIdListForFeed);

        return dynamicContent.OrderByDescending(x => x.FeedOn).ToList();
    }

    public static async Task<MapComponentDto> MapComponentDtoFromContentId(Guid mapComponentGuid)
    {
        var db = await Context().ConfigureAwait(false);

        var map = db.MapComponents.Single(x => x.ContentId == mapComponentGuid);
        var elements = await db.MapComponentElements.Where(x => x.MapComponentContentId == mapComponentGuid)
            .ToListAsync().ConfigureAwait(false);

        return new MapComponentDto(map, elements);
    }

    public static async Task<List<string>> MapIconNames()
    {
        var db = await Context();
        return "".AsList().Concat(await db.MapIcons.Select(x => x.IconName).OrderBy(x => x).ToListAsync()).ToList();
    }

    public static async Task<string?> MapIconSvgFromMapIconName(string? mapIconName)
    {
        if (string.IsNullOrWhiteSpace(mapIconName)) return null;

        var db = await Context().ConfigureAwait(false);

        return (await db.MapIcons.SingleOrDefaultAsync(x => x.IconName == mapIconName).ConfigureAwait(false))?.IconSvg;
    }

    public static bool MapInitialBoundingBoxOverlaps(MapComponent content, SpatialBounds bounds)
    {
        return (
                   (bounds.MinLatitude >= content.InitialViewBoundsMinLatitude
                    && bounds.MinLatitude <= content.InitialViewBoundsMaxLatitude)
                   || (bounds.MaxLatitude >= content.InitialViewBoundsMinLatitude
                       && bounds.MaxLatitude <= content.InitialViewBoundsMaxLatitude)
                   || (bounds.MinLatitude <= content.InitialViewBoundsMinLatitude &&
                       bounds.MaxLatitude >= content.InitialViewBoundsMaxLatitude)
               )
               &&
               (
                   (bounds.MinLongitude >= content.InitialViewBoundsMinLongitude
                    && bounds.MinLongitude <= content.InitialViewBoundsMaxLongitude)
                   || (bounds.MaxLongitude >= content.InitialViewBoundsMinLongitude
                       && bounds.MaxLongitude <= content.InitialViewBoundsMaxLongitude)
                   || (bounds.MinLongitude <= content.InitialViewBoundsMinLongitude &&
                       bounds.MaxLongitude >= content.InitialViewBoundsMaxLongitude)
               );
    }

    public static async Task<List<dynamic>> OptionalLocationContentFromBoundingBox(this PointlessWaymarksContext db,
        SpatialBounds bounds)
    {
        var returnList = new List<dynamic>();

        returnList.AddRange(await db.FileContents.Where(x =>
            x.Latitude != null && x.Longitude != null
                               && x.Latitude >= bounds.MinLatitude
                               && x.Latitude <= bounds.MaxLatitude
                               && x.Longitude >= bounds.MinLongitude
                               && x.Longitude <= bounds.MaxLongitude).ToListAsync());

        returnList.AddRange(await db.ImageContents.Where(x =>
            x.Latitude != null && x.Longitude != null
                               && x.Latitude >= bounds.MinLatitude
                               && x.Latitude <= bounds.MaxLatitude
                               && x.Longitude >= bounds.MinLongitude
                               && x.Longitude <= bounds.MaxLongitude).ToListAsync());

        returnList.AddRange(await db.PhotoContents.Where(x =>
            x.Latitude != null && x.Longitude != null
                               && x.Latitude >= bounds.MinLatitude
                               && x.Latitude <= bounds.MaxLatitude
                               && x.Longitude >= bounds.MinLongitude
                               && x.Longitude <= bounds.MaxLongitude).ToListAsync());

        returnList.AddRange(await db.PostContents.Where(x =>
            x.Latitude != null && x.Longitude != null
                               && x.Latitude >= bounds.MinLatitude
                               && x.Latitude <= bounds.MaxLatitude
                               && x.Longitude >= bounds.MinLongitude
                               && x.Longitude <= bounds.MaxLongitude).ToListAsync());

        returnList.AddRange(await db.VideoContents.Where(x =>
            x.Latitude != null && x.Longitude != null
                               && x.Latitude >= bounds.MinLatitude
                               && x.Latitude <= bounds.MaxLatitude
                               && x.Longitude >= bounds.MinLongitude
                               && x.Longitude <= bounds.MaxLongitude).ToListAsync());

        return returnList;
    }

    public static async Task<List<Guid>> OptionalLocationContentIdsFromBoundingBox(this PointlessWaymarksContext db,
        SpatialBounds bounds)
    {
        var returnList = new List<Guid>();

        returnList.AddRange(await db.FileContents.Where(x =>
            x.Latitude != null && x.Longitude != null
                               && x.Latitude >= bounds.MinLatitude
                               && x.Latitude <= bounds.MaxLatitude
                               && x.Longitude >= bounds.MinLongitude
                               && x.Longitude <= bounds.MaxLongitude).Select(x => x.ContentId).ToListAsync());

        returnList.AddRange(await db.ImageContents.Where(x =>
            x.Latitude != null && x.Longitude != null
                               && x.Latitude >= bounds.MinLatitude
                               && x.Latitude <= bounds.MaxLatitude
                               && x.Longitude >= bounds.MinLongitude
                               && x.Longitude <= bounds.MaxLongitude).Select(x => x.ContentId).ToListAsync());

        returnList.AddRange(await db.PhotoContents.Where(x =>
            x.Latitude != null && x.Longitude != null
                               && x.Latitude >= bounds.MinLatitude
                               && x.Latitude <= bounds.MaxLatitude
                               && x.Longitude >= bounds.MinLongitude
                               && x.Longitude <= bounds.MaxLongitude).Select(x => x.ContentId).ToListAsync());

        returnList.AddRange(await db.PostContents.Where(x =>
            x.Latitude != null && x.Longitude != null
                               && x.Latitude >= bounds.MinLatitude
                               && x.Latitude <= bounds.MaxLatitude
                               && x.Longitude >= bounds.MinLongitude
                               && x.Longitude <= bounds.MaxLongitude).Select(x => x.ContentId).ToListAsync());

        returnList.AddRange(await db.VideoContents.Where(x =>
            x.Latitude != null && x.Longitude != null
                               && x.Latitude >= bounds.MinLatitude
                               && x.Latitude <= bounds.MaxLatitude
                               && x.Longitude >= bounds.MinLongitude
                               && x.Longitude <= bounds.MaxLongitude).Select(x => x.ContentId).ToListAsync());

        return returnList;
    }

    public static bool OptionalLocationContentIsInBoundingBox(IOptionalLocation content, SpatialBounds bounds)
    {
        return content is { Latitude: not null, Longitude: not null } &&
               content.Latitude >= bounds.MinLatitude
               && content.Latitude <= bounds.MaxLatitude
               && content.Longitude >= bounds.MinLongitude
               && content.Longitude <= bounds.MaxLongitude;
    }

    public static List<(string tag, List<object> contentObjects)> ParseToTagSlugsAndContentList(List<ITag>? toAdd,
        bool removeExcludedTags, IProgress<string>? progress = null)
    {
        var returnList = new List<(string tag, List<object> contentObjects)>();

        if (toAdd == null) return returnList;

        var allResults = new ConcurrentBag<(string tag, object contentObject)>();

        Parallel.ForEach(toAdd, x =>
        {
            var results = ParseToTagSlugsAndContentList(x, removeExcludedTags);

            results.ForEach(y => allResults.Add(y));
        });

        var projectedReturn = allResults.GroupBy(x => x.tag)
            .Select(x => (x.Key, x.Select(y => y.contentObject).ToList())).ToList();

        return projectedReturn;
    }

    public static List<(string tag, object contentObject)> ParseToTagSlugsAndContentList(ITag toAdd,
        bool removeExcludedTags)
    {
        var tags = TagListParseToSlugs(toAdd, removeExcludedTags);

        return !tags.Any()
            ? new List<(string tag, object contentObject)>()
            : tags.Select(loopTags => (loopTags, (object)toAdd)).ToList();
    }

    /// <summary>
    ///     Finds the next photo based on PhotoDate, CreatedDate and ContentId.
    /// </summary>
    public static async Task<PhotoContent?> PhotoCommonContentNext(DateTime photoDate)
    {
        var db = await Context().ConfigureAwait(false);

        return await db.PhotoContents.Where(x => !x.IsDraft && x.PhotoCreatedOn > photoDate)
            .OrderBy(x => x.PhotoCreatedOn).ThenBy(x => x.CreatedOn).ThenBy(x => x.ContentId).FirstOrDefaultAsync()
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Finds the previous photo based on PhotoDate, CreatedDate and ContentId.
    /// </summary>
    /// <returns></returns>
    public static async Task<PhotoContent?> PhotoCommonContentPrevious(DateTime photoDate)
    {
        var db = await Context().ConfigureAwait(false);

        return await db.PhotoContents.Where(x => !x.IsDraft && x.PhotoCreatedOn < photoDate)
            .OrderByDescending(x => x.PhotoCreatedOn).ThenByDescending(x => x.CreatedOn)
            .ThenByDescending(x => x.ContentId).FirstOrDefaultAsync().ConfigureAwait(false);
    }

    public static async Task<List<PhotoContent>> PhotoContentFromBoundingBox(this PointlessWaymarksContext db,
        SpatialBounds bounds)
    {
        var returnList = new List<PhotoContent>();

        returnList.AddRange(await db.PhotoContents.Where(x =>
            x.Latitude != null && x.Longitude != null
                               && x.Latitude >= bounds.MinLatitude
                               && x.Latitude <= bounds.MaxLatitude
                               && x.Longitude >= bounds.MinLongitude
                               && x.Longitude <= bounds.MaxLongitude).ToListAsync());

        return returnList;
    }

    public static async Task<PointContentDto?> PointContentDto(Guid pointContentId)
    {
        var db = await Context().ConfigureAwait(false);

        return await PointContentDto(pointContentId, db).ConfigureAwait(false);
    }

    public static async Task<PointContentDto?> PointContentDto(Guid pointContentId, PointlessWaymarksContext db)
    {
        var point = await db.PointContents.SingleAsync(x => x.ContentId == pointContentId).ConfigureAwait(false);

        return await PointContentDtoFromPoint(point, db).ConfigureAwait(false);
    }

    public static async Task<List<PointContentDto>> PointContentDto(List<Guid>? pointContentIdList,
        PointlessWaymarksContext db)
    {
        if (pointContentIdList == null) return [];

        var returnList = new List<PointContentDto>();

        foreach (var loopId in pointContentIdList)
        {
            var toAdd = await PointContentDto(loopId, db).ConfigureAwait(false);
            if (toAdd != null) returnList.Add(toAdd);
        }

        return returnList;
    }

    public static async Task<PointContentDto> PointContentDtoFromPoint(PointContent content,
        PointlessWaymarksContext context)
    {
        var toReturn = new PointContentDto();

        toReturn.InjectFrom(content);

        toReturn.PointDetails = await PointDetailsForPoint(content.ContentId, context);
        if (!string.IsNullOrWhiteSpace(content.MapIconName))
            toReturn.MapIconSvg = (await context.MapIcons.SingleOrDefaultAsync(x => x.IconName == content.MapIconName)
                .ConfigureAwait(false))?.IconSvg;

        return toReturn;
    }

    public static PointContentDto PointContentDtoFromPointContentAndDetails(PointContent content,
        List<PointDetail> details, string? mapIconSvg)
    {
        var toReturn = new PointContentDto();

        toReturn.InjectFrom(content);

        toReturn.PointDetails = details;
        toReturn.MapIconSvg = mapIconSvg;

        return toReturn;
    }

    public static (PointContent content, List<PointDetail> details) PointContentDtoToPointContentAndDetails(
        PointContentDto dto)
    {
        var toSave = (PointContent)PointContent.CreateInstance().InjectFrom(dto);
        var relatedDetails = dto.PointDetails;

        return (toSave, relatedDetails);
    }

    public static async Task<List<PointContent>> PointContentFromBoundingBox(this PointlessWaymarksContext db,
        SpatialBounds bounds)
    {
        var returnList = new List<PointContent>();

        returnList.AddRange(await db.PointContents
            .Where(x => x.Latitude >= bounds.MinLatitude
                        && x.Latitude <= bounds.MaxLatitude
                        && x.Longitude >= bounds.MinLongitude
                        && x.Longitude <= bounds.MaxLongitude).ToListAsync());

        return returnList;
    }

    public static async Task<List<Guid>> PointContentIdsFromBoundingBox(this PointlessWaymarksContext db,
        SpatialBounds bounds)
    {
        var returnList = new List<Guid>();

        returnList.AddRange(await db.PointContents
            .Where(x => x.Latitude >= bounds.MinLatitude
                        && x.Latitude <= bounds.MaxLatitude
                        && x.Longitude >= bounds.MinLongitude
                        && x.Longitude <= bounds.MaxLongitude).Select(x => x.ContentId).ToListAsync());


        return returnList;
    }

    public static bool PointContentIsInBoundingBox(PointContent point, SpatialBounds bounds)
    {
        return point.Latitude >= bounds.MinLatitude
               && point.Latitude <= bounds.MaxLatitude
               && point.Longitude >= bounds.MinLongitude
               && point.Longitude <= bounds.MaxLongitude;
    }

    public static IPointDetailData? PointDetailDataFromIdentifierAndJson(string dataIdentifier, string json)
    {
        return dataIdentifier switch
        {
            "Campground" => JsonSerializer.Deserialize<Campground>(json),
            "Feature" => JsonSerializer.Deserialize<Feature>(json),
            "Fee" => JsonSerializer.Deserialize<Fee>(json),
            "Driving Directions" => JsonSerializer.Deserialize<DrivingDirections>(json),
            "Parking" => JsonSerializer.Deserialize<Parking>(json),
            "Peak" => JsonSerializer.Deserialize<Peak>(json),
            "Restroom" => JsonSerializer.Deserialize<Restroom>(json),
            "Trail Junction" => JsonSerializer.Deserialize<TrailJunction>(json),
            "Vehicle Access" => JsonSerializer.Deserialize<VehicleAccess>(json),
            _ => null
        };
    }

    public static bool PointDetailDataTypeIsValid(string dataType)
    {
        var pointDetailTypes = from type in typeof(Db).Assembly.GetTypes()
            where typeof(IPointDetailData).IsAssignableFrom(type) && !type.IsInterface
            select type;

        foreach (var loopTypes in pointDetailTypes)
        {
            var typeExample = (IPointDetailData?)Activator.CreateInstance(loopTypes);

            if (typeExample == null) continue;

            if (typeExample.DataTypeIdentifier == dataType) return true;
        }

        return false;
    }

    public static async Task<List<PointDetail>> PointDetailsForPoint(Guid pointContentId, PointlessWaymarksContext db)
    {
        var details = await db.PointDetails.Where(x => x.PointContentId == pointContentId).ToListAsync()
            .ConfigureAwait(false);

        return details;
    }

    public static async Task<List<PointContentDto>> PointsAndPointDetails(List<Guid> pointContentId)
    {
        var db = await Context().ConfigureAwait(false);

        var idChunks = pointContentId.Chunk(250);

        var returnList = new List<PointContentDto>();

        foreach (var loopChunk in idChunks)
        {
            var contents = await db.PointContents.Where(x => loopChunk.Contains(x.ContentId)).ToListAsync()
                .ConfigureAwait(false);

            foreach (var loopContent in contents)
            {
                var details = await PointDetailsForPoint(loopContent.ContentId, db).ConfigureAwait(false);
                var toAdd = new PointContentDto();
                toAdd.InjectFrom(loopContent);
                toAdd.PointDetails = details;

                returnList.Add(toAdd);
            }
        }

        return returnList;
    }

    public static async Task<List<PostContent>> PostContentFromBoundingBox(this PointlessWaymarksContext db,
        SpatialBounds bounds)
    {
        var returnList = new List<PostContent>();

        returnList.AddRange(await db.PostContents.Where(x =>
            x.Latitude != null && x.Longitude != null
                               && x.Latitude >= bounds.MinLatitude
                               && x.Latitude <= bounds.MaxLatitude
                               && x.Longitude >= bounds.MinLongitude
                               && x.Longitude <= bounds.MaxLongitude).ToListAsync());

        return returnList;
    }

    public static async Task SaveFileContent(FileContent? toSave)
    {
        if (toSave == null) return;

        var context = await Context().ConfigureAwait(false);

        var toHistoric = await context.FileContents.Where(x => x.ContentId == toSave.ContentId).ToListAsync()
            .ConfigureAwait(false);

        var isUpdate = toHistoric.Any();

        foreach (var loopToHistoric in toHistoric)
        {
            var newHistoric = new HistoricFileContent();
            newHistoric.InjectFrom(loopToHistoric);
            newHistoric.Id = 0;
            newHistoric.LastUpdatedOn = DateTime.Now;
            if (string.IsNullOrWhiteSpace(newHistoric.LastUpdatedBy))
                newHistoric.LastUpdatedBy = "Historic Entry Archivist";
            await context.HistoricFileContents.AddAsync(newHistoric).ConfigureAwait(false);
            context.FileContents.Remove(loopToHistoric);
        }

        if (toSave.Id > 0) toSave.Id = 0;
        toSave.ContentVersion = ContentVersionDateTime();

        toSave.MainPicture = toSave.UserMainPicture ??
                             await BracketCodeCommon.PhotoOrImageCodeFirstIdInContent(toSave.BodyContent, null);

        await context.FileContents.AddAsync(toSave).ConfigureAwait(false);

        await context.SaveChangesAsync(true).ConfigureAwait(false);

        DataNotifications.PublishDataNotification("Db", DataNotificationContentType.File,
            isUpdate ? DataNotificationUpdateType.Update : DataNotificationUpdateType.New,
            [toSave.ContentId]);
    }

    public static async Task SaveGenerationFileTransferScriptLog(GenerationFileTransferScriptLog? toSave)
    {
        if (toSave == null) return;

        var context = await Context().ConfigureAwait(false);

        await context.GenerationFileTransferScriptLogs.AddAsync(toSave).ConfigureAwait(false);

        await context.SaveChangesAsync().ConfigureAwait(false);

        DataNotifications.PublishDataNotification("Db", DataNotificationContentType.FileTransferScriptLog,
            DataNotificationUpdateType.New, []);
    }

    public static async Task SaveGenerationLogAndRecordSettings(DateTime generationVersion)
    {
        var db = await Context().ConfigureAwait(false);

        var serializedSettings = JsonSerializer.Serialize(UserSettingsSingleton.CurrentSettings().GenerationValues());
        var dbGenerationRecord = new GenerationLog
        {
            GenerationSettings = serializedSettings, GenerationVersion = generationVersion
        };

        await db.GenerationLogs.AddAsync(dbGenerationRecord).ConfigureAwait(false);
        await db.SaveChangesAsync(true).ConfigureAwait(false);

        DataNotifications.PublishDataNotification("Db", DataNotificationContentType.GenerationLog,
            DataNotificationUpdateType.New, []);
    }

    public static async Task SaveGeoJsonContent(GeoJsonContent? toSave)
    {
        if (toSave == null) return;

        var context = await Context().ConfigureAwait(false);

        var toHistoric = await context.GeoJsonContents.Where(x => x.ContentId == toSave.ContentId).ToListAsync()
            .ConfigureAwait(false);

        var isUpdate = toHistoric.Any();

        foreach (var loopToHistoric in toHistoric)
        {
            var newHistoric = new HistoricGeoJsonContent();
            newHistoric.InjectFrom(loopToHistoric);
            newHistoric.Id = 0;
            newHistoric.LastUpdatedOn = DateTime.Now;
            if (string.IsNullOrWhiteSpace(newHistoric.LastUpdatedBy))
                newHistoric.LastUpdatedBy = "Historic Entry Archivist";
            await context.HistoricGeoJsonContents.AddAsync(newHistoric).ConfigureAwait(false);
            context.GeoJsonContents.Remove(loopToHistoric);
        }

        await context.SaveChangesAsync().ConfigureAwait(false);

        if (toSave.Id > 0) toSave.Id = 0;

        toSave.ContentVersion = ContentVersionDateTime();

        toSave.MainPicture = await BracketCodeCommon.PhotoOrImageCodeFirstIdInContent(toSave.BodyContent, null);

        var boundingBox = SpatialConverters.GeometryBoundingBox(toSave);

        toSave.InitialViewBoundsMaxLatitude = boundingBox.MaxY;
        toSave.InitialViewBoundsMaxLongitude = boundingBox.MaxX;
        toSave.InitialViewBoundsMinLatitude = boundingBox.MinY;
        toSave.InitialViewBoundsMinLongitude = boundingBox.MinX;
        DefaultPropertyCleanup(toSave);

        await context.GeoJsonContents.AddAsync(toSave).ConfigureAwait(false);

        await context.SaveChangesAsync(true).ConfigureAwait(false);

        DataNotifications.PublishDataNotification("Db", DataNotificationContentType.GeoJson,
            isUpdate ? DataNotificationUpdateType.Update : DataNotificationUpdateType.New,
            [toSave.ContentId]);
    }

    public static async Task SaveImageContent(ImageContent? toSave)
    {
        if (toSave == null) return;

        var context = await Context().ConfigureAwait(false);

        var toHistoric = await context.ImageContents.Where(x => x.ContentId == toSave.ContentId).ToListAsync()
            .ConfigureAwait(false);

        var isUpdate = toHistoric.Any();

        foreach (var loopToHistoric in toHistoric)
        {
            var newHistoric = new HistoricImageContent();
            newHistoric.InjectFrom(loopToHistoric);
            newHistoric.Id = 0;
            newHistoric.LastUpdatedOn = DateTime.Now;
            if (string.IsNullOrWhiteSpace(newHistoric.LastUpdatedBy))
                newHistoric.LastUpdatedBy = "Historic Entry Archivist";
            await context.HistoricImageContents.AddAsync(newHistoric).ConfigureAwait(false);
            context.ImageContents.Remove(loopToHistoric);
        }

        if (toSave.Id > 0) toSave.Id = 0;
        toSave.ContentVersion = ContentVersionDateTime();

        await context.ImageContents.AddAsync(toSave).ConfigureAwait(false);

        await context.SaveChangesAsync(true).ConfigureAwait(false);

        toSave.MainPicture = toSave.ContentId;

        await context.SaveChangesAsync(true).ConfigureAwait(false);

        DataNotifications.PublishDataNotification("Db", DataNotificationContentType.Image,
            isUpdate ? DataNotificationUpdateType.Update : DataNotificationUpdateType.New,
            [toSave.ContentId]);
    }

    public static async Task SaveLineContent(LineContent? toSave)
    {
        if (toSave == null) return;

        var context = await Context().ConfigureAwait(false);

        var toHistoric = await context.LineContents.Where(x => x.ContentId == toSave.ContentId).ToListAsync()
            .ConfigureAwait(false);

        var isUpdate = toHistoric.Any();

        foreach (var loopToHistoric in toHistoric)
        {
            var newHistoric = new HistoricLineContent();
            newHistoric.InjectFrom(loopToHistoric);
            newHistoric.Id = 0;
            newHistoric.LastUpdatedOn = DateTime.Now;
            if (string.IsNullOrWhiteSpace(newHistoric.LastUpdatedBy))
                newHistoric.LastUpdatedBy = "Historic Entry Archivist";
            await context.HistoricLineContents.AddAsync(newHistoric).ConfigureAwait(false);
            context.LineContents.Remove(loopToHistoric);
        }

        await context.SaveChangesAsync(true).ConfigureAwait(false);

        if (toSave.Id > 0) toSave.Id = 0;

        toSave.ContentVersion = ContentVersionDateTime();

        toSave.MainPicture = await BracketCodeCommon.PhotoOrImageCodeFirstIdInContent(toSave.BodyContent, null);

        var boundingBox = SpatialConverters.GeometryBoundingBox(toSave);

        toSave.InitialViewBoundsMaxLatitude = boundingBox.MaxY;
        toSave.InitialViewBoundsMaxLongitude = boundingBox.MaxX;
        toSave.InitialViewBoundsMinLatitude = boundingBox.MinY;
        toSave.InitialViewBoundsMinLongitude = boundingBox.MinX;
        DefaultPropertyCleanup(toSave);

        await context.LineContents.AddAsync(toSave).ConfigureAwait(false);

        await context.SaveChangesAsync(true).ConfigureAwait(false);

        DataNotifications.PublishDataNotification("Db", DataNotificationContentType.Line,
            isUpdate ? DataNotificationUpdateType.Update : DataNotificationUpdateType.New,
            [toSave.ContentId]);
    }

    public static async Task SaveLinkContent(LinkContent? toSave)
    {
        if (toSave == null) return;

        var context = await Context().ConfigureAwait(false);

        var toHistoric = await context.LinkContents.Where(x => x.ContentId == toSave.ContentId).ToListAsync()
            .ConfigureAwait(false);

        var isUpdate = toHistoric.Any();

        foreach (var loopToHistoric in toHistoric)
        {
            var newHistoric = new HistoricLinkContent();
            newHistoric.InjectFrom(loopToHistoric);
            newHistoric.Id = 0;
            newHistoric.LastUpdatedOn = DateTime.Now;
            if (string.IsNullOrWhiteSpace(newHistoric.LastUpdatedBy))
                newHistoric.LastUpdatedBy = "Historic Entry Archivist";
            await context.HistoricLinkContents.AddAsync(newHistoric).ConfigureAwait(false);
            context.LinkContents.Remove(loopToHistoric);
        }

        if (toSave.Id > 0) toSave.Id = 0;
        toSave.ContentVersion = ContentVersionDateTime();

        await context.LinkContents.AddAsync(toSave).ConfigureAwait(false);

        await context.SaveChangesAsync(true).ConfigureAwait(false);

        DataNotifications.PublishDataNotification("Db", DataNotificationContentType.Link,
            isUpdate ? DataNotificationUpdateType.Update : DataNotificationUpdateType.New,
            [toSave.ContentId]);
    }

    public static async Task<MapComponentDto> SaveMapComponent(MapComponentDto toSaveDto)
    {
        var context = await Context().ConfigureAwait(false);

        var groupLastUpdateOn = DateTime.Now;
        var updateGroup = Guid.NewGuid();

        var toHistoric = await context.MapComponents.Where(x => x.ContentId == toSaveDto.ContentId).ToListAsync()
            .ConfigureAwait(false);

        var isUpdate = toHistoric.Any();

        foreach (var loopToHistoric in toHistoric)
        {
            var newHistoric = new HistoricMapComponent();
            newHistoric.InjectFrom(loopToHistoric);
            newHistoric.Id = 0;
            newHistoric.LastUpdatedOn = groupLastUpdateOn;
            if (string.IsNullOrWhiteSpace(newHistoric.LastUpdatedBy))
                newHistoric.LastUpdatedBy = "Historic Entry Archivist";

            var elements = await context.MapComponentElements
                .Where(x => x.MapComponentContentId == loopToHistoric.ContentId).ToListAsync().ConfigureAwait(false);
            newHistoric.Elements = JsonSerializer.Serialize(elements);

            await context.HistoricMapComponents.AddAsync(newHistoric).ConfigureAwait(false);
            context.MapComponents.Remove(loopToHistoric);
        }

        if (toSaveDto.Id > 0) toSaveDto.Id = 0;
        toSaveDto.ContentVersion = ContentVersionDateTime();

        var dbMap = toSaveDto.ToDbObject();

        await context.MapComponents.AddAsync(dbMap).ConfigureAwait(false);

        await context.SaveChangesAsync(true).ConfigureAwait(false);

        var dbElements = await context.MapComponentElements
            .Where(x => x.MapComponentContentId == dbMap.ContentId).ToListAsync().ConfigureAwait(false);

        var dbElementContentIds = dbElements.Select(x => x.ElementContentId).Distinct().ToList();

        foreach (var loopElements in dbElements)
        {
            await context.HistoricMapComponentElements.AddAsync(new HistoricMapElement
            {
                ElementContentId = loopElements.ElementContentId,
                ShowDetailsDefault = loopElements.ShowDetailsDefault,
                IncludeInDefaultView = loopElements.IncludeInDefaultView,
                IsFeaturedElement = loopElements.IsFeaturedElement,
                LastUpdateOn = groupLastUpdateOn,
                HistoricGroupId = updateGroup,
                MapComponentContentId = loopElements.MapComponentContentId,
                LinksTo = loopElements.LinksTo
            }).ConfigureAwait(false);

            context.MapComponentElements.Remove(loopElements);
        }

        await context.SaveChangesAsync().ConfigureAwait(false);

        var newElementsContentIds = toSaveDto.Elements.Select(x => x.ElementContentId).ToList();

        foreach (var loopElements in toSaveDto.Elements)
        {
            loopElements.Id = 0;
            await context.MapComponentElements.AddAsync(loopElements).ConfigureAwait(false);
        }

        await context.SaveChangesAsync().ConfigureAwait(false);

        var points = await context.PointContents.Where(x => newElementsContentIds.Contains(x.ContentId)).ToListAsync()
            .ConfigureAwait(false);
        var boundingBox = SpatialConverters.PointBoundingBox(points);

        var photos = await context.PhotoContents.Where(x => newElementsContentIds.Contains(x.ContentId)).ToListAsync()
            .ConfigureAwait(false);
        if (photos.Any()) boundingBox = SpatialConverters.PhotoBoundingBox(photos, boundingBox);

        var geoJsonLines = await context.LineContents.Where(x => newElementsContentIds.Contains(x.ContentId))
            .ToListAsync().ConfigureAwait(false);
        if (geoJsonLines.Any()) boundingBox = SpatialConverters.GeometryBoundingBox(geoJsonLines, boundingBox);

        var geoJson = await context.GeoJsonContents.Where(x => newElementsContentIds.Contains(x.ContentId))
            .ToListAsync().ConfigureAwait(false);
        if (geoJson.Any()) boundingBox = SpatialConverters.GeometryBoundingBox(geoJson, boundingBox);

        dbMap.InitialViewBoundsMaxLatitude = boundingBox.MaxY;
        dbMap.InitialViewBoundsMaxLongitude = boundingBox.MaxX;
        dbMap.InitialViewBoundsMinLatitude = boundingBox.MinY;
        dbMap.InitialViewBoundsMinLongitude = boundingBox.MinX;
        DefaultPropertyCleanup(dbMap);

        await context.SaveChangesAsync().ConfigureAwait(false);

        var newElements = newElementsContentIds.Except(dbElementContentIds).ToList();
        var updatedElements = newElementsContentIds.Where(x => dbElementContentIds.Contains(x)).ToList();
        var deletedElements = dbElementContentIds.Except(newElementsContentIds).ToList();

        DataNotifications.PublishDataNotification("Db", DataNotificationContentType.Map,
            isUpdate ? DataNotificationUpdateType.Update : DataNotificationUpdateType.New,
            [dbMap.ContentId]);

        DataNotifications.PublishDataNotification("Db", DataNotificationContentType.MapElement,
            DataNotificationUpdateType.New, newElements);

        DataNotifications.PublishDataNotification("Db", DataNotificationContentType.MapElement,
            DataNotificationUpdateType.Update, updatedElements);

        DataNotifications.PublishDataNotification("Db", DataNotificationContentType.MapElement,
            DataNotificationUpdateType.Delete, deletedElements);

        return await MapComponentDtoFromContentId(dbMap.ContentId);
    }

    public static async Task<MapIcon> SaveMapIcon(MapIcon toSave)
    {
        var context = await Context();

        var possibleExistingItem =
            await context.MapIcons.FirstOrDefaultAsync(x => x.ContentId == toSave.ContentId);

        if (possibleExistingItem != null)
        {
            var historicEntry = toSave.ToHistoricMapIcon();

            await context.HistoricMapIcons.AddAsync(historicEntry);
            context.Remove(possibleExistingItem);
        }

        await context.AddAsync(toSave);
        await context.SaveChangesAsync();

        if (possibleExistingItem is null)
            DataNotifications.PublishDataNotification("Map Icon Added", DataNotificationContentType.MapIcon,
                DataNotificationUpdateType.New, toSave.ContentId.AsList());
        else
            DataNotifications.PublishDataNotification("Map Icon Updated", DataNotificationContentType.MapIcon,
                DataNotificationUpdateType.Update, toSave.ContentId.AsList());

        return toSave;
    }

    public static async Task SaveNoteContent(NoteContent? toSave)
    {
        if (toSave == null) return;

        var context = await Context().ConfigureAwait(false);

        var toHistoric = await context.NoteContents.Where(x => x.ContentId == toSave.ContentId).ToListAsync()
            .ConfigureAwait(false);

        var isUpdate = toHistoric.Any();

        foreach (var loopToHistoric in toHistoric)
        {
            var newHistoric = new HistoricNoteContent();
            newHistoric.InjectFrom(loopToHistoric);
            newHistoric.Id = 0;
            newHistoric.LastUpdatedOn = DateTime.Now;
            if (string.IsNullOrWhiteSpace(newHistoric.LastUpdatedBy))
                newHistoric.LastUpdatedBy = "Historic Entry Archivist";
            await context.HistoricNoteContents.AddAsync(newHistoric).ConfigureAwait(false);
            context.NoteContents.Remove(loopToHistoric);
        }

        if (toSave.Id > 0) toSave.Id = 0;
        toSave.ContentVersion = ContentVersionDateTime();

        await context.NoteContents.AddAsync(toSave).ConfigureAwait(false);

        await context.SaveChangesAsync(true).ConfigureAwait(false);

        DataNotifications.PublishDataNotification("Db", DataNotificationContentType.Note,
            isUpdate ? DataNotificationUpdateType.Update : DataNotificationUpdateType.New,
            [toSave.ContentId]);
    }

    public static async Task SavePhotoContent(PhotoContent? toSave)
    {
        if (toSave == null) return;

        var context = await Context().ConfigureAwait(false);

        var toHistoric = await context.PhotoContents.Where(x => x.ContentId == toSave.ContentId).ToListAsync()
            .ConfigureAwait(false);

        var isUpdate = toHistoric.Any();

        foreach (var loopToHistoric in toHistoric)
        {
            var newHistoric = new HistoricPhotoContent();
            newHistoric.InjectFrom(loopToHistoric);
            newHistoric.Id = 0;
            newHistoric.LastUpdatedOn = DateTime.Now;
            if (string.IsNullOrWhiteSpace(newHistoric.LastUpdatedBy))
                newHistoric.LastUpdatedBy = "Historic Entry Archivist";
            await context.HistoricPhotoContents.AddAsync(newHistoric).ConfigureAwait(false);
            context.PhotoContents.Remove(loopToHistoric);
        }

        if (toSave.Id > 0) toSave.Id = 0;
        toSave.ContentVersion = ContentVersionDateTime();

        await context.PhotoContents.AddAsync(toSave).ConfigureAwait(false);

        await context.SaveChangesAsync(true).ConfigureAwait(false);

        toSave.MainPicture = toSave.ContentId;

        await context.SaveChangesAsync(true).ConfigureAwait(false);

        DataNotifications.PublishDataNotification("Db", DataNotificationContentType.Photo,
            isUpdate ? DataNotificationUpdateType.Update : DataNotificationUpdateType.New,
            [toSave.ContentId]);
    }

    public static async Task<PointContentDto?> SavePointContent(PointContentDto? toSaveDto)
    {
        if (toSaveDto == null) return null;

        var (toSave, relatedDetails) = PointContentDtoToPointContentAndDetails(toSaveDto);

        var context = await Context().ConfigureAwait(false);

        var toHistoric = await context.PointContents.Where(x => x.ContentId == toSave.ContentId).ToListAsync()
            .ConfigureAwait(false);

        var isUpdate = toHistoric.Any();

        foreach (var loopToHistoric in toHistoric)
        {
            var newHistoric = new HistoricPointContent();
            newHistoric.InjectFrom(loopToHistoric);
            newHistoric.Id = 0;
            newHistoric.LastUpdatedOn = DateTime.Now;
            if (string.IsNullOrWhiteSpace(newHistoric.LastUpdatedBy))
                newHistoric.LastUpdatedBy = "Historic Entry Archivist";

            var pointDetails = await context.PointDetails.Where(x => x.PointContentId == loopToHistoric.ContentId)
                .ToListAsync().ConfigureAwait(false);
            newHistoric.PointDetails = JsonSerializer.Serialize(pointDetails);

            await context.HistoricPointContents.AddAsync(newHistoric).ConfigureAwait(false);
            context.PointContents.Remove(loopToHistoric);
        }

        if (toSave.Id > 0) toSave.Id = 0;
        toSave.ContentVersion = ContentVersionDateTime();

        toSave.MainPicture = await BracketCodeCommon.PhotoOrImageCodeFirstIdInContent(toSave.BodyContent, null);

        await context.PointContents.AddAsync(toSave).ConfigureAwait(false);

        await context.SaveChangesAsync(true).ConfigureAwait(false);

        await SavePointDetailContent(relatedDetails).ConfigureAwait(false);

        DataNotifications.PublishDataNotification("Db", DataNotificationContentType.Point,
            isUpdate ? DataNotificationUpdateType.Update : DataNotificationUpdateType.New, toSave.ContentId.AsList());

        return await PointContentDto(toSaveDto.ContentId).ConfigureAwait(false);
    }

    public static async Task SavePointDetailContent(List<PointDetail> toSave)
    {
        if (!toSave.Any()) return;

        if (toSave.Select(x => x.PointContentId).Distinct().Count() > 1)
        {
            var grouped = toSave.GroupBy(x => x.PointContentId).ToList();

            foreach (var loopGroups in grouped)
                await SavePointDetailContent(loopGroups.Select(x => x).ToList()).ConfigureAwait(false);

            return;
        }

        //The code above is intended to ensure that by this point all the PointDetails to save are related to the same PointContent

        var context = await Context().ConfigureAwait(false);

        var groupLastUpdateOn = DateTime.Now;
        var updateGroup = Guid.NewGuid();

        var toSaveGuids = toSave.Select(x => x.ContentId).ToList();
        var relatedContentGuid = toSave.First().PointContentId;

        var currentEntriesFromPoint = await context.PointDetails.Where(x => x.PointContentId == relatedContentGuid)
            .ToListAsync().ConfigureAwait(false);
        var detailsToReplace = currentEntriesFromPoint.Where(x => toSaveGuids.Contains(x.ContentId)).ToList();

        //The logic here is that if there are items to remove it is an update and if not the item is new
        var updatedDetailIds = toSave.Where(x => detailsToReplace.Select(y => y.ContentId).Contains(x.ContentId))
            .Select(x => x.ContentId).ToList();
        var newDetailIds = toSave.Where(x => !detailsToReplace.Select(y => y.ContentId).Contains(x.ContentId))
            .Select(x => x.ContentId).ToList();

        foreach (var loopToHistoric in currentEntriesFromPoint)
        {
            var newHistoric = new HistoricPointDetail();
            newHistoric.InjectFrom(loopToHistoric);
            newHistoric.Id = 0;
            newHistoric.LastUpdatedOn = groupLastUpdateOn;
            newHistoric.HistoricGroupId = updateGroup;
            await context.HistoricPointDetails.AddAsync(newHistoric).ConfigureAwait(false);
        }

        var deletedContentIds = currentEntriesFromPoint.Select(x => x.ContentId).Except(updatedDetailIds)
            .Except(newDetailIds).ToList();

        context.PointDetails.RemoveRange(currentEntriesFromPoint);

        await context.SaveChangesAsync().ConfigureAwait(false);

        toSave.ForEach(x => x.ContentVersion = ContentVersionDateTime());

        await context.PointDetails.AddRangeAsync(toSave).ConfigureAwait(false);

        await context.SaveChangesAsync(true).ConfigureAwait(false);

        if (deletedContentIds.Any())
            DataNotifications.PublishDataNotification("Db", DataNotificationContentType.PointDetail,
                DataNotificationUpdateType.Delete, updatedDetailIds);

        if (updatedDetailIds.Any())
            DataNotifications.PublishDataNotification("Db", DataNotificationContentType.PointDetail,
                DataNotificationUpdateType.Update, updatedDetailIds);

        if (newDetailIds.Any())
            DataNotifications.PublishDataNotification("Db", DataNotificationContentType.PointDetail,
                DataNotificationUpdateType.New, updatedDetailIds);
    }

    /// <summary>
    ///     Save a PointDetail - saving a single point detail will save the entire current set of Point Details
    ///     into Historic Details to preserve history - it is more efficient to submit all changes at once.
    /// </summary>
    /// <param name="toSave"></param>
    /// <returns></returns>
    public static async Task SavePointDetailContent(PointDetail? toSave)
    {
        if (toSave == null) return;

        var context = await Context().ConfigureAwait(false);

        var groupLastUpdateOn = DateTime.Now;
        var updateGroup = Guid.NewGuid();

        var currentEntriesFromPoint = await context.PointDetails.Where(x => x.PointContentId == toSave.PointContentId)
            .ToListAsync().ConfigureAwait(false);
        var detailsToReplace = currentEntriesFromPoint.Where(x => x.ContentId == toSave.ContentId).ToList();
        var isUpdate = detailsToReplace.Any();

        foreach (var loopToHistoric in currentEntriesFromPoint)
        {
            var newHistoric = new HistoricPointDetail();
            newHistoric.InjectFrom(loopToHistoric);
            newHistoric.Id = 0;
            newHistoric.LastUpdatedOn = groupLastUpdateOn;
            newHistoric.HistoricGroupId = updateGroup;
            await context.HistoricPointDetails.AddAsync(newHistoric).ConfigureAwait(false);
        }

        context.PointDetails.RemoveRange(detailsToReplace);

        await context.SaveChangesAsync().ConfigureAwait(false);

        if (toSave.Id > 0) toSave.Id = 0;

        toSave.ContentVersion = ContentVersionDateTime();

        await context.PointDetails.AddAsync(toSave).ConfigureAwait(false);

        await context.SaveChangesAsync(true).ConfigureAwait(false);

        DataNotifications.PublishDataNotification("Db", DataNotificationContentType.PointDetail,
            isUpdate ? DataNotificationUpdateType.Update : DataNotificationUpdateType.New,
            [toSave.ContentId]);
    }

    public static async Task SavePostContent(PostContent? toSave)
    {
        if (toSave == null) return;

        var context = await Context().ConfigureAwait(false);

        var toHistoric = await context.PostContents.Where(x => x.ContentId == toSave.ContentId).ToListAsync()
            .ConfigureAwait(false);

        var isUpdate = toHistoric.Any();

        foreach (var loopToHistoric in toHistoric)
        {
            var newHistoric = new HistoricPostContent();
            newHistoric.InjectFrom(loopToHistoric);
            newHistoric.Id = 0;
            newHistoric.LastUpdatedOn = DateTime.Now;
            if (string.IsNullOrWhiteSpace(newHistoric.LastUpdatedBy))
                newHistoric.LastUpdatedBy = "Historic Entry Archivist";
            await context.HistoricPostContents.AddAsync(newHistoric).ConfigureAwait(false);
            context.PostContents.Remove(loopToHistoric);
        }

        if (toSave.Id > 0) toSave.Id = 0;
        toSave.ContentVersion = ContentVersionDateTime();

        toSave.MainPicture = await BracketCodeCommon.PhotoOrImageCodeFirstIdInContent(toSave.BodyContent, null);

        await context.PostContents.AddAsync(toSave).ConfigureAwait(false);

        await context.SaveChangesAsync(true).ConfigureAwait(false);

        DataNotifications.PublishDataNotification("Db", DataNotificationContentType.Post,
            isUpdate ? DataNotificationUpdateType.Update : DataNotificationUpdateType.New,
            [toSave.ContentId]);
    }

    public static async Task SaveSnippet(Snippet? toSave)
    {
        if (toSave == null) return;

        var context = await Context().ConfigureAwait(false);

        var toHistoric = await context.Snippets.Where(x => x.ContentId == toSave.ContentId).ToListAsync()
            .ConfigureAwait(false);

        var isUpdate = toHistoric.Any();

        foreach (var loopToHistoric in toHistoric)
        {
            var newHistoric = new HistoricSnippet
            {
                ContentId = loopToHistoric.ContentId,
                CreatedOn = loopToHistoric.CreatedOn,
                ContentVersion = loopToHistoric.ContentVersion
            };
            newHistoric.InjectFrom(loopToHistoric);
            newHistoric.Id = 0;
            newHistoric.LastUpdatedOn = DateTime.Now;
            if (string.IsNullOrWhiteSpace(newHistoric.LastUpdatedBy))
                newHistoric.LastUpdatedBy = "Historic Entry Archivist";
            await context.HistoricSnippets.AddAsync(newHistoric).ConfigureAwait(false);
            context.Snippets.Remove(loopToHistoric);
        }

        if (toSave.Id > 0) toSave.Id = 0;
        toSave.ContentVersion = ContentVersionDateTime();

        await context.Snippets.AddAsync(toSave).ConfigureAwait(false);

        await context.SaveChangesAsync(true).ConfigureAwait(false);

        DataNotifications.PublishDataNotification("Db", DataNotificationContentType.Snippet,
            isUpdate ? DataNotificationUpdateType.Update : DataNotificationUpdateType.New,
            [toSave.ContentId]);
    }

    public static async Task SaveTrailContent(TrailContent? toSave)
    {
        if (toSave == null) return;

        var context = await Context().ConfigureAwait(false);

        var toHistoric = await context.TrailContents.Where(x => x.ContentId == toSave.ContentId).ToListAsync()
            .ConfigureAwait(false);

        var isUpdate = toHistoric.Any();

        foreach (var loopToHistoric in toHistoric)
        {
            var newHistoric = new HistoricTrailContent();
            newHistoric.InjectFrom(loopToHistoric);
            newHistoric.Id = 0;
            newHistoric.LastUpdatedOn = DateTime.Now;
            if (string.IsNullOrWhiteSpace(newHistoric.LastUpdatedBy))
                newHistoric.LastUpdatedBy = "Historic Entry Archivist";
            await context.HistoricTrailContents.AddAsync(newHistoric).ConfigureAwait(false);
            context.TrailContents.Remove(loopToHistoric);
        }

        if (toSave.Id > 0) toSave.Id = 0;
        toSave.ContentVersion = ContentVersionDateTime();

        toSave.MainPicture = await BracketCodeCommon.PhotoOrImageCodeFirstIdInContent(toSave.BodyContent, null);

        await context.TrailContents.AddAsync(toSave).ConfigureAwait(false);

        await context.SaveChangesAsync(true).ConfigureAwait(false);

        DataNotifications.PublishDataNotification("Db", DataNotificationContentType.Trail,
            isUpdate ? DataNotificationUpdateType.Update : DataNotificationUpdateType.New,
            [toSave.ContentId]);
    }

    public static async Task SaveVideoContent(VideoContent? toSave)
    {
        if (toSave == null) return;

        var context = await Context().ConfigureAwait(false);

        var toHistoric = await context.VideoContents.Where(x => x.ContentId == toSave.ContentId).ToListAsync()
            .ConfigureAwait(false);

        var isUpdate = toHistoric.Any();

        foreach (var loopToHistoric in toHistoric)
        {
            var newHistoric = new HistoricVideoContent();
            newHistoric.InjectFrom(loopToHistoric);
            newHistoric.Id = 0;
            newHistoric.LastUpdatedOn = DateTime.Now;
            if (string.IsNullOrWhiteSpace(newHistoric.LastUpdatedBy))
                newHistoric.LastUpdatedBy = "Historic Entry Archivist";
            await context.HistoricVideoContents.AddAsync(newHistoric).ConfigureAwait(false);
            context.VideoContents.Remove(loopToHistoric);
        }

        if (toSave.Id > 0) toSave.Id = 0;
        toSave.ContentVersion = ContentVersionDateTime();

        toSave.MainPicture = toSave.UserMainPicture ?? await
            BracketCodeCommon.PhotoOrImageCodeFirstIdInContent(toSave.BodyContent, null);

        await context.VideoContents.AddAsync(toSave).ConfigureAwait(false);

        await context.SaveChangesAsync(true).ConfigureAwait(false);

        DataNotifications.PublishDataNotification("Db", DataNotificationContentType.Video,
            isUpdate ? DataNotificationUpdateType.Update : DataNotificationUpdateType.New,
            [toSave.ContentId]);
    }

    public static async Task<List<TagExclusion>> TagExclusions()
    {
        var context = await Context().ConfigureAwait(false);

        return await context.TagExclusions.OrderBy(x => x.Tag).ToListAsync().ConfigureAwait(false);
    }

    public static async Task<List<(string slug, TagExclusion exclusion)>> TagExclusionSlugAndExclusions()
    {
        var context = await Context().ConfigureAwait(false);

        return (await context.TagExclusions.OrderBy(x => x.Tag).ToListAsync().ConfigureAwait(false))
            .Select(x => (SlugTools.CreateSlug(true, x.Tag, 200), x)).ToList();
    }

    public static async Task<List<string>> TagExclusionSlugs()
    {
        var context = await Context().ConfigureAwait(false);

        return (await context.TagExclusions.OrderBy(x => x.Tag).ToListAsync().ConfigureAwait(false))
            .Select(x => SlugTools.CreateSlug(true, x.Tag, 200)).ToList();
    }

    public static string TagListCleanup(string? tags)
    {
        return string.IsNullOrWhiteSpace(tags) ? string.Empty : TagListJoin(TagListParse(tags));
    }

    public static List<string> TagListCleanup(List<string> listToClean)
    {
        if (!listToClean.Any()) return [];

        return listToClean.Select(TagListItemCleanup).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
    }

    /// <summary>
    ///     Use to clean up a single tag - trims and removes inner multi-space
    /// </summary>
    /// <param name="toClean"></param>
    /// <returns></returns>
    public static string TagListItemCleanup(string? toClean)
    {
        if (string.IsNullOrWhiteSpace(toClean)) return string.Empty;

        return Regex.Replace(SlugTools.CreateSpacedString(true, toClean, 200), @"\s+", " ").TrimNullToEmpty()
            .ToLower();
    }


    /// <summary>
    ///     Cleans and joins a list of tags into a string suitable for use as a database Tag value with this program's
    ///     conventions.
    /// </summary>
    /// <param name="tagList"></param>
    /// <returns></returns>
    public static string TagListJoin(List<string> tagList)
    {
        if (tagList.Count < 1) return string.Empty;

        var cleanedList = tagList.Select(TagListItemCleanup).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct()
            .OrderBy(x => x).ToList();

        return string.Join(",", cleanedList);
    }

    public static string TagListJoinAsSlugs(List<string> tagList, bool removeExcludedTags)
    {
        if (tagList.Count < 1) return string.Empty;

        var excludedTags = new List<string>();

        if (removeExcludedTags)
        {
            var db = Context().Result;
            excludedTags = db.TagExclusions.ToList().Select(x => SlugTools.CreateSlug(true, x.Tag, 200)).ToList();
        }

        var cleanedList = tagList.Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => SlugTools.CreateSlug(true, x.Trim(), 200)).Distinct().Except(excludedTags).OrderBy(x => x)
            .ToList();

        return string.Join(",", cleanedList);
    }

    /// <summary>
    ///     Converts a string into a List of Tags - resulting tags will be cleaned/converted according to program conventions
    /// </summary>
    /// <param name="rawTagString"></param>
    /// <returns></returns>
    public static List<string> TagListParse(string? rawTagString)
    {
        if (rawTagString == null) return [];
        if (string.IsNullOrWhiteSpace(rawTagString)) return [];

        return rawTagString.Split(",").Select(TagListItemCleanup).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct()
            .OrderBy(x => x).ToList();
    }

    /// <summary>
    ///     Takes an incoming string, parses and cleans tags according to program conventions, joins the tags
    ///     back into a string. This can be used to convert user input into a database
    ///     appropriate tag value. Note: the output of this method will be clean and correctly formatted but may not
    ///     be what the user intended - this method may be best used in a situation where the user has had
    ///     a preview of the converted content.
    /// </summary>
    /// <param name="toClean"></param>
    /// <returns></returns>
    public static string TagListParseCleanAndJoin(string? toClean)
    {
        return TagListJoin(TagListParse(toClean));
    }

    public static List<string> TagListParseToSlugs(string? rawTagString, bool removeExcludedTags)
    {
        if (rawTagString == null) return [];
        if (string.IsNullOrWhiteSpace(rawTagString)) return [];

        var excludedTags = new List<string>();

        if (removeExcludedTags)
        {
            var db = Context().Result;
            excludedTags = db.TagExclusions.ToList().Select(x => SlugTools.CreateSlug(true, x.Tag, 200)).ToList();
        }

        return rawTagString.Split(",").Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim())
            .Select(x => SlugTools.CreateSlug(true, x, 200)).Distinct().Where(x => !excludedTags.Contains(x))
            .OrderBy(x => x).ToList();
    }

    public static List<string> TagListParseToSlugs(ITag? tag, bool removeExcludedTags)
    {
        if (tag == null) return [];
        if (string.IsNullOrWhiteSpace(tag.Tags)) return [];

        return TagListParseToSlugs(tag.Tags, removeExcludedTags);
    }

    public static List<TagSlugAndIsExcluded> TagListParseToSlugsAndIsExcluded(ITag? tag)
    {
        if (tag == null) return [];
        if (string.IsNullOrWhiteSpace(tag.Tags)) return [];

        return TagListParseToSlugsAndIsExcluded(tag.Tags);
    }

    public static List<TagSlugAndIsExcluded> TagListParseToSlugsAndIsExcluded(string? rawTagString)
    {
        if (rawTagString == null) return [];
        if (string.IsNullOrWhiteSpace(rawTagString)) return [];

        var db = Context().Result;
        var excludedTags = db.TagExclusions.ToList().Select(x => SlugTools.CreateSlug(true, x.Tag, 200)).ToList();

        var tagSlugs = rawTagString.Split(",").Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim())
            .Select(x => SlugTools.CreateSlug(true, x, 200)).Distinct().ToList();

        return tagSlugs.Select(x => new TagSlugAndIsExcluded(x, excludedTags.Contains(x))).ToList();
    }

    public static async Task<List<(string tag, List<dynamic> contentObjects)>> TagSlugsAndContentList(
        bool includePagesExcludedFromSearch, bool removeExcludedTags, IProgress<string>? progress = null)
    {
        progress?.Report("Starting Parse of Tag Content");

        var tagBag = new ConcurrentBag<(string tag, List<dynamic> contentObjects)>();

        //!!Content List
        var taskSet = new List<Func<Task>>
        {
            async () =>
            {
                progress?.Report("Process File Content Tags");
                var db = await Context().ConfigureAwait(false);
                var toAdd = ParseToTagSlugsAndContentList(
                    includePagesExcludedFromSearch
                        ? (await db.FileContents.Where(x => !x.IsDraft).ToListAsync().ConfigureAwait(false))
                        .Cast<ITag>().ToList()
                        : (await db.FileContents.Where(x => !x.IsDraft && x.ShowInSearch).ToListAsync()
                            .ConfigureAwait(false)).Cast<ITag>().ToList(), removeExcludedTags, progress);
                toAdd.ForEach(x => tagBag.Add(x));
            },
            async () =>
            {
                progress?.Report("Process GeoJson Content Tags");
                var db = await Context().ConfigureAwait(false);
                var toAdd = ParseToTagSlugsAndContentList(
                    includePagesExcludedFromSearch
                        ? (await db.GeoJsonContents.Where(x => !x.IsDraft).ToListAsync()
                            .ConfigureAwait(false)).Cast<ITag>().ToList()
                        : (await db.GeoJsonContents.Where(x => !x.IsDraft && x.ShowInSearch).ToListAsync()
                            .ConfigureAwait(false)).Cast<ITag>().ToList()
                    , removeExcludedTags, progress);
                toAdd.ForEach(x => tagBag.Add(x));
            },
            async () =>
            {
                progress?.Report("Process Image Content Tags");
                var db = await Context().ConfigureAwait(false);
                var toAdd = ParseToTagSlugsAndContentList(
                    includePagesExcludedFromSearch
                        ? (await db.ImageContents.Where(x => !x.IsDraft).ToListAsync().ConfigureAwait(false))
                        .Cast<ITag>().ToList()
                        : (await db.ImageContents.Where(y => !y.IsDraft && y.ShowInSearch).ToListAsync()
                            .ConfigureAwait(false)).Cast<ITag>().ToList(), removeExcludedTags, progress);
                toAdd.ForEach(x => tagBag.Add(x));
            },
            async () =>
            {
                progress?.Report("Process Line Content Tags");
                var db = await Context().ConfigureAwait(false);
                var toAdd = ParseToTagSlugsAndContentList(
                    includePagesExcludedFromSearch
                        ? (await db.LineContents.Where(x => !x.IsDraft).ToListAsync().ConfigureAwait(false))
                        .Cast<ITag>()
                        .ToList()
                        : (await db.LineContents.Where(x => !x.IsDraft && x.ShowInSearch).ToListAsync()
                            .ConfigureAwait(false)).Cast<ITag>().ToList(), removeExcludedTags, progress);
                toAdd.ForEach(x => tagBag.Add(x));
            },
            async () =>
            {
                progress?.Report("Process Link Content Tags");
                var db = await Context().ConfigureAwait(false);
                var toAdd = ParseToTagSlugsAndContentList(
                    (await db.LinkContents.ToListAsync().ConfigureAwait(false)).Cast<ITag>().ToList(),
                    removeExcludedTags, progress);
                toAdd.ForEach(x => tagBag.Add(x));
            },
            async () =>
            {
                progress?.Report("Process Note Content Tags");
                var db = await Context().ConfigureAwait(false);
                var toAdd = ParseToTagSlugsAndContentList(
                    includePagesExcludedFromSearch
                        ? (await db.NoteContents.Where(x => !x.IsDraft).ToListAsync().ConfigureAwait(false))
                        .Cast<ITag>()
                        .ToList()
                        : (await db.NoteContents.Where(x => !x.IsDraft && x.ShowInSearch).ToListAsync()
                            .ConfigureAwait(false)).Cast<ITag>().ToList(), removeExcludedTags, progress);
                toAdd.ForEach(x => tagBag.Add(x));
            },
            async () =>
            {
                progress?.Report("Process Photo Content Tags");
                var db = await Context().ConfigureAwait(false);
                var toAdd = ParseToTagSlugsAndContentList(
                    includePagesExcludedFromSearch
                        ? (await db.PhotoContents.Where(x => !x.IsDraft).ToListAsync().ConfigureAwait(false))
                        .Cast<ITag>().ToList()
                        : (await db.PhotoContents.Where(x => !x.IsDraft && x.ShowInSearch).ToListAsync()
                            .ConfigureAwait(false)).Cast<ITag>().ToList(), removeExcludedTags, progress);
                toAdd.ForEach(x => tagBag.Add(x));
            },
            async () =>
            {
                progress?.Report("Process Point Content Tags");
                var db = await Context().ConfigureAwait(false);
                var toAdd = ParseToTagSlugsAndContentList(
                    includePagesExcludedFromSearch
                        ? (await db.PointContents.Where(x => !x.IsDraft).ToListAsync().ConfigureAwait(false))
                        .Cast<ITag>().ToList()
                        : (await db.PointContents.Where(x => !x.IsDraft && x.ShowInSearch).ToListAsync()
                            .ConfigureAwait(false)).Cast<ITag>().ToList(), removeExcludedTags, progress);
                toAdd.ForEach(x => tagBag.Add(x));
            },
            async () =>
            {
                progress?.Report("Process Post Content Tags");
                var db = await Context().ConfigureAwait(false);
                var toAdd = ParseToTagSlugsAndContentList(
                    includePagesExcludedFromSearch
                        ? (await db.PostContents.Where(x => !x.IsDraft).ToListAsync().ConfigureAwait(false))
                        .Cast<ITag>().ToList()
                        : (await db.PostContents.Where(x => !x.IsDraft && x.ShowInSearch).ToListAsync()
                            .ConfigureAwait(false)).Cast<ITag>().ToList(), removeExcludedTags, progress);
                toAdd.ForEach(x => tagBag.Add(x));
            },
            async () =>
            {
                progress?.Report("Process Trail Content Tags");
                var db = await Context().ConfigureAwait(false);
                var toAdd = ParseToTagSlugsAndContentList(
                    includePagesExcludedFromSearch
                        ? (await db.TrailContents.Where(x => !x.IsDraft).ToListAsync().ConfigureAwait(false))
                        .Cast<ITag>().ToList()
                        : (await db.TrailContents.Where(x => !x.IsDraft && x.ShowInSearch).ToListAsync()
                            .ConfigureAwait(false)).Cast<ITag>().ToList(), removeExcludedTags, progress);
                toAdd.ForEach(x => tagBag.Add(x));
            },
            async () =>
            {
                progress?.Report("Process Video Content Tags");
                var db = await Context().ConfigureAwait(false);
                var toAdd = ParseToTagSlugsAndContentList(
                    includePagesExcludedFromSearch
                        ? (await db.VideoContents.Where(x => !x.IsDraft).ToListAsync().ConfigureAwait(false))
                        .Cast<ITag>().ToList()
                        : (await db.VideoContents.Where(x => !x.IsDraft && x.ShowInSearch).ToListAsync()
                            .ConfigureAwait(false)).Cast<ITag>().ToList(), removeExcludedTags, progress);
                toAdd.ForEach(x => tagBag.Add(x));
            }
        };

        await Parallel.ForEachAsync(taskSet, async (x, _) => await x()).ConfigureAwait(false);

        var flattened = tagBag.ToList();

        var grouped = flattened.GroupBy(x => x.tag).Select(x => (x.Key, x.SelectMany(y => y.contentObjects).ToList()))
            .OrderBy(x => x.Key).ToList();

        progress?.Report("Finished Parsing Tag Content");

        return grouped;
    }

    public static async Task<List<PointContentDto>> ToPointContentDto(this List<PointContent> pointContents,
        PointlessWaymarksContext context)
    {
        var returnList = new List<PointContentDto>();

        foreach (var loopContent in pointContents) returnList.Add(await PointContentDtoFromPoint(loopContent, context));

        return returnList;
    }

    public static async Task<List<VideoContent>> VideoContentFromBoundingBox(this PointlessWaymarksContext db,
        SpatialBounds bounds)
    {
        var returnList = new List<VideoContent>();

        returnList.AddRange(await db.VideoContents.Where(x =>
            x.Latitude != null && x.Longitude != null
                               && x.Latitude >= bounds.MinLatitude
                               && x.Latitude <= bounds.MaxLatitude
                               && x.Longitude >= bounds.MinLongitude
                               && x.Longitude <= bounds.MaxLongitude).ToListAsync());

        return returnList;
    }

    public record TagSlugAndIsExcluded(string TagSlug, bool IsExcluded);
}