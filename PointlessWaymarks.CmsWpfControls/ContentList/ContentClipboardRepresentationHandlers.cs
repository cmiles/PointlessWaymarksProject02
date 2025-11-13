using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using PointlessWaymarks.CmsData;
using PointlessWaymarks.CmsData.BracketCodes;
using PointlessWaymarks.CmsData.ContentGeneration;
using PointlessWaymarks.CmsData.Database;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.CmsData.Server;
using PointlessWaymarks.CmsWpfControls.FileContentEditor;
using PointlessWaymarks.CmsWpfControls.GeoJsonContentEditor;
using PointlessWaymarks.CmsWpfControls.ImageContentEditor;
using PointlessWaymarks.CmsWpfControls.LineContentEditor;
using PointlessWaymarks.CmsWpfControls.LinkContentEditor;
using PointlessWaymarks.CmsWpfControls.NoteContentEditor;
using PointlessWaymarks.CmsWpfControls.PhotoContentEditor;
using PointlessWaymarks.CmsWpfControls.PostContentEditor;
using PointlessWaymarks.CmsWpfControls.SnippetEditor;
using PointlessWaymarks.CmsWpfControls.TrailContentEditor;
using PointlessWaymarks.CmsWpfControls.VideoContentEditor;
using PointlessWaymarks.CommonTools.S3;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.Status;
using PointlessWaymarks.WpfCommon.Utility;
using Serilog;

namespace PointlessWaymarks.CmsWpfControls.ContentList;

//TODO: Validate any Bracket Codes in Point Details?
public static class ContentClipboardRepresentationHandlers
{
    public static async Task CopyLinkSnapshotImagesFromRemote(Guid contentId, string remoteApiBaseUrl,
        StatusControlContext statusContext)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var httpClient = new HttpClient();
        var localArchiveDir = UserSettingsSingleton.CurrentSettings().LocalMediaArchiveLinkDirectory();

        // Get the list of files from the remote API
        var snapshotsApiUrl = $"{remoteApiBaseUrl}/localapi/linksnapshots/{contentId}";
        statusContext.Progress($"Requesting link snapshot images for {contentId} from {snapshotsApiUrl}");

        var response = await httpClient.GetAsync(snapshotsApiUrl);

        if (!response.IsSuccessStatusCode)
        {
            await statusContext.ToastError(
                $"Failed to retrieve link snapshot images for {contentId}: {response.ReasonPhrase}");
            return;
        }

        // The API returns a zip file if there are files
        var zipFileName = $"LinkSnapshots_{contentId}.zip";
        var tempZipPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}_{zipFileName}");

        await using (var fs = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await response.Content.CopyToAsync(fs);
        }

        // Extract and copy files
        using (var archive = new ZipArchive(new FileStream(tempZipPath, FileMode.Open, FileAccess.Read),
                   ZipArchiveMode.Read))
        {
            foreach (var entry in archive.Entries)
            {
                var destFilePath = Path.Combine(localArchiveDir.FullName, entry.Name);

                if (File.Exists(destFilePath))
                {
                    statusContext.Progress($"File already exists, skipping: {destFilePath}");
                    continue;
                }

                statusContext.Progress($"Copying snapshot image: {entry.Name} to {destFilePath}");
                entry.ExtractToFile(destFilePath);
            }
        }

        // Clean up temp zip
        try
        {
            File.Delete(tempZipPath);
        }
        catch
        {
            /* ignore */
        }

        await statusContext.ToastSuccess($"Finished copying link snapshot images for {contentId}");
    }

    private static string FakeMainPhotoBracketCode(Guid? mainPhoto)
    {
        if (mainPhoto == null) return string.Empty;
        return
            $"{{{{{BracketCodePhotos.BracketCodeToken} {mainPhoto.ToString()}; Fake Bracket Code for Content Ref Import}}}}";
    }

    public static async Task<List<string>> HandleFileContentReferences(List<ContentClipboardRepresentation> contentRefs,
        StatusControlContext statusContext)
    {
        var errors = new List<string>();
        await ThreadSwitcher.ResumeBackgroundAsync();

        statusContext.Progress("Starting file content load.");

        var loopCount = 0;

        using var httpClient = new HttpClient();

        foreach (var loopRef in contentRefs)
        {
            loopCount++;

            statusContext.Progress($"Processing {loopCount} of {contentRefs.Count} Files from Clipboard");

            await ThreadSwitcher.ResumeBackgroundAsync();

            var db = await Db.Context();

            try
            {
                // Get the content JSON from the source site's API
                var contentJsonUrl = $"{loopRef.SiteLocalApiUrl}/contentjson/{loopRef.ContentId}";
                var contentJsonResponse = await httpClient.GetStringAsync(contentJsonUrl);

                if (string.IsNullOrEmpty(contentJsonResponse))
                {
                    var err = $"Failed to retrieve content data for {loopRef.ContentId}";
                    await statusContext.ToastError(err);
                    errors.Add(err);
                    continue;
                }

                // Deserialize content data
                var fileContent = JsonSerializer.Deserialize<FileContent>(contentJsonResponse,
                    JsonTools.WriteIndentedOptions);

                if (fileContent == null)
                {
                    var err = $"Failed to parse content data for {loopRef.ContentId}";
                    await statusContext.ToastError(err);
                    errors.Add(err);
                    continue;
                }

                fileContent.Id = 0;

                // Get the media file information for the content
                var mediaFileUrl = $"{loopRef.SiteLocalApiUrl}/mediafile/{loopRef.ContentId}";
                var mediaFileResponse = await httpClient.GetStringAsync(mediaFileUrl);

                if (string.IsNullOrEmpty(mediaFileResponse))
                {
                    var err = $"Failed to retrieve media file data for {loopRef.ContentId}";
                    await statusContext.ToastError(err);
                    errors.Add(err);
                    continue;
                }

                // Parse the media file information
                var mediaFileInfo = JsonSerializer.Deserialize<ContentMediaFileResponse>(
                    mediaFileResponse,
                    JsonTools.WriteIndentedOptions);

                if (mediaFileInfo is not { Exists: true })
                {
                    var err = $"Media file not found for {loopRef.ContentId}";
                    await statusContext.ToastError(err);
                    errors.Add(err);
                    continue;
                }

                var fileFile = new FileInfo(mediaFileInfo.FullPath);

                var (saveGenerationReturn, _) = await FileGenerator.SaveAndGenerateHtml(fileContent, fileFile,
                    null, statusContext.ProgressTracker());

                var bracketCodeCheck = await CommonContentValidation.CheckStringForBadContentReferences(
                    $"{fileContent.BodyContent ?? string.Empty} {fileContent.UpdateNotes ?? string.Empty} {FakeMainPhotoBracketCode(fileContent.MainPicture)}",
                    db,
                    statusContext.ProgressTracker());

                if (saveGenerationReturn.HasError || bracketCodeCheck.HasError)
                {
                    var editor =
                        await FileContentEditorWindow.CreateInstance(fileContent, skipMetadataLoadFromFile: true);
                    await editor.PositionWindowAndShowOnUiThread();

                    //Allow execution to continue so Automation can continue
                    _ = editor.StatusContext.ShowMessageWithOkButton("Problem Saving",
                        saveGenerationReturn.GenerationNote);

                    errors.Add(
                        $"Error saving or validating file content {loopRef.ContentId}: {saveGenerationReturn.GenerationNote} {bracketCodeCheck.GenerationNote}");
                    continue;
                }

                statusContext.Progress($"New File Editor - based on {loopRef.ContentId} ");

                await ThreadSwitcher.ResumeBackgroundAsync();
            }
            catch (Exception ex)
            {
                var err = $"Error processing content {loopRef.ContentId}: {ex.Message}";
                await statusContext.ToastError(err);
                Log.Error(ex, "Error processing content from other site: {ContentId}", loopRef.ContentId);
                errors.Add(err);
            }
        }

        return errors;
    }

    public static async Task<List<string>> HandleGeoJsonContentReferences(
        List<ContentClipboardRepresentation> contentRefs,
        StatusControlContext statusContext)
    {
        var errors = new List<string>();
        await ThreadSwitcher.ResumeBackgroundAsync();

        statusContext.Progress("Starting GeoJson content load.");

        var loopCount = 0;

        using var httpClient = new HttpClient();

        foreach (var loopRef in contentRefs)
        {
            loopCount++;

            statusContext.Progress($"Processing {loopCount} of {contentRefs.Count} GeoJson items from Clipboard");

            await ThreadSwitcher.ResumeBackgroundAsync();

            var db = await Db.Context();

            try
            {
                // Get the content JSON from the source site's API
                var contentJsonUrl = $"{loopRef.SiteLocalApiUrl}/contentjson/{loopRef.ContentId}";
                var contentJsonResponse = await httpClient.GetStringAsync(contentJsonUrl);

                if (string.IsNullOrEmpty(contentJsonResponse))
                {
                    var err = $"Failed to retrieve content data for {loopRef.ContentId}";
                    await statusContext.ToastError(err);
                    errors.Add(err);
                    continue;
                }

                // Deserialize content data
                var geoJsonContent = JsonSerializer.Deserialize<GeoJsonContent>(contentJsonResponse,
                    JsonTools.WriteIndentedOptions);

                if (geoJsonContent == null)
                {
                    var err = $"Failed to parse content data for {loopRef.ContentId}";
                    await statusContext.ToastError(err);
                    errors.Add(err);
                    continue;
                }

                geoJsonContent.Id = 0;

                var (saveGenerationReturn, _) = await GeoJsonGenerator.SaveAndGenerateHtml(geoJsonContent,
                    null, statusContext.ProgressTracker());

                var bracketCodeCheck = await CommonContentValidation.CheckStringForBadContentReferences(
                    $"{geoJsonContent.BodyContent ?? string.Empty} {geoJsonContent.UpdateNotes ?? string.Empty} {FakeMainPhotoBracketCode(geoJsonContent.MainPicture)}",
                    db,
                    statusContext.ProgressTracker());

                if (saveGenerationReturn.HasError || bracketCodeCheck.HasError)
                {
                    var editor = await GeoJsonContentEditorWindow.CreateInstance(geoJsonContent);
                    await editor.PositionWindowAndShowOnUiThread();

                    //Allow execution to continue so Automation can continue
                    _ = editor.StatusContext.ShowMessageWithOkButton("Problem Saving",
                        saveGenerationReturn.GenerationNote);

                    errors.Add(
                        $"Error saving or validating GeoJson content {loopRef.ContentId}: {saveGenerationReturn.GenerationNote} {bracketCodeCheck.GenerationNote}");
                    continue;
                }

                statusContext.Progress($"New GeoJson Editor - based on {loopRef.ContentId} ");

                await ThreadSwitcher.ResumeBackgroundAsync();
            }
            catch (Exception ex)
            {
                var err = $"Error processing content {loopRef.ContentId}: {ex.Message}";
                await statusContext.ToastError(err);
                Log.Error(ex, "Error processing content from other site: {ContentId}", loopRef.ContentId);
                errors.Add(err);
            }
        }

        return errors;
    }

    public static async Task<List<string>> HandleImageContentReferences(
        List<ContentClipboardRepresentation> contentRefs,
        StatusControlContext statusContext)
    {
        var errors = new List<string>();
        await ThreadSwitcher.ResumeBackgroundAsync();

        statusContext.Progress("Starting image content load.");

        var loopCount = 0;

        using var httpClient = new HttpClient();

        foreach (var loopRef in contentRefs)
        {
            loopCount++;

            statusContext.Progress($"Processing {loopCount} of {contentRefs.Count} Images from Clipboard");

            await ThreadSwitcher.ResumeBackgroundAsync();

            var db = await Db.Context();

            try
            {
                // Get the content JSON from the source site's API
                var contentJsonUrl = $"{loopRef.SiteLocalApiUrl}/contentjson/{loopRef.ContentId}";
                var contentJsonResponse = await httpClient.GetStringAsync(contentJsonUrl);

                if (string.IsNullOrEmpty(contentJsonResponse))
                {
                    var err = $"Failed to retrieve content data for {loopRef.ContentId}";
                    await statusContext.ToastError(err);
                    errors.Add(err);
                    continue;
                }

                // Deserialize content data
                var imageContent = JsonSerializer.Deserialize<ImageContent>(contentJsonResponse,
                    JsonTools.WriteIndentedOptions);

                if (imageContent == null)
                {
                    var err = $"Failed to parse content data for {loopRef.ContentId}";
                    await statusContext.ToastError(err);
                    errors.Add(err);
                    continue;
                }

                imageContent.Id = 0;

                // Get the media file information for the content
                var mediaFileUrl = $"{loopRef.SiteLocalApiUrl}/mediafile/{loopRef.ContentId}";
                var mediaFileResponse = await httpClient.GetStringAsync(mediaFileUrl);

                if (string.IsNullOrEmpty(mediaFileResponse))
                {
                    var err = $"Failed to retrieve media file data for {loopRef.ContentId}";
                    await statusContext.ToastError(err);
                    errors.Add(err);
                    continue;
                }

                // Parse the media file information
                var mediaFileInfo = JsonSerializer.Deserialize<ContentMediaFileResponse>(
                    mediaFileResponse,
                    JsonTools.WriteIndentedOptions);

                if (mediaFileInfo is not { Exists: true })
                {
                    var err = $"Media file not found for {loopRef.ContentId}";
                    await statusContext.ToastError(err);
                    errors.Add(err);
                    continue;
                }

                var imageFile = new FileInfo(mediaFileInfo.FullPath);

                var (saveGenerationReturn, _) = await ImageGenerator.SaveAndGenerateHtml(imageContent, imageFile, true,
                    null, statusContext.ProgressTracker());

                var bracketCodeCheck = await CommonContentValidation.CheckStringForBadContentReferences(
                    $"{imageContent.BodyContent ?? string.Empty} {imageContent.UpdateNotes ?? string.Empty} {FakeMainPhotoBracketCode(imageContent.MainPicture)}",
                    db,
                    statusContext.ProgressTracker());

                if (saveGenerationReturn.HasError || bracketCodeCheck.HasError)
                {
                    var editor =
                        await ImageContentEditorWindow.CreateInstance(imageContent, imageFile,
                            skipImageMetadataLoad: true);
                    await editor.PositionWindowAndShowOnUiThread();

                    //Allow execution to continue so Automation can continue
                    _ = editor.StatusContext.ShowMessageWithOkButton("Problem Saving",
                        saveGenerationReturn.GenerationNote);

                    errors.Add(
                        $"Error saving or validating image content {loopRef.ContentId}: {saveGenerationReturn.GenerationNote} {bracketCodeCheck.GenerationNote}");
                    continue;
                }

                statusContext.Progress($"New Image Editor - based on {loopRef.ContentId} ");

                await ThreadSwitcher.ResumeBackgroundAsync();
            }
            catch (Exception ex)
            {
                var err = $"Error processing content {loopRef.ContentId}: {ex.Message}";
                await statusContext.ToastError(err);
                Log.Error(ex, "Error processing content from other site: {ContentId}", loopRef.ContentId);
                errors.Add(err);
            }
        }

        return errors;
    }

    public static async Task<List<string>> HandleLineContentReferences(List<ContentClipboardRepresentation> contentRefs,
        StatusControlContext statusContext)
    {
        var errors = new List<string>();
        await ThreadSwitcher.ResumeBackgroundAsync();

        statusContext.Progress("Starting line content load.");

        var loopCount = 0;

        using var httpClient = new HttpClient();

        foreach (var loopRef in contentRefs)
        {
            loopCount++;

            statusContext.Progress($"Processing {loopCount} of {contentRefs.Count} Lines from Clipboard");

            await ThreadSwitcher.ResumeBackgroundAsync();

            var db = await Db.Context();

            try
            {
                // Get the content JSON from the source site's API
                var contentJsonUrl = $"{loopRef.SiteLocalApiUrl}/contentjson/{loopRef.ContentId}";
                var contentJsonResponse = await httpClient.GetStringAsync(contentJsonUrl);

                if (string.IsNullOrEmpty(contentJsonResponse))
                {
                    var err = $"Failed to retrieve content data for {loopRef.ContentId}";
                    await statusContext.ToastError(err);
                    errors.Add(err);
                    continue;
                }

                // Deserialize content data
                var lineContent = JsonSerializer.Deserialize<LineContent>(contentJsonResponse,
                    JsonTools.WriteIndentedOptions);

                if (lineContent == null)
                {
                    var err = $"Failed to parse content data for {loopRef.ContentId}";
                    await statusContext.ToastError(err);
                    errors.Add(err);
                    continue;
                }

                lineContent.Id = 0;

                var (saveGenerationReturn, _) = await LineGenerator.SaveAndGenerateHtml(lineContent,
                    null, statusContext.ProgressTracker());

                var bracketCodeCheck = await CommonContentValidation.CheckStringForBadContentReferences(
                    $"{lineContent.BodyContent ?? string.Empty} {lineContent.UpdateNotes ?? string.Empty} {FakeMainPhotoBracketCode(lineContent.MainPicture)}",
                    db,
                    statusContext.ProgressTracker());

                if (saveGenerationReturn.HasError || bracketCodeCheck.HasError)
                {
                    var editor = await LineContentEditorWindow.CreateInstance(lineContent);
                    await editor.PositionWindowAndShowOnUiThread();

                    //Allow execution to continue so Automation can continue
                    _ = editor.StatusContext.ShowMessageWithOkButton("Problem Saving",
                        saveGenerationReturn.GenerationNote);

                    errors.Add(
                        $"Error saving or validating line content {loopRef.ContentId}: {saveGenerationReturn.GenerationNote} {bracketCodeCheck.GenerationNote}");
                    continue;
                }

                statusContext.Progress($"New Line Editor - based on {loopRef.ContentId} ");

                await ThreadSwitcher.ResumeBackgroundAsync();
            }
            catch (Exception ex)
            {
                var err = $"Error processing content {loopRef.ContentId}: {ex.Message}";
                await statusContext.ToastError(err);
                Log.Error(ex, "Error processing content from other site: {ContentId}", loopRef.ContentId);
                errors.Add(err);
            }
        }

        return errors;
    }

    public static async Task<List<string>> HandleLinkContentReferences(List<ContentClipboardRepresentation> contentRefs,
        StatusControlContext statusContext)
    {
        var errors = new List<string>();
        await ThreadSwitcher.ResumeBackgroundAsync();

        statusContext.Progress("Starting link content load.");

        var loopCount = 0;

        using var httpClient = new HttpClient();

        foreach (var loopRef in contentRefs)
        {
            loopCount++;

            statusContext.Progress($"Processing {loopCount} of {contentRefs.Count} Links from Clipboard");

            await ThreadSwitcher.ResumeBackgroundAsync();

            var db = await Db.Context();

            try
            {
                // Get the content JSON from the source site's API
                var contentJsonUrl = $"{loopRef.SiteLocalApiUrl}/contentjson/{loopRef.ContentId}";
                var contentJsonResponse = await httpClient.GetStringAsync(contentJsonUrl);

                if (string.IsNullOrEmpty(contentJsonResponse))
                {
                    var err = $"Failed to retrieve content data for {loopRef.ContentId}";
                    await statusContext.ToastError(err);
                    errors.Add(err);
                    continue;
                }

                // Deserialize content data
                var linkContent = JsonSerializer.Deserialize<LinkContent>(contentJsonResponse,
                    JsonTools.WriteIndentedOptions);

                if (linkContent == null)
                {
                    var err = $"Failed to parse content data for {loopRef.ContentId}";
                    await statusContext.ToastError(err);
                    errors.Add(err);
                    continue;
                }

                linkContent.Id = 0;

                await CopyLinkSnapshotImagesFromRemote(linkContent.ContentId, loopRef.SiteLocalApiUrl, statusContext);

                var (saveGenerationReturn, _) = await LinkGenerator.SaveAndGenerateHtml(linkContent,
                    null, statusContext.ProgressTracker());

                var bracketCodeCheck = await CommonContentValidation.CheckStringForBadContentReferences(
                    $"{linkContent.Comments ?? string.Empty}", db,
                    statusContext.ProgressTracker());

                if (saveGenerationReturn.HasError || bracketCodeCheck.HasError)
                {
                    var editor = await LinkContentEditorWindow.CreateInstance(linkContent);
                    await editor.PositionWindowAndShowOnUiThread();

                    //Allow execution to continue so Automation can continue
                    _ = editor.StatusContext.ShowMessageWithOkButton("Problem Saving",
                        saveGenerationReturn.GenerationNote);

                    errors.Add(
                        $"Error saving or validating link content {loopRef.ContentId}: {saveGenerationReturn.GenerationNote} {bracketCodeCheck.GenerationNote}");
                    continue;
                }

                statusContext.Progress($"New Link Editor - based on {loopRef.ContentId} ");

                await ThreadSwitcher.ResumeBackgroundAsync();
            }
            catch (Exception ex)
            {
                var err = $"Error processing content {loopRef.ContentId}: {ex.Message}";
                await statusContext.ToastError(err);
                Log.Error(ex, "Error processing content from other site: {ContentId}", loopRef.ContentId);
                errors.Add(err);
            }
        }

        return errors;
    }

    public static async Task<List<string>> HandleMapContentReferences(
        List<ContentClipboardRepresentation> contentRefs,
        StatusControlContext statusContext)
    {
        var errors = new List<string>();
        await ThreadSwitcher.ResumeBackgroundAsync();

        statusContext.Progress("Starting map content load.");

        var loopCount = 0;

        using var httpClient = new HttpClient();

        foreach (var loopRef in contentRefs)
        {
            loopCount++;

            statusContext.Progress($"Processing {loopCount} of {contentRefs.Count} Maps from Clipboard");

            await ThreadSwitcher.ResumeBackgroundAsync();

            var db = await Db.Context();
            
            try
            {
                // Get the MapDto from the source site's API
                var mapJsonUrl = $"{loopRef.SiteLocalApiUrl}/mapcomponentdto/{loopRef.ContentId}";
                var mapJsonResponse = await httpClient.GetStringAsync(mapJsonUrl);

                if (string.IsNullOrEmpty(mapJsonResponse))
                {
                    var err = $"Failed to retrieve map data for {loopRef.ContentId}";
                    await statusContext.ToastError(err);
                    errors.Add(err);
                    continue;
                }

                // Deserialize MapDto
                var mapDto =
                    JsonSerializer.Deserialize<MapComponentDto>(mapJsonResponse, JsonTools.WriteIndentedOptions);

                if (mapDto == null)
                {
                    var err = $"Failed to parse map data for {loopRef.ContentId}";
                    await statusContext.ToastError(err);
                    errors.Add(err);
                    continue;
                }

                mapDto.Id = 0;
                mapDto.Elements.ForEach(x => x.Id = 0);

                // Save and generate HTML for the map
                var (saveGenerationReturn, _) =
                    await MapComponentGenerator.SaveAndGenerateData(mapDto, null, statusContext.ProgressTracker());

                var bracketCodeCheck = await CommonContentValidation.CheckStringForBadContentReferences(
                    $"{mapDto.UpdateNotes ?? string.Empty}", db,
                    statusContext.ProgressTracker());

                if (saveGenerationReturn.HasError || bracketCodeCheck.HasError)
                {
                    errors.Add(
                        $"Error saving or validating map content {loopRef.ContentId}: {saveGenerationReturn.GenerationNote} {bracketCodeCheck.GenerationNote}");
                    continue;
                }

                statusContext.Progress($"Imported Map - based on {loopRef.ContentId}");

                await ThreadSwitcher.ResumeBackgroundAsync();
            }
            catch (Exception ex)
            {
                var err = $"Error processing map content {loopRef.ContentId}: {ex.Message}";
                await statusContext.ToastError(err);
                Log.Error(ex, "Error processing map content from other site: {ContentId}", loopRef.ContentId);
                errors.Add(err);
            }
        }

        return errors;
    }

    public static async Task<List<string>> HandleNoteContentReferences(List<ContentClipboardRepresentation> contentRefs,
        StatusControlContext statusContext)
    {
        var errors = new List<string>();
        await ThreadSwitcher.ResumeBackgroundAsync();

        statusContext.Progress("Starting note content load.");

        var loopCount = 0;

        using var httpClient = new HttpClient();

        foreach (var loopRef in contentRefs)
        {
            loopCount++;

            statusContext.Progress($"Processing {loopCount} of {contentRefs.Count} Notes from Clipboard");

            await ThreadSwitcher.ResumeBackgroundAsync();

            var db = await Db.Context();

            try
            {
                // Get the content JSON from the source site's API
                var contentJsonUrl = $"{loopRef.SiteLocalApiUrl}/contentjson/{loopRef.ContentId}";
                var contentJsonResponse = await httpClient.GetStringAsync(contentJsonUrl);

                if (string.IsNullOrEmpty(contentJsonResponse))
                {
                    var err = $"Failed to retrieve content data for {loopRef.ContentId}";
                    await statusContext.ToastError(err);
                    errors.Add(err);
                    continue;
                }

                // Deserialize content data
                var noteContent = JsonSerializer.Deserialize<NoteContent>(contentJsonResponse,
                    JsonTools.WriteIndentedOptions);

                if (noteContent == null)
                {
                    var err = $"Failed to parse content data for {loopRef.ContentId}";
                    await statusContext.ToastError(err);
                    errors.Add(err);
                    continue;
                }

                noteContent.Id = 0;

                var (saveGenerationReturn, _) = await NoteGenerator.SaveAndGenerateHtml(noteContent,
                    null, statusContext.ProgressTracker());

                var bracketCodeCheck = await CommonContentValidation.CheckStringForBadContentReferences(
                    $"{noteContent.BodyContent ?? string.Empty} {noteContent.Summary ?? string.Empty} {FakeMainPhotoBracketCode(noteContent.MainPicture)}",
                    db,
                    statusContext.ProgressTracker());

                if (saveGenerationReturn.HasError || bracketCodeCheck.HasError)
                {
                    var editor = await NoteContentEditorWindow.CreateInstance(noteContent);
                    await editor.PositionWindowAndShowOnUiThread();

                    //Allow execution to continue so Automation can continue
                    _ = editor.StatusContext.ShowMessageWithOkButton("Problem Saving",
                        saveGenerationReturn.GenerationNote);

                    errors.Add(
                        $"Error saving or validating note content {loopRef.ContentId}: {saveGenerationReturn.GenerationNote} {bracketCodeCheck.GenerationNote}");
                    continue;
                }

                statusContext.Progress($"New Note Editor - based on {loopRef.ContentId} ");

                await ThreadSwitcher.ResumeBackgroundAsync();
            }
            catch (Exception ex)
            {
                var err = $"Error processing content {loopRef.ContentId}: {ex.Message}";
                await statusContext.ToastError(err);
                Log.Error(ex, "Error processing content from other site: {ContentId}", loopRef.ContentId);
                errors.Add(err);
            }
        }

        return errors;
    }

    public static async Task<List<string>> HandlePhotoContentReferences(
        List<ContentClipboardRepresentation> contentRefs,
        StatusControlContext statusContext)
    {
        var errors = new List<string>();
        await ThreadSwitcher.ResumeBackgroundAsync();

        statusContext.Progress("Starting photo load.");

        var loopCount = 0;

        using var httpClient = new HttpClient();

        foreach (var loopRef in contentRefs)
        {
            loopCount++;

            statusContext.Progress($"Processing {loopCount} of {contentRefs.Count} Photos from Clipboard");

            await ThreadSwitcher.ResumeBackgroundAsync();

            var db = await Db.Context();

            try
            {
                // Get the content JSON from the source site's API
                var contentJsonUrl = $"{loopRef.SiteLocalApiUrl}/contentjson/{loopRef.ContentId}";
                var contentJsonResponse = await httpClient.GetStringAsync(contentJsonUrl);

                if (string.IsNullOrEmpty(contentJsonResponse))
                {
                    var err = $"Failed to retrieve content data for {loopRef.ContentId}";
                    await statusContext.ToastError(err);
                    errors.Add(err);
                    continue;
                }

                // Deserialize content data
                var photoContent = JsonSerializer.Deserialize<PhotoContent>(contentJsonResponse,
                    JsonTools.WriteIndentedOptions);

                if (photoContent == null)
                {
                    var err = $"Failed to parse content data for {loopRef.ContentId}";
                    await statusContext.ToastError(err);
                    errors.Add(err);
                    continue;
                }

                photoContent.Id = 0;

                // Get the media file information for the content
                var mediaFileUrl = $"{loopRef.SiteLocalApiUrl}/mediafile/{loopRef.ContentId}";
                var mediaFileResponse = await httpClient.GetStringAsync(mediaFileUrl);

                if (string.IsNullOrEmpty(mediaFileResponse))
                {
                    var err = $"Failed to retrieve media file data for {loopRef.ContentId}";
                    await statusContext.ToastError(err);
                    errors.Add(err);
                    continue;
                }

                // Parse the media file information
                var mediaFileInfo = JsonSerializer.Deserialize<ContentMediaFileResponse>(
                    mediaFileResponse,
                    JsonTools.WriteIndentedOptions);

                if (mediaFileInfo is not { Exists: true })
                {
                    var err = $"Media file not found for {loopRef.ContentId}";
                    await statusContext.ToastError(err);
                    errors.Add(err);
                    continue;
                }

                var photoFile = new FileInfo(mediaFileInfo.FullPath);

                var (saveGenerationReturn, _) = await PhotoGenerator.SaveAndGenerateHtml(photoContent, photoFile, true,
                    null, statusContext.ProgressTracker());

                var bracketCodeCheck = await CommonContentValidation.CheckStringForBadContentReferences(
                    $"{photoContent.BodyContent ?? string.Empty} {photoContent.UpdateNotes ?? string.Empty} {FakeMainPhotoBracketCode(photoContent.MainPicture)}",
                    db,
                    statusContext.ProgressTracker());

                if (saveGenerationReturn.HasError || bracketCodeCheck.HasError)
                {
                    var editor = await PhotoContentEditorWindow.CreateInstance(photoContent, false, photoFile, true);
                    await editor.PositionWindowAndShowOnUiThread();

                    //Allow execution to continue so Automation can continue
                    _ = editor.StatusContext.ShowMessageWithOkButton("Problem Saving",
                        saveGenerationReturn.GenerationNote);

                    errors.Add(
                        $"Error saving or validating photo content {loopRef.ContentId}: {saveGenerationReturn.GenerationNote} {bracketCodeCheck.GenerationNote}");
                    continue;
                }

                statusContext.Progress($"New Photo Editor - based on {loopRef.ContentId} ");

                await ThreadSwitcher.ResumeBackgroundAsync();
            }
            catch (Exception ex)
            {
                var err = $"Error processing content {loopRef.ContentId}: {ex.Message}";
                await statusContext.ToastError(err);
                Log.Error(ex, "Error processing content from other site: {ContentId}", loopRef.ContentId);
                errors.Add(err);
            }
        }

        return errors;
    }

    public static async Task<List<string>> HandlePointContentReferences(
        List<ContentClipboardRepresentation> contentRefs,
        StatusControlContext statusContext)
    {
        var errors = new List<string>();
        await ThreadSwitcher.ResumeBackgroundAsync();

        statusContext.Progress("Starting point content load.");

        var loopCount = 0;

        using var httpClient = new HttpClient();

        foreach (var loopRef in contentRefs)
        {
            loopCount++;

            statusContext.Progress($"Processing {loopCount} of {contentRefs.Count} Points from Clipboard");

            await ThreadSwitcher.ResumeBackgroundAsync();

            var db = await Db.Context();

            try
            {
                // Get the PointContentDto from the source site's API
                var pointJsonUrl = $"{loopRef.SiteLocalApiUrl}/pointdto/{loopRef.ContentId}";
                var pointJsonResponse = await httpClient.GetStringAsync(pointJsonUrl);

                if (string.IsNullOrEmpty(pointJsonResponse))
                {
                    var err = $"Failed to retrieve point data for {loopRef.ContentId}";
                    await statusContext.ToastError(err);
                    errors.Add(err);
                    continue;
                }

                // Deserialize PointContentDto
                var pointDto =
                    JsonSerializer.Deserialize<PointContentDto>(pointJsonResponse, JsonTools.WriteIndentedOptions);

                if (pointDto == null)
                {
                    var err = $"Failed to parse point data for {loopRef.ContentId}";
                    await statusContext.ToastError(err);
                    errors.Add(err);
                    continue;
                }

                pointDto.Id = 0;
                pointDto.PointDetails.ForEach(x => x.Id = 0);

                // Save and generate HTML/data for the point
                var (saveGenerationReturn, _) =
                    await PointGenerator.SaveAndGenerateHtml(pointDto, null, statusContext.ProgressTracker());

                var bracketCodeCheck = await CommonContentValidation.CheckStringForBadContentReferences(
                    $"{pointDto.BodyContent ?? string.Empty} {pointDto.UpdateNotes ?? string.Empty}", db,
                    statusContext.ProgressTracker());

                if (saveGenerationReturn.HasError || bracketCodeCheck.HasError)
                {
                    errors.Add(
                        $"Error saving or validating point content {loopRef.ContentId}: {saveGenerationReturn.GenerationNote} {bracketCodeCheck.GenerationNote}");
                    continue;
                }

                statusContext.Progress($"Imported Point - based on {loopRef.ContentId}");

                await ThreadSwitcher.ResumeBackgroundAsync();
            }
            catch (Exception ex)
            {
                var err = $"Error processing point content {loopRef.ContentId}: {ex.Message}";
                await statusContext.ToastError(err);
                Log.Error(ex, "Error processing point content from other site: {ContentId}", loopRef.ContentId);
                errors.Add(err);
            }
        }

        return errors;
    }

    public static async Task<List<string>> HandlePostContentReferences(List<ContentClipboardRepresentation> contentRefs,
        StatusControlContext statusContext)
    {
        var errors = new List<string>();
        await ThreadSwitcher.ResumeBackgroundAsync();

        statusContext.Progress("Starting post content load.");

        var loopCount = 0;

        using var httpClient = new HttpClient();

        foreach (var loopRef in contentRefs)
        {
            loopCount++;

            statusContext.Progress($"Processing {loopCount} of {contentRefs.Count} Posts from Clipboard");

            await ThreadSwitcher.ResumeBackgroundAsync();

            var db = await Db.Context();

            try
            {
                // Get the content JSON from the source site's API
                var contentJsonUrl = $"{loopRef.SiteLocalApiUrl}/contentjson/{loopRef.ContentId}";
                var contentJsonResponse = await httpClient.GetStringAsync(contentJsonUrl);

                if (string.IsNullOrEmpty(contentJsonResponse))
                {
                    var err = $"Failed to retrieve content data for {loopRef.ContentId}";
                    await statusContext.ToastError(err);
                    errors.Add(err);
                    continue;
                }

                // Deserialize content data
                var postContent = JsonSerializer.Deserialize<PostContent>(contentJsonResponse,
                    JsonTools.WriteIndentedOptions);

                if (postContent == null)
                {
                    var err = $"Failed to parse content data for {loopRef.ContentId}";
                    await statusContext.ToastError(err);
                    errors.Add(err);
                    continue;
                }

                postContent.Id = 0;

                var (saveGenerationReturn, _) = await PostGenerator.SaveAndGenerateHtml(postContent,
                    null, statusContext.ProgressTracker());

                var bracketCodeCheck = await CommonContentValidation.CheckStringForBadContentReferences(
                    $"{postContent.BodyContent ?? string.Empty} {postContent.UpdateNotes ?? string.Empty} {FakeMainPhotoBracketCode(postContent.MainPicture)}",
                    db,
                    statusContext.ProgressTracker());

                if (saveGenerationReturn.HasError || bracketCodeCheck.HasError)
                {
                    var editor = await PostContentEditorWindow.CreateInstance(postContent);
                    await editor.PositionWindowAndShowOnUiThread();

                    //Allow execution to continue so Automation can continue
                    _ = editor.StatusContext.ShowMessageWithOkButton("Problem Saving",
                        saveGenerationReturn.GenerationNote);

                    errors.Add(
                        $"Error saving or validating post content {loopRef.ContentId}: {saveGenerationReturn.GenerationNote} {bracketCodeCheck.GenerationNote}");
                    continue;
                }

                statusContext.Progress($"New Post Editor - based on {loopRef.ContentId} ");

                await ThreadSwitcher.ResumeBackgroundAsync();
            }
            catch (Exception ex)
            {
                var err = $"Error processing content {loopRef.ContentId}: {ex.Message}";
                await statusContext.ToastError(err);
                Log.Error(ex, "Error processing content from other site: {ContentId}", loopRef.ContentId);
                errors.Add(err);
            }
        }

        return errors;
    }

    public static async Task HandleReferencesFromOtherSites(List<ContentClipboardRepresentation> contentRefs,
        StatusControlContext statusContext)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        statusContext.Progress($"Processing {contentRefs.Count} content items from other sites...");

        if (contentRefs.Count > 10)
            if (await statusContext.ShowMessageWithYesNoButton(
                    "Confirm > 10 Items to Import...",
                    $"You are about to import {contentRefs.Count} items. Do you want to do this?") == "No")
                return;

        var contentDescriptions =
            contentRefs.Select(c => $"Content Type: {c.ContentType}, ID: {c.ContentId}, From Site: {c.SiteId}");
        var message = string.Join(Environment.NewLine, contentDescriptions);

        await statusContext.ToastSuccess($"Received content references:{Environment.NewLine}{message}");

        var allErrors = new List<string>();

        var fileContentRefs = contentRefs.Where(c =>
            c.ContentType.Equals(Db.ContentTypeDisplayStringForFile, StringComparison.OrdinalIgnoreCase)).ToList();
        if (fileContentRefs.Any())
        {
            statusContext.Progress($"Found {fileContentRefs.Count} file content items to import");
            var fileErrors = await HandleFileContentReferences(fileContentRefs, statusContext);
            allErrors.AddRange(fileErrors);
        }

        var geoJsonContentRefs = contentRefs.Where(c =>
            c.ContentType.Equals(Db.ContentTypeDisplayStringForGeoJson, StringComparison.OrdinalIgnoreCase)).ToList();
        if (geoJsonContentRefs.Any())
        {
            statusContext.Progress($"Found {geoJsonContentRefs.Count} GeoJson content items to import");
            var geoJsonErrors = await HandleGeoJsonContentReferences(geoJsonContentRefs, statusContext);
            allErrors.AddRange(geoJsonErrors);
        }

        var imageContentRefs = contentRefs.Where(c =>
            c.ContentType.Equals(Db.ContentTypeDisplayStringForImage, StringComparison.OrdinalIgnoreCase)).ToList();
        if (imageContentRefs.Any())
        {
            statusContext.Progress($"Found {imageContentRefs.Count} image content items to import");
            var imageErrors = await HandleImageContentReferences(imageContentRefs, statusContext);
            allErrors.AddRange(imageErrors);
        }

        var lineContentRefs = contentRefs.Where(c =>
            c.ContentType.Equals(Db.ContentTypeDisplayStringForLine, StringComparison.OrdinalIgnoreCase)).ToList();
        if (lineContentRefs.Any())
        {
            statusContext.Progress($"Found {lineContentRefs.Count} line content items to import");
            var lineErrors = await HandleLineContentReferences(lineContentRefs, statusContext);
            allErrors.AddRange(lineErrors);
        }

        var linkContentRefs = contentRefs.Where(c =>
            c.ContentType.Equals(Db.ContentTypeDisplayStringForLink, StringComparison.OrdinalIgnoreCase)).ToList();
        if (linkContentRefs.Any())
        {
            statusContext.Progress($"Found {linkContentRefs.Count} link content items to import");
            var linkErrors = await HandleLinkContentReferences(linkContentRefs, statusContext);
            allErrors.AddRange(linkErrors);
        }

        var mapContentRefs = contentRefs.Where(c =>
            c.ContentType.Equals(Db.ContentTypeDisplayStringForMap, StringComparison.OrdinalIgnoreCase)).ToList();
        if (mapContentRefs.Any())
        {
            statusContext.Progress($"Found {mapContentRefs.Count} map content items to import");
            var mapErrors = await HandleMapContentReferences(mapContentRefs, statusContext);
            allErrors.AddRange(mapErrors);
        }

        var noteContentRefs = contentRefs.Where(c =>
            c.ContentType.Equals(Db.ContentTypeDisplayStringForNote, StringComparison.OrdinalIgnoreCase)).ToList();
        if (noteContentRefs.Any())
        {
            statusContext.Progress($"Found {noteContentRefs.Count} note content items to import");
            var noteErrors = await HandleNoteContentReferences(noteContentRefs, statusContext);
            allErrors.AddRange(noteErrors);
        }

        var photoContentRefs = contentRefs.Where(c =>
            c.ContentType.Equals(Db.ContentTypeDisplayStringForPhoto, StringComparison.OrdinalIgnoreCase)).ToList();
        if (photoContentRefs.Any())
        {
            statusContext.Progress($"Found {photoContentRefs.Count} photo content items to import");
            var photoErrors = await HandlePhotoContentReferences(photoContentRefs, statusContext);
            allErrors.AddRange(photoErrors);
        }

        var pointContentRefs = contentRefs.Where(c =>
            c.ContentType.Equals(Db.ContentTypeDisplayStringForPoint, StringComparison.OrdinalIgnoreCase)).ToList();
        if (pointContentRefs.Any())
        {
            statusContext.Progress($"Found {pointContentRefs.Count} point content items to import");
            var pointErrors = await HandlePointContentReferences(pointContentRefs, statusContext);
            allErrors.AddRange(pointErrors);
        }

        var postContentRefs = contentRefs.Where(c =>
            c.ContentType.Equals(Db.ContentTypeDisplayStringForPost, StringComparison.OrdinalIgnoreCase)).ToList();
        if (postContentRefs.Any())
        {
            statusContext.Progress($"Found {postContentRefs.Count} post content items to import");
            var postErrors = await HandlePostContentReferences(postContentRefs, statusContext);
            allErrors.AddRange(postErrors);
        }

        var snippetContentRefs = contentRefs.Where(c =>
            c.ContentType.Equals(Db.ContentTypeDisplayStringForSnippet, StringComparison.OrdinalIgnoreCase)).ToList();
        if (snippetContentRefs.Any())
        {
            statusContext.Progress($"Found {snippetContentRefs.Count} snippet content items to import");
            var snippetErrors = await HandleSnippetContentReferences(snippetContentRefs, statusContext);
            allErrors.AddRange(snippetErrors);
        }

        var trailContentRefs = contentRefs.Where(c =>
            c.ContentType.Equals(Db.ContentTypeDisplayStringForTrail, StringComparison.OrdinalIgnoreCase)).ToList();
        if (trailContentRefs.Any())
        {
            statusContext.Progress($"Found {trailContentRefs.Count} trail content items to import");
            var trailErrors = await HandleTrailContentReferences(trailContentRefs, statusContext);
            allErrors.AddRange(trailErrors);
        }

        var videoContentRefs = contentRefs.Where(c =>
            c.ContentType.Equals(Db.ContentTypeDisplayStringForVideo, StringComparison.OrdinalIgnoreCase)).ToList();
        if (videoContentRefs.Any())
        {
            statusContext.Progress($"Found {videoContentRefs.Count} video content items to import");
            var videoErrors = await HandleVideoContentReferences(videoContentRefs, statusContext);
            allErrors.AddRange(videoErrors);
        }

        // Show all errors at the end
        if (allErrors.Any())
        {
            var errorMessage = "Import completed with errors:\n" + string.Join("\n", allErrors);
            await statusContext.ShowMessageWithOkButton("Import Errors", errorMessage);
        }
    }

    public static async Task<List<string>> HandleSnippetContentReferences(
        List<ContentClipboardRepresentation> contentRefs,
        StatusControlContext statusContext)
    {
        var errors = new List<string>();
        await ThreadSwitcher.ResumeBackgroundAsync();

        statusContext.Progress("Starting snippet content load.");

        var loopCount = 0;

        using var httpClient = new HttpClient();

        foreach (var loopRef in contentRefs)
        {
            loopCount++;

            statusContext.Progress($"Processing {loopCount} of {contentRefs.Count} Snippets from Clipboard");

            await ThreadSwitcher.ResumeBackgroundAsync();

            var db = await Db.Context();

            try
            {
                // Get the content JSON from the source site's API
                var contentJsonUrl = $"{loopRef.SiteLocalApiUrl}/contentjson/{loopRef.ContentId}";
                var contentJsonResponse = await httpClient.GetStringAsync(contentJsonUrl);

                if (string.IsNullOrEmpty(contentJsonResponse))
                {
                    var err = $"Failed to retrieve content data for {loopRef.ContentId}";
                    await statusContext.ToastError(err);
                    errors.Add(err);
                    continue;
                }

                // Deserialize content data
                var snippetContent = JsonSerializer.Deserialize<Snippet>(contentJsonResponse,
                    JsonTools.WriteIndentedOptions);

                if (snippetContent == null)
                {
                    var err = $"Failed to parse content data for {loopRef.ContentId}";
                    await statusContext.ToastError(err);
                    errors.Add(err);
                    continue;
                }

                snippetContent.Id = 0;

                var (saveGenerationReturn, _) = await SnippetGenerator.SaveAndGenerateHtml(snippetContent,
                    statusContext.ProgressTracker());

                var bracketCodeCheck = await CommonContentValidation.CheckStringForBadContentReferences(
                    $"{snippetContent.BodyContent ?? string.Empty} {snippetContent.Summary ?? string.Empty}", db,
                    statusContext.ProgressTracker());

                if (saveGenerationReturn.HasError || bracketCodeCheck.HasError)
                {
                    var editor = await SnippetEditorWindow.CreateInstance(snippetContent);
                    await editor.PositionWindowAndShowOnUiThread();

                    //Allow execution to continue so Automation can continue
                    _ = editor.StatusContext.ShowMessageWithOkButton("Problem Saving",
                        saveGenerationReturn.GenerationNote);

                    errors.Add(
                        $"Error saving or validating snippet content {loopRef.ContentId}: {saveGenerationReturn.GenerationNote} {bracketCodeCheck.GenerationNote}");
                    continue;
                }

                statusContext.Progress($"New Snippet Editor - based on {loopRef.ContentId} ");

                await ThreadSwitcher.ResumeBackgroundAsync();
            }
            catch (Exception ex)
            {
                var err = $"Error processing content {loopRef.ContentId}: {ex.Message}";
                await statusContext.ToastError(err);
                Log.Error(ex, "Error processing content from other site: {ContentId}", loopRef.ContentId);
                errors.Add(err);
            }
        }

        return errors;
    }

    public static async Task<List<string>> HandleTrailContentReferences(
        List<ContentClipboardRepresentation> contentRefs,
        StatusControlContext statusContext)
    {
        var errors = new List<string>();
        await ThreadSwitcher.ResumeBackgroundAsync();

        statusContext.Progress("Starting trail content load.");

        var loopCount = 0;

        using var httpClient = new HttpClient();

        foreach (var loopRef in contentRefs)
        {
            loopCount++;

            statusContext.Progress($"Processing {loopCount} of {contentRefs.Count} Trails from Clipboard");

            await ThreadSwitcher.ResumeBackgroundAsync();

            var db = await Db.Context();

            try
            {
                // Get the content JSON from the source site's API
                var contentJsonUrl = $"{loopRef.SiteLocalApiUrl}/contentjson/{loopRef.ContentId}";
                var contentJsonResponse = await httpClient.GetStringAsync(contentJsonUrl);

                if (string.IsNullOrEmpty(contentJsonResponse))
                {
                    var err = $"Failed to retrieve content data for {loopRef.ContentId}";
                    await statusContext.ToastError(err);
                    errors.Add(err);
                    continue;
                }

                // Deserialize content data
                var trailContent = JsonSerializer.Deserialize<TrailContent>(contentJsonResponse,
                    JsonTools.WriteIndentedOptions);

                if (trailContent == null)
                {
                    var err = $"Failed to parse content data for {loopRef.ContentId}";
                    await statusContext.ToastError(err);
                    errors.Add(err);
                    continue;
                }

                trailContent.Id = 0;

                var (saveGenerationReturn, _) = await TrailGenerator.SaveAndGenerateHtml(trailContent,
                    null, statusContext.ProgressTracker());

                var bracketCodeCheck = await CommonContentValidation.CheckStringForBadContentReferences(
                    $"{trailContent.BodyContent ?? string.Empty} {trailContent.UpdateNotes ?? string.Empty} " +
                    $"{trailContent.BikesNote ?? string.Empty} {trailContent.DogsNote ?? string.Empty} " +
                    $"{trailContent.FeesNote ?? string.Empty} {trailContent.OtherDetails ?? string.Empty} {FakeMainPhotoBracketCode(trailContent.MainPicture)}",
                    db,
                    statusContext.ProgressTracker());

                if (saveGenerationReturn.HasError || bracketCodeCheck.HasError)
                {
                    var editor = await TrailContentEditorWindow.CreateInstance(trailContent);
                    await editor.PositionWindowAndShowOnUiThread();

                    //Allow execution to continue so Automation can continue
                    _ = editor.StatusContext.ShowMessageWithOkButton("Problem Saving",
                        saveGenerationReturn.GenerationNote);

                    errors.Add(
                        $"Error saving or validating trail content {loopRef.ContentId}: {saveGenerationReturn.GenerationNote} {bracketCodeCheck.GenerationNote}");
                    continue;
                }

                statusContext.Progress($"New Trail Editor - based on {loopRef.ContentId} ");

                await ThreadSwitcher.ResumeBackgroundAsync();
            }
            catch (Exception ex)
            {
                var err = $"Error processing content {loopRef.ContentId}: {ex.Message}";
                await statusContext.ToastError(err);
                Log.Error(ex, "Error processing content from other site: {ContentId}", loopRef.ContentId);
                errors.Add(err);
            }
        }

        return errors;
    }

    public static async Task<List<string>> HandleVideoContentReferences(
        List<ContentClipboardRepresentation> contentRefs,
        StatusControlContext statusContext)
    {
        var errors = new List<string>();
        await ThreadSwitcher.ResumeBackgroundAsync();

        statusContext.Progress("Starting video content load.");

        var loopCount = 0;

        using var httpClient = new HttpClient();

        foreach (var loopRef in contentRefs)
        {
            loopCount++;

            statusContext.Progress($"Processing {loopCount} of {contentRefs.Count} Videos from Clipboard");

            await ThreadSwitcher.ResumeBackgroundAsync();

            var db = await Db.Context();

            try
            {
                // Get the content JSON from the source site's API
                var contentJsonUrl = $"{loopRef.SiteLocalApiUrl}/contentjson/{loopRef.ContentId}";
                var contentJsonResponse = await httpClient.GetStringAsync(contentJsonUrl);

                if (string.IsNullOrEmpty(contentJsonResponse))
                {
                    var err = $"Failed to retrieve content data for {loopRef.ContentId}";
                    await statusContext.ToastError(err);
                    errors.Add(err);
                    continue;
                }

                // Deserialize content data
                var videoContent = JsonSerializer.Deserialize<VideoContent>(contentJsonResponse,
                    JsonTools.WriteIndentedOptions);

                if (videoContent == null)
                {
                    var err = $"Failed to parse content data for {loopRef.ContentId}";
                    await statusContext.ToastError(err);
                    errors.Add(err);
                    continue;
                }

                videoContent.Id = 0;

                // Get the media file information for the content
                var mediaFileUrl = $"{loopRef.SiteLocalApiUrl}/mediafile/{loopRef.ContentId}";
                var mediaFileResponse = await httpClient.GetStringAsync(mediaFileUrl);

                if (string.IsNullOrEmpty(mediaFileResponse))
                {
                    var err = $"Failed to retrieve media file data for {loopRef.ContentId}";
                    await statusContext.ToastError(err);
                    errors.Add(err);
                    continue;
                }

                // Parse the media file information
                var mediaFileInfo = JsonSerializer.Deserialize<ContentMediaFileResponse>(
                    mediaFileResponse,
                    JsonTools.WriteIndentedOptions);

                if (mediaFileInfo is not { Exists: true })
                {
                    var err = $"Media file not found for {loopRef.ContentId}";
                    await statusContext.ToastError(err);
                    errors.Add(err);
                    continue;
                }

                var videoFile = new FileInfo(mediaFileInfo.FullPath);

                var (saveGenerationReturn, _) = await VideoGenerator.SaveAndGenerateHtml(videoContent, videoFile,
                    null, statusContext.ProgressTracker());

                var bracketCodeCheck = await CommonContentValidation.CheckStringForBadContentReferences(
                    $"{videoContent.BodyContent ?? string.Empty} {videoContent.UpdateNotes ?? string.Empty} {FakeMainPhotoBracketCode(videoContent.MainPicture)}",
                    db,
                    statusContext.ProgressTracker());

                if (saveGenerationReturn.HasError || bracketCodeCheck.HasError)
                {
                    var editor =
                        await VideoContentEditorWindow.CreateInstance(videoContent, skipMetadataLoadFromVideo: true);
                    await editor.PositionWindowAndShowOnUiThread();

                    //Allow execution to continue so Automation can continue
                    _ = editor.StatusContext.ShowMessageWithOkButton("Problem Saving",
                        saveGenerationReturn.GenerationNote);

                    errors.Add(
                        $"Error saving or validating video content {loopRef.ContentId}: {saveGenerationReturn.GenerationNote} {bracketCodeCheck.GenerationNote}");
                    continue;
                }

                statusContext.Progress($"New Video Editor - based on {loopRef.ContentId} ");

                await ThreadSwitcher.ResumeBackgroundAsync();
            }
            catch (Exception ex)
            {
                var err = $"Error processing content {loopRef.ContentId}: {ex.Message}";
                await statusContext.ToastError(err);
                Log.Error(ex, "Error processing content from other site: {ContentId}", loopRef.ContentId);
                errors.Add(err);
            }
        }

        return errors;
    }
}