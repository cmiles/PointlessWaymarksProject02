using Microsoft.EntityFrameworkCore;
using PointlessWaymarks.CmsData.BracketCodes;
using PointlessWaymarks.CmsData.Database;
using PointlessWaymarks.CmsData.Database.Models;

namespace PointlessWaymarks.CmsData.CommonHtml;

public static class RelatedContentReferenceHelpers
{
    public static async Task ExtractAndWriteRelatedContentDbReferences(DateTime generationVersion,
        List<IContentCommon> content, PointlessWaymarksContext db, IProgress<string>? progress = null)
    {
        if (!content.Any()) return;

        foreach (var loopContent in content)
            await ExtractAndWriteRelatedContentDbReferences(generationVersion, loopContent, db, progress)
                .ConfigureAwait(false);
    }


    public static async Task ExtractAndWriteRelatedContentDbReferences(DateTime generationVersion,
        IContentCommon content, PointlessWaymarksContext db, IProgress<string>? progress = null)
    {
        var toAdd = new List<Guid>();

        if (content.MainPicture != null && content.MainPicture != content.ContentId)
            toAdd.Add(content.MainPicture.Value);

        var toSearch = string.Empty;

        if (content is TrailContent trail)
        {
            if (trail.LineContentId is not null) toAdd.Add(trail.LineContentId.Value);
            if (trail.MapComponentId is not null) toAdd.Add(trail.MapComponentId.Value);
            if (trail.StartingPointContentId is not null) toAdd.Add(trail.StartingPointContentId.Value);
            if (trail.EndingPointContentId is not null) toAdd.Add(trail.EndingPointContentId.Value);

            toSearch += trail.BikesNote + trail.DogsNote + trail.FeesNote +
                        trail.LocationArea + trail.OtherDetails + trail.TrailShape;
        }

        toSearch += content.BodyContent + content.Summary;

        if (content is GeoJsonContent geoContent) toSearch += geoContent.GeoJson;

        if (content is IUpdateNotes updateContent) toSearch += updateContent.UpdateNotes;

        if (string.IsNullOrWhiteSpace(toSearch) && !toAdd.Any()) return;

        toAdd.AddRange(BracketCodeCommon.BracketCodeContentIds(toSearch));

        if (!toAdd.Any()) return;

        var dbEntries = toAdd.Distinct().Select(x => new GenerationRelatedContent
        {
            ContentOne = content.ContentId, ContentTwo = x, GenerationVersion = generationVersion
        });

        await db.GenerationRelatedContents.AddRangeAsync(dbEntries).ConfigureAwait(false);

        await db.SaveChangesAsync().ConfigureAwait(false);
    }

    public static async Task ExtractAndWriteRelatedContentDbReferencesFromString(Guid sourceGuid, string toSearch,
        PointlessWaymarksContext db, IProgress<string>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(toSearch)) return;

        var toAdd = BracketCodeCommon.BracketCodeContentIds(toSearch);

        if (!toAdd.Any()) return;

        var dbEntries = toAdd.Select(x => new GenerationRelatedContent { ContentOne = sourceGuid, ContentTwo = x });

        await db.GenerationRelatedContents.AddRangeAsync(dbEntries).ConfigureAwait(false);
    }

    /// <summary>
    ///     A costly heavy-handed database query that will find a given contentId in any of the content tables.
    /// </summary>
    /// <param name="contentId"></param>
    /// <param name="progress"></param>
    /// <returns></returns>
    public static async Task<List<Guid>> FindContentUsing(Guid contentId, IProgress<string> progress)
    {
        var db = await Db.Context();

        var returnList = new List<Guid>();

        var guidString = contentId.ToString();

        progress.Report("Searching File Content...");
        var fileContentResults = (await db.FileContents.Where(x =>
                (x.MainPicture != null && x.MainPicture.Value == contentId) ||
                (x.BodyContent != null && x.BodyContent.Contains(guidString)) ||
                (x.Summary != null && x.Summary.Contains(guidString)) ||
                (x.UpdateNotes != null && x.UpdateNotes.Contains(guidString)))
            .Select(x => x.ContentId).ToListAsync()).Where(x => x != contentId).ToList();
        if(fileContentResults.Any()) progress.Report($"Found {fileContentResults.Count} references in Files");
        returnList.AddRange(fileContentResults);
        progress.Report($"Found {returnList.Distinct().Count()} total references after searching Files");

        progress.Report("Searching GeoJson Content...");
        var geoJsonContentResults = (await db.GeoJsonContents.Where(x =>
                (x.MainPicture != null && x.MainPicture.Value == contentId) ||
                (x.BodyContent != null && x.BodyContent.Contains(guidString)) ||
                (x.Summary != null && x.Summary.Contains(guidString)) ||
                (x.UpdateNotes != null && x.UpdateNotes.Contains(guidString)) ||
                (x.GeoJson != null && x.GeoJson.Contains(guidString)))
            .Select(x => x.ContentId).ToListAsync()).Where(x => x != contentId).ToList();
        if(geoJsonContentResults.Any()) progress.Report($"Found {geoJsonContentResults.Count} references in GeoJson");
        returnList.AddRange(geoJsonContentResults);
        progress.Report($"Found {returnList.Distinct().Count()} total references after searching GeoJson");

        progress.Report("Searching Line Content...");
        var lineContentResults = (await db.LineContents.Where(x =>
                (x.MainPicture != null && x.MainPicture.Value == contentId) ||
                (x.BodyContent != null && x.BodyContent.Contains(guidString)) ||
                (x.Summary != null && x.Summary.Contains(guidString)) ||
                (x.UpdateNotes != null && x.UpdateNotes.Contains(guidString)))
            .Select(x => x.ContentId).ToListAsync()).Where(x => x != contentId).ToList();
        if(lineContentResults.Any()) progress.Report($"Found {lineContentResults.Count} references in Lines");
        returnList.AddRange(lineContentResults);
        progress.Report($"Found {returnList.Distinct().Count()} total references after searching Lines");

        progress.Report("Searching Link Content...");
        var linkContentResults = (await db.LinkContents.Where(x =>
                (x.Comments != null && x.Comments.Contains(guidString)) ||
                (x.Description != null && x.Description.Contains(guidString)))
            .Select(x => x.ContentId).ToListAsync()).Where(x => x != contentId).ToList();
        if(linkContentResults.Any()) progress.Report($"Found {linkContentResults.Count} references in Links");
        returnList.AddRange(linkContentResults);
        progress.Report($"Found {returnList.Distinct().Count()} total references after searching Links");

        progress.Report("Searching Map Components...");
        var mapComponentResults = (await db.MapComponents.Where(x =>
                (x.Summary != null && x.Summary.Contains(guidString)) ||
                (x.UpdateNotes != null && x.UpdateNotes.Contains(guidString)))
            .Select(x => x.ContentId).ToListAsync()).Where(x => x != contentId).ToList();
        if(mapComponentResults.Any()) progress.Report($"Found {mapComponentResults.Count} references in Map Components");
        returnList.AddRange(mapComponentResults);
        progress.Report($"Found {returnList.Distinct().Count()} total references after searching Map Components");

        progress.Report("Searching Map Component Elements...");
        var mapElementResults = (await db.MapComponentElements.Where(x => x.ElementContentId == contentId)
            .Select(x => x.MapComponentContentId).ToListAsync()).Where(x => x != contentId).ToList();
        if(mapElementResults.Any()) progress.Report($"Found {mapElementResults.Count} references in Map Elements");
        returnList.AddRange(mapElementResults);
        progress.Report($"Found {returnList.Distinct().Count()} total references after searching Map Elements");

        progress.Report("Searching Note Content...");
        var noteContentResults = (await db.NoteContents.Where(x =>
                (x.BodyContent != null && x.BodyContent.Contains(guidString)) ||
                (x.Summary != null && x.Summary.Contains(guidString)))
            .Select(x => x.ContentId).ToListAsync()).Where(x => x != contentId).ToList();
        if(noteContentResults.Any()) progress.Report($"Found {noteContentResults.Count} references in Notes");
        returnList.AddRange(noteContentResults);
        progress.Report($"Found {returnList.Distinct().Count()} total references after searching Notes");

        progress.Report("Searching Photo Content...");
        var photoContentResults = (await db.PhotoContents.Where(x =>
                (x.MainPicture != null && x.MainPicture.Value == contentId) ||
                (x.BodyContent != null && x.BodyContent.Contains(guidString)) ||
                (x.Summary != null && x.Summary.Contains(guidString)) ||
                (x.UpdateNotes != null && x.UpdateNotes.Contains(guidString)))
            .Select(x => x.ContentId).ToListAsync()).Where(x => x != contentId).ToList();
        if(photoContentResults.Any()) progress.Report($"Found {photoContentResults.Count} references in Photos");
        returnList.AddRange(photoContentResults);
        progress.Report($"Found {returnList.Distinct().Count()} total references after searching Photos");

        progress.Report("Searching Point Content...");
        var pointContentResults = (await db.PointContents.Where(x =>
                (x.MainPicture != null && x.MainPicture.Value == contentId) ||
                (x.BodyContent != null && x.BodyContent.Contains(guidString)) ||
                (x.Summary != null && x.Summary.Contains(guidString)) ||
                (x.UpdateNotes != null && x.UpdateNotes.Contains(guidString)))
            .Select(x => x.ContentId).ToListAsync()).Where(x => x != contentId).ToList();
        if(pointContentResults.Any()) progress.Report($"Found {pointContentResults.Count} references in Points");
        returnList.AddRange(pointContentResults);
        progress.Report($"Found {returnList.Distinct().Count()} total references after searching Points");

        progress.Report("Searching Post Content...");
        var postContentResults = (await db.PostContents.Where(x =>
                (x.MainPicture != null && x.MainPicture.Value == contentId) ||
                (x.BodyContent != null && x.BodyContent.Contains(guidString)) ||
                (x.Summary != null && x.Summary.Contains(guidString)) ||
                (x.UpdateNotes != null && x.UpdateNotes.Contains(guidString)))
            .Select(x => x.ContentId).ToListAsync()).Where(x => x != contentId).ToList();
        if(postContentResults.Any()) progress.Report($"Found {postContentResults.Count} references in Posts");
        returnList.AddRange(postContentResults);
        progress.Report($"Found {returnList.Distinct().Count()} total references after searching Posts");

        progress.Report("Searching Trail Content...");
        var trailContentResults = (await db.TrailContents.Where(x =>
                (x.MainPicture != null && x.MainPicture.Value == contentId) ||
                (x.LineContentId != null && x.LineContentId.Value == contentId) ||
                (x.MapComponentId != null && x.MapComponentId.Value == contentId) ||
                (x.StartingPointContentId != null && x.StartingPointContentId.Value == contentId) ||
                (x.EndingPointContentId != null && x.EndingPointContentId.Value == contentId) ||
                (x.BikesNote != null && x.BikesNote.Contains(guidString)) ||
                (x.DogsNote != null && x.DogsNote.Contains(guidString)) ||
                (x.FeesNote != null && x.FeesNote.Contains(guidString)) ||
                (x.LocationArea != null && x.LocationArea.Contains(guidString)) ||
                (x.OtherDetails != null && x.OtherDetails.Contains(guidString)) ||
                (x.TrailShape != null && x.TrailShape.Contains(guidString)) ||
                (x.BodyContent != null && x.BodyContent.Contains(guidString)) ||
                (x.Summary != null && x.Summary.Contains(guidString)) ||
                (x.UpdateNotes != null && x.UpdateNotes.Contains(guidString)))
            .Select(x => x.ContentId).ToListAsync()).Where(x => x != contentId).ToList();
        if(trailContentResults.Any()) progress.Report($"Found {trailContentResults.Count} references in Trails");
        returnList.AddRange(trailContentResults);
        progress.Report($"Found {returnList.Distinct().Count()} total references after searching Trails");

        progress.Report("Searching Video Content...");
        var videoContentResults = (await db.VideoContents.Where(x =>
                (x.MainPicture != null && x.MainPicture.Value == contentId) ||
                (x.BodyContent != null && x.BodyContent.Contains(guidString)) ||
                (x.Summary != null && x.Summary.Contains(guidString)) ||
                (x.UpdateNotes != null && x.UpdateNotes.Contains(guidString)))
            .Select(x => x.ContentId).ToListAsync()).Where(x => x != contentId).ToList();
        if(videoContentResults.Any()) progress.Report($"Found {videoContentResults.Count} references in Videos");
        returnList.AddRange(videoContentResults);
        progress.Report($"Found {returnList.Distinct().Count()} total references after searching Videos");

        var distinctList = returnList.Distinct().ToList();
        progress.Report($"Found {distinctList.Count} total distinct references.");
        return distinctList;
    }

    public static async Task GenerateRelatedContentDbTable(DateTime generationVersion,
        IProgress<string>? progress = null)
    {
        //!!Content Type List!!
        var taskFunctionList = new List<Func<Task>>
        {
            async () =>
            {
                var db = await Db.Context().ConfigureAwait(false);
                var files = (await db.FileContents.Where(x => !x.IsDraft).ToListAsync().ConfigureAwait(false))
                    .Cast<IContentCommon>().ToList();
                progress?.Report($"Processing {files.Count} File Content Entries for Related Content");
                await ExtractAndWriteRelatedContentDbReferences(generationVersion, files, db, progress)
                    .ConfigureAwait(false);
            },
            async () =>
            {
                var db = await Db.Context().ConfigureAwait(false);
                var geoJson = (await db.GeoJsonContents.Where(x => !x.IsDraft).ToListAsync().ConfigureAwait(false))
                    .Cast<IContentCommon>().ToList();
                progress?.Report($"Processing {geoJson.Count} GeoJson Content Entries for Related Content");
                await ExtractAndWriteRelatedContentDbReferences(generationVersion, geoJson, db, progress)
                    .ConfigureAwait(false);
            },
            async () =>
            {
                var db = await Db.Context().ConfigureAwait(false);
                var images = (await db.ImageContents.Where(x => !x.IsDraft).ToListAsync().ConfigureAwait(false))
                    .Cast<IContentCommon>().ToList();
                progress?.Report($"Processing {images.Count} Image Content Entries for Related Content");
                await ExtractAndWriteRelatedContentDbReferences(generationVersion, images, db, progress)
                    .ConfigureAwait(false);
            },
            async () =>
            {
                var db = await Db.Context().ConfigureAwait(false);
                var lines = (await db.LineContents.Where(x => !x.IsDraft).ToListAsync().ConfigureAwait(false))
                    .Cast<IContentCommon>().ToList();
                progress?.Report($"Processing {lines.Count} Line Content Entries for Related Content");
                await ExtractAndWriteRelatedContentDbReferences(generationVersion, lines, db, progress)
                    .ConfigureAwait(false);
            },
            async () =>
            {
                var db = await Db.Context().ConfigureAwait(false);
                var links = await db.LinkContents
                    .Select(x => new { x.ContentId, toCheck = x.Comments + x.Description })
                    .ToListAsync().ConfigureAwait(false);
                progress?.Report($"Processing {links.Count} Link Content Entries for Related Content");
                foreach (var loopLink in links)
                    await ExtractAndWriteRelatedContentDbReferencesFromString(loopLink.ContentId, loopLink.toCheck,
                        db,
                        progress).ConfigureAwait(false);
            },
            async () =>
            {
                var db = await Db.Context().ConfigureAwait(false);
                var notes = (await db.NoteContents.Where(x => !x.IsDraft).ToListAsync().ConfigureAwait(false))
                    .Cast<IContentCommon>().ToList();
                progress?.Report($"Processing {notes.Count} Note Content Entries for Related Content");
                await ExtractAndWriteRelatedContentDbReferences(generationVersion, notes, db, progress)
                    .ConfigureAwait(false);
            },
            async () =>
            {
                var db = await Db.Context().ConfigureAwait(false);
                var photos = (await db.PhotoContents.Where(x => !x.IsDraft).ToListAsync().ConfigureAwait(false))
                    .Cast<IContentCommon>().ToList();
                progress?.Report($"Processing {photos.Count} Photo Content Entries for Related Content");
                await ExtractAndWriteRelatedContentDbReferences(generationVersion, photos, db, progress)
                    .ConfigureAwait(false);
            },
            async () =>
            {
                var db = await Db.Context().ConfigureAwait(false);
                var points = (await db.PointContents.Where(x => !x.IsDraft).ToListAsync().ConfigureAwait(false))
                    .Cast<IContentCommon>().ToList();
                progress?.Report($"Processing {points.Count} Point Content Entries for Related Content");
                await ExtractAndWriteRelatedContentDbReferences(generationVersion, points, db, progress)
                    .ConfigureAwait(false);
            },
            async () =>
            {
                var db = await Db.Context().ConfigureAwait(false);
                var posts = (await db.PostContents.Where(x => !x.IsDraft).ToListAsync().ConfigureAwait(false))
                    .Cast<IContentCommon>().ToList();
                progress?.Report($"Processing {posts.Count} Post Content Entries for Related Content");
                await ExtractAndWriteRelatedContentDbReferences(generationVersion, posts, db, progress)
                    .ConfigureAwait(false);
            },
            async () =>
            {
                var db = await Db.Context().ConfigureAwait(false);
                var trails = (await db.TrailContents.Where(x => !x.IsDraft).ToListAsync().ConfigureAwait(false))
                    .Cast<IContentCommon>().ToList();
                progress?.Report($"Processing {trails.Count} Trail Content Entries for Related Content");
                await ExtractAndWriteRelatedContentDbReferences(generationVersion, trails, db, progress)
                    .ConfigureAwait(false);
            },
            async () =>
            {
                var db = await Db.Context().ConfigureAwait(false);
                var videos = (await db.VideoContents.Where(x => !x.IsDraft).ToListAsync().ConfigureAwait(false))
                    .Cast<IContentCommon>().ToList();
                progress?.Report($"Processing {videos.Count} Video Content Entries for Related Content");
                await ExtractAndWriteRelatedContentDbReferences(generationVersion, videos, db, progress)
                    .ConfigureAwait(false);
            }
        };

        var taskList = taskFunctionList.Select(Task.Run).ToList();

        await Task.WhenAll(taskList).ConfigureAwait(false);
    }
}