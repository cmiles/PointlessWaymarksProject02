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
using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace PointlessWaymarks.CmsWpfControls.ContentList;

public static class ContentClipboardRepresentationHandlers
{
    private static string FakeMainPhotoBracketCode(Guid? mainPhoto)
    {
        if (mainPhoto == null) return string.Empty;
        return
            $"{{{{{BracketCodePhotos.BracketCodeToken} {mainPhoto.ToString()}; Fake Bracket Code for Content Ref Import}}}}";
    }
    
    //TODO: Handle Point Types
    //TODO: Trails and Points and ?Others better Handle missing Content References
    //TODO: Check Main Image Values
    public static async Task HandleFileContentReferences(List<ContentClipboardRepresentation> contentRefs,
        StatusControlContext statusContext)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        statusContext.Progress("Starting file content load.");

        if (contentRefs.Count > 10)
        {
            await statusContext.ToastError(
                "Dragging in new content is limited to 10 files at a time...");
            return;
        }

        await ThreadSwitcher.ResumeBackgroundAsync();

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
                    await statusContext.ToastError($"Failed to retrieve content data for {loopRef.ContentId}");
                    continue;
                }

                // Deserialize content data
                var fileContent = JsonSerializer.Deserialize<FileContent>(contentJsonResponse,
                    JsonTools.WriteIndentedOptions);

                if (fileContent == null)
                {
                    await statusContext.ToastError($"Failed to parse content data for {loopRef.ContentId}");
                    continue;
                }

                fileContent.Id = 0;

                // Get the media file information for the content
                var mediaFileUrl = $"{loopRef.SiteLocalApiUrl}/mediafile/{loopRef.ContentId}";
                var mediaFileResponse = await httpClient.GetStringAsync(mediaFileUrl);

                if (string.IsNullOrEmpty(mediaFileResponse))
                {
                    await statusContext.ToastError($"Failed to retrieve media file data for {loopRef.ContentId}");
                    continue;
                }

                // Parse the media file information
                var mediaFileInfo = JsonSerializer.Deserialize<ContentMediaFileResponse>(
                    mediaFileResponse,
                    JsonTools.WriteIndentedOptions);

                if (mediaFileInfo is not { Exists: true })
                {
                    await statusContext.ToastError($"Media file not found for {loopRef.ContentId}");
                    continue;
                }

                var fileFile = new FileInfo(mediaFileInfo.FullPath);

                var (saveGenerationReturn, _) = await FileGenerator.SaveAndGenerateHtml(fileContent, fileFile,
                    null, statusContext.ProgressTracker());
                
                var bracketCodeCheck = await CommonContentValidation.CheckStringForBadContentReferences(
                    $"{fileContent.BodyContent ?? string.Empty} {fileContent.UpdateNotes ?? string.Empty} {FakeMainPhotoBracketCode(fileContent.MainPicture)}", db,
                    statusContext.ProgressTracker());
                
                if (saveGenerationReturn.HasError || bracketCodeCheck.HasError)
                {
                    var editor = await FileContentEditorWindow.CreateInstance(fileContent);
                    await editor.PositionWindowAndShowOnUiThread();

                    //Allow execution to continue so Automation can continue
                    _ = editor.StatusContext.ShowMessageWithOkButton("Problem Saving",
                        saveGenerationReturn.GenerationNote);

                    continue;
                }

                statusContext.Progress($"New File Editor - based on {loopRef.ContentId} ");

                await ThreadSwitcher.ResumeBackgroundAsync();
            }
            catch (Exception ex)
            {
                await statusContext.ToastError($"Error processing content {loopRef.ContentId}: {ex.Message}");
                Log.Error(ex, "Error processing content from other site: {ContentId}", loopRef.ContentId);
            }
        }
    }

    public static async Task HandleGeoJsonContentReferences(List<ContentClipboardRepresentation> contentRefs,
        StatusControlContext statusContext)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        statusContext.Progress("Starting GeoJson content load.");

        if (contentRefs.Count > 10)
        {
            await statusContext.ToastError(
                "Dragging in new content is limited to 10 GeoJson items at a time...");
            return;
        }

        await ThreadSwitcher.ResumeBackgroundAsync();

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
                    await statusContext.ToastError($"Failed to retrieve content data for {loopRef.ContentId}");
                    continue;
                }

                // Deserialize content data
                var geoJsonContent = JsonSerializer.Deserialize<GeoJsonContent>(contentJsonResponse,
                    JsonTools.WriteIndentedOptions);

                if (geoJsonContent == null)
                {
                    await statusContext.ToastError($"Failed to parse content data for {loopRef.ContentId}");
                    continue;
                }

                geoJsonContent.Id = 0;

                var (saveGenerationReturn, _) = await GeoJsonGenerator.SaveAndGenerateHtml(geoJsonContent,
                    null, statusContext.ProgressTracker());

                var bracketCodeCheck = await CommonContentValidation.CheckStringForBadContentReferences(
                    $"{geoJsonContent.BodyContent ?? string.Empty} {geoJsonContent.UpdateNotes ?? string.Empty} {FakeMainPhotoBracketCode(geoJsonContent.MainPicture)}", db,
                    statusContext.ProgressTracker());

                if (saveGenerationReturn.HasError || bracketCodeCheck.HasError)
                {
                    var editor = await GeoJsonContentEditorWindow.CreateInstance(geoJsonContent);
                    await editor.PositionWindowAndShowOnUiThread();

                    //Allow execution to continue so Automation can continue
                    _ = editor.StatusContext.ShowMessageWithOkButton("Problem Saving",
                        saveGenerationReturn.GenerationNote);

                    continue;
                }

                statusContext.Progress($"New GeoJson Editor - based on {loopRef.ContentId} ");

                await ThreadSwitcher.ResumeBackgroundAsync();
            }
            catch (Exception ex)
            {
                await statusContext.ToastError($"Error processing content {loopRef.ContentId}: {ex.Message}");
                Log.Error(ex, "Error processing content from other site: {ContentId}", loopRef.ContentId);
            }
        }
    }

    public static async Task HandleImageContentReferences(List<ContentClipboardRepresentation> contentRefs,
        StatusControlContext statusContext)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        statusContext.Progress("Starting image content load.");

        if (contentRefs.Count > 10)
        {
            await statusContext.ToastError(
                "Dragging in new content is limited to 10 images at a time...");
            return;
        }

        await ThreadSwitcher.ResumeBackgroundAsync();

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
                    await statusContext.ToastError($"Failed to retrieve content data for {loopRef.ContentId}");
                    continue;
                }

                // Deserialize content data
                var imageContent = JsonSerializer.Deserialize<ImageContent>(contentJsonResponse,
                    JsonTools.WriteIndentedOptions);

                if (imageContent == null)
                {
                    await statusContext.ToastError($"Failed to parse content data for {loopRef.ContentId}");
                    continue;
                }

                imageContent.Id = 0;

                // Get the media file information for the content
                var mediaFileUrl = $"{loopRef.SiteLocalApiUrl}/mediafile/{loopRef.ContentId}";
                var mediaFileResponse = await httpClient.GetStringAsync(mediaFileUrl);

                if (string.IsNullOrEmpty(mediaFileResponse))
                {
                    await statusContext.ToastError($"Failed to retrieve media file data for {loopRef.ContentId}");
                    continue;
                }

                // Parse the media file information
                var mediaFileInfo = JsonSerializer.Deserialize<ContentMediaFileResponse>(
                    mediaFileResponse,
                    JsonTools.WriteIndentedOptions);

                if (mediaFileInfo is not { Exists: true })
                {
                    await statusContext.ToastError($"Media file not found for {loopRef.ContentId}");
                    continue;
                }

                var imageFile = new FileInfo(mediaFileInfo.FullPath);

                var (saveGenerationReturn, _) = await ImageGenerator.SaveAndGenerateHtml(imageContent, imageFile, true,
                    null, statusContext.ProgressTracker());

                var bracketCodeCheck = await CommonContentValidation.CheckStringForBadContentReferences(
                    $"{imageContent.BodyContent ?? string.Empty} {imageContent.UpdateNotes ?? string.Empty} {FakeMainPhotoBracketCode(imageContent.MainPicture)}", db,
                    statusContext.ProgressTracker());

                if (saveGenerationReturn.HasError || bracketCodeCheck.HasError)
                {
                    var editor = await ImageContentEditorWindow.CreateInstance(imageContent, imageFile);
                    await editor.PositionWindowAndShowOnUiThread();

                    //Allow execution to continue so Automation can continue
                    _ = editor.StatusContext.ShowMessageWithOkButton("Problem Saving",
                        saveGenerationReturn.GenerationNote);

                    continue;
                }

                statusContext.Progress($"New Image Editor - based on {loopRef.ContentId} ");

                await ThreadSwitcher.ResumeBackgroundAsync();
            }
            catch (Exception ex)
            {
                await statusContext.ToastError($"Error processing content {loopRef.ContentId}: {ex.Message}");
                Log.Error(ex, "Error processing content from other site: {ContentId}", loopRef.ContentId);
            }
        }
    }

    public static async Task HandleLineContentReferences(List<ContentClipboardRepresentation> contentRefs,
        StatusControlContext statusContext)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        statusContext.Progress("Starting line content load.");

        if (contentRefs.Count > 10)
        {
            await statusContext.ToastError(
                "Dragging in new content is limited to 10 lines at a time...");
            return;
        }

        await ThreadSwitcher.ResumeBackgroundAsync();

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
                    await statusContext.ToastError($"Failed to retrieve content data for {loopRef.ContentId}");
                    continue;
                }

                // Deserialize content data
                var lineContent = JsonSerializer.Deserialize<LineContent>(contentJsonResponse,
                    JsonTools.WriteIndentedOptions);

                if (lineContent == null)
                {
                    await statusContext.ToastError($"Failed to parse content data for {loopRef.ContentId}");
                    continue;
                }

                lineContent.Id = 0;

                var (saveGenerationReturn, _) = await LineGenerator.SaveAndGenerateHtml(lineContent,
                    null, statusContext.ProgressTracker());

                var bracketCodeCheck = await CommonContentValidation.CheckStringForBadContentReferences(
                    $"{lineContent.BodyContent ?? string.Empty} {lineContent.UpdateNotes ?? string.Empty} {FakeMainPhotoBracketCode(lineContent.MainPicture)}", db,
                    statusContext.ProgressTracker());

                if (saveGenerationReturn.HasError || bracketCodeCheck.HasError)
                {
                    var editor = await LineContentEditorWindow.CreateInstance(lineContent);
                    await editor.PositionWindowAndShowOnUiThread();

                    //Allow execution to continue so Automation can continue
                    _ = editor.StatusContext.ShowMessageWithOkButton("Problem Saving",
                        saveGenerationReturn.GenerationNote);

                    continue;
                }

                statusContext.Progress($"New Line Editor - based on {loopRef.ContentId} ");

                await ThreadSwitcher.ResumeBackgroundAsync();
            }
            catch (Exception ex)
            {
                await statusContext.ToastError($"Error processing content {loopRef.ContentId}: {ex.Message}");
                Log.Error(ex, "Error processing content from other site: {ContentId}", loopRef.ContentId);
            }
        }
    }

    public static async Task HandleLinkContentReferences(List<ContentClipboardRepresentation> contentRefs,
        StatusControlContext statusContext)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        statusContext.Progress("Starting link content load.");

        if (contentRefs.Count > 10)
        {
            await statusContext.ToastError(
                "Dragging in new content is limited to 10 links at a time...");
            return;
        }

        await ThreadSwitcher.ResumeBackgroundAsync();

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
                    await statusContext.ToastError($"Failed to retrieve content data for {loopRef.ContentId}");
                    continue;
                }

                // Deserialize content data
                var linkContent = JsonSerializer.Deserialize<LinkContent>(contentJsonResponse,
                    JsonTools.WriteIndentedOptions);

                if (linkContent == null)
                {
                    await statusContext.ToastError($"Failed to parse content data for {loopRef.ContentId}");
                    continue;
                }

                linkContent.Id = 0;

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

                    continue;
                }

                statusContext.Progress($"New Link Editor - based on {loopRef.ContentId} ");

                await ThreadSwitcher.ResumeBackgroundAsync();
            }
            catch (Exception ex)
            {
                await statusContext.ToastError($"Error processing content {loopRef.ContentId}: {ex.Message}");
                Log.Error(ex, "Error processing content from other site: {ContentId}", loopRef.ContentId);
            }
        }
    }

    public static async Task HandleNoteContentReferences(List<ContentClipboardRepresentation> contentRefs,
        StatusControlContext statusContext)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        statusContext.Progress("Starting note content load.");

        if (contentRefs.Count > 10)
        {
            await statusContext.ToastError(
                "Dragging in new content is limited to 10 notes at a time...");
            return;
        }

        await ThreadSwitcher.ResumeBackgroundAsync();

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
                    await statusContext.ToastError($"Failed to retrieve content data for {loopRef.ContentId}");
                    continue;
                }

                // Deserialize content data
                var noteContent = JsonSerializer.Deserialize<NoteContent>(contentJsonResponse,
                    JsonTools.WriteIndentedOptions);

                if (noteContent == null)
                {
                    await statusContext.ToastError($"Failed to parse content data for {loopRef.ContentId}");
                    continue;
                }

                noteContent.Id = 0;

                var (saveGenerationReturn, _) = await NoteGenerator.SaveAndGenerateHtml(noteContent,
                    null, statusContext.ProgressTracker());

                var bracketCodeCheck = await CommonContentValidation.CheckStringForBadContentReferences(
                    $"{noteContent.BodyContent ?? string.Empty} {noteContent.Summary ?? string.Empty} {FakeMainPhotoBracketCode(noteContent.MainPicture)}", db,
                    statusContext.ProgressTracker());

                if (saveGenerationReturn.HasError || bracketCodeCheck.HasError)
                {
                    var editor = await NoteContentEditorWindow.CreateInstance(noteContent);
                    await editor.PositionWindowAndShowOnUiThread();

                    //Allow execution to continue so Automation can continue
                    _ = editor.StatusContext.ShowMessageWithOkButton("Problem Saving",
                        saveGenerationReturn.GenerationNote);

                    continue;
                }

                statusContext.Progress($"New Note Editor - based on {loopRef.ContentId} ");

                await ThreadSwitcher.ResumeBackgroundAsync();
            }
            catch (Exception ex)
            {
                await statusContext.ToastError($"Error processing content {loopRef.ContentId}: {ex.Message}");
                Log.Error(ex, "Error processing content from other site: {ContentId}", loopRef.ContentId);
            }
        }
    }

    public static async Task HandlePhotoContentReferences(List<ContentClipboardRepresentation> contentRefs,
        StatusControlContext statusContext)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        statusContext.Progress("Starting photo load.");

        if (contentRefs.Count > 10)
        {
            await statusContext.ToastError(
                "Dragging in new content is limited to 10 photos at a time...");
            return;
        }

        await ThreadSwitcher.ResumeBackgroundAsync();

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
                    await statusContext.ToastError($"Failed to retrieve content data for {loopRef.ContentId}");
                    continue;
                }

                // Deserialize content data
                var photoContent = JsonSerializer.Deserialize<PhotoContent>(contentJsonResponse,
                    JsonTools.WriteIndentedOptions);

                if (photoContent == null)
                {
                    await statusContext.ToastError($"Failed to parse content data for {loopRef.ContentId}");
                    continue;
                }

                photoContent.Id = 0;

                // Get the media file information for the content
                var mediaFileUrl = $"{loopRef.SiteLocalApiUrl}/mediafile/{loopRef.ContentId}";
                var mediaFileResponse = await httpClient.GetStringAsync(mediaFileUrl);

                if (string.IsNullOrEmpty(mediaFileResponse))
                {
                    await statusContext.ToastError($"Failed to retrieve media file data for {loopRef.ContentId}");
                    continue;
                }

                // Parse the media file information
                var mediaFileInfo = JsonSerializer.Deserialize<ContentMediaFileResponse>(
                    mediaFileResponse,
                    JsonTools.WriteIndentedOptions);

                if (mediaFileInfo is not { Exists: true })
                {
                    await statusContext.ToastError($"Media file not found for {loopRef.ContentId}");
                    continue;
                }

                var photoFile = new FileInfo(mediaFileInfo.FullPath);

                var (saveGenerationReturn, _) = await PhotoGenerator.SaveAndGenerateHtml(photoContent, photoFile, true,
                    null, statusContext.ProgressTracker());

                var bracketCodeCheck = await CommonContentValidation.CheckStringForBadContentReferences(
                    $"{photoContent.BodyContent ?? string.Empty} {photoContent.UpdateNotes ?? string.Empty} {FakeMainPhotoBracketCode(photoContent.MainPicture)}", db,
                    statusContext.ProgressTracker());

                if (saveGenerationReturn.HasError || bracketCodeCheck.HasError)
                {
                    var editor = await PhotoContentEditorWindow.CreateInstance(photoContent, false, photoFile);
                    await editor.PositionWindowAndShowOnUiThread();

                    //Allow execution to continue so Automation can continue
                    _ = editor.StatusContext.ShowMessageWithOkButton("Problem Saving",
                        saveGenerationReturn.GenerationNote);

                    continue;
                }

                statusContext.Progress($"New Photo Editor - based on {loopRef.ContentId} ");

                await ThreadSwitcher.ResumeBackgroundAsync();
            }
            catch (Exception ex)
            {
                await statusContext.ToastError($"Error processing content {loopRef.ContentId}: {ex.Message}");
                Log.Error(ex, "Error processing content from other site: {ContentId}", loopRef.ContentId);
            }
        }
    }

    public static async Task HandlePostContentReferences(List<ContentClipboardRepresentation> contentRefs,
        StatusControlContext statusContext)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        statusContext.Progress("Starting post content load.");

        if (contentRefs.Count > 10)
        {
            await statusContext.ToastError(
                "Dragging in new content is limited to 10 posts at a time...");
            return;
        }

        await ThreadSwitcher.ResumeBackgroundAsync();

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
                    await statusContext.ToastError($"Failed to retrieve content data for {loopRef.ContentId}");
                    continue;
                }

                // Deserialize content data
                var postContent = JsonSerializer.Deserialize<PostContent>(contentJsonResponse,
                    JsonTools.WriteIndentedOptions);

                if (postContent == null)
                {
                    await statusContext.ToastError($"Failed to parse content data for {loopRef.ContentId}");
                    continue;
                }

                postContent.Id = 0;

                var (saveGenerationReturn, _) = await PostGenerator.SaveAndGenerateHtml(postContent,
                    null, statusContext.ProgressTracker());

                var bracketCodeCheck = await CommonContentValidation.CheckStringForBadContentReferences(
                    $"{postContent.BodyContent ?? string.Empty} {postContent.UpdateNotes ?? string.Empty} {FakeMainPhotoBracketCode(postContent.MainPicture)}", db,
                    statusContext.ProgressTracker());

                if (saveGenerationReturn.HasError || bracketCodeCheck.HasError)
                {
                    var editor = await PostContentEditorWindow.CreateInstance(postContent);
                    await editor.PositionWindowAndShowOnUiThread();

                    //Allow execution to continue so Automation can continue
                    _ = editor.StatusContext.ShowMessageWithOkButton("Problem Saving",
                        saveGenerationReturn.GenerationNote);

                    continue;
                }

                statusContext.Progress($"New Post Editor - based on {loopRef.ContentId} ");

                await ThreadSwitcher.ResumeBackgroundAsync();
            }
            catch (Exception ex)
            {
                await statusContext.ToastError($"Error processing content {loopRef.ContentId}: {ex.Message}");
                Log.Error(ex, "Error processing content from other site: {ContentId}", loopRef.ContentId);
            }
        }
    }

    public static async Task HandleSnippetContentReferences(List<ContentClipboardRepresentation> contentRefs,
        StatusControlContext statusContext)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        statusContext.Progress("Starting snippet content load.");

        if (contentRefs.Count > 10)
        {
            await statusContext.ToastError(
                "Dragging in new content is limited to 10 snippets at a time...");
            return;
        }

        await ThreadSwitcher.ResumeBackgroundAsync();

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
                    await statusContext.ToastError($"Failed to retrieve content data for {loopRef.ContentId}");
                    continue;
                }

                // Deserialize content data
                var snippetContent = JsonSerializer.Deserialize<Snippet>(contentJsonResponse,
                    JsonTools.WriteIndentedOptions);

                if (snippetContent == null)
                {
                    await statusContext.ToastError($"Failed to parse content data for {loopRef.ContentId}");
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

                    continue;
                }

                statusContext.Progress($"New Snippet Editor - based on {loopRef.ContentId} ");

                await ThreadSwitcher.ResumeBackgroundAsync();
            }
            catch (Exception ex)
            {
                await statusContext.ToastError($"Error processing content {loopRef.ContentId}: {ex.Message}");
                Log.Error(ex, "Error processing content from other site: {ContentId}", loopRef.ContentId);
            }
        }
    }

    public static async Task HandleTrailContentReferences(List<ContentClipboardRepresentation> contentRefs,
        StatusControlContext statusContext)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        statusContext.Progress("Starting trail content load.");

        if (contentRefs.Count > 10)
        {
            await statusContext.ToastError(
                "Dragging in new content is limited to 10 trails at a time...");
            return;
        }

        await ThreadSwitcher.ResumeBackgroundAsync();

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
                    await statusContext.ToastError($"Failed to retrieve content data for {loopRef.ContentId}");
                    continue;
                }

                // Deserialize content data
                var trailContent = JsonSerializer.Deserialize<TrailContent>(contentJsonResponse,
                    JsonTools.WriteIndentedOptions);

                if (trailContent == null)
                {
                    await statusContext.ToastError($"Failed to parse content data for {loopRef.ContentId}");
                    continue;
                }

                trailContent.Id = 0;

                var (saveGenerationReturn, _) = await TrailGenerator.SaveAndGenerateHtml(trailContent,
                    null, statusContext.ProgressTracker());

                var bracketCodeCheck = await CommonContentValidation.CheckStringForBadContentReferences(
                    $"{trailContent.BodyContent ?? string.Empty} {trailContent.UpdateNotes ?? string.Empty} " +
                    $"{trailContent.BikesNote ?? string.Empty} {trailContent.DogsNote ?? string.Empty} " +
                    $"{trailContent.FeesNote ?? string.Empty} {trailContent.OtherDetails ?? string.Empty} {FakeMainPhotoBracketCode(trailContent.MainPicture)}", db,
                    statusContext.ProgressTracker());

                if (saveGenerationReturn.HasError || bracketCodeCheck.HasError)
                {
                    var editor = await TrailContentEditorWindow.CreateInstance(trailContent);
                    await editor.PositionWindowAndShowOnUiThread();

                    //Allow execution to continue so Automation can continue
                    _ = editor.StatusContext.ShowMessageWithOkButton("Problem Saving",
                        saveGenerationReturn.GenerationNote);

                    continue;
                }

                statusContext.Progress($"New Trail Editor - based on {loopRef.ContentId} ");

                await ThreadSwitcher.ResumeBackgroundAsync();
            }
            catch (Exception ex)
            {
                await statusContext.ToastError($"Error processing content {loopRef.ContentId}: {ex.Message}");
                Log.Error(ex, "Error processing content from other site: {ContentId}", loopRef.ContentId);
            }
        }
    }

    public static async Task HandleVideoContentReferences(List<ContentClipboardRepresentation> contentRefs,
        StatusControlContext statusContext)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        statusContext.Progress("Starting video content load.");

        if (contentRefs.Count > 10)
        {
            await statusContext.ToastError(
                "Dragging in new content is limited to 10 videos at a time...");
            return;
        }

        await ThreadSwitcher.ResumeBackgroundAsync();

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
                    await statusContext.ToastError($"Failed to retrieve content data for {loopRef.ContentId}");
                    continue;
                }

                // Deserialize content data
                var videoContent = JsonSerializer.Deserialize<VideoContent>(contentJsonResponse,
                    JsonTools.WriteIndentedOptions);

                if (videoContent == null)
                {
                    await statusContext.ToastError($"Failed to parse content data for {loopRef.ContentId}");
                    continue;
                }

                videoContent.Id = 0;

                // Get the media file information for the content
                var mediaFileUrl = $"{loopRef.SiteLocalApiUrl}/mediafile/{loopRef.ContentId}";
                var mediaFileResponse = await httpClient.GetStringAsync(mediaFileUrl);

                if (string.IsNullOrEmpty(mediaFileResponse))
                {
                    await statusContext.ToastError($"Failed to retrieve media file data for {loopRef.ContentId}");
                    continue;
                }

                // Parse the media file information
                var mediaFileInfo = JsonSerializer.Deserialize<ContentMediaFileResponse>(
                    mediaFileResponse,
                    JsonTools.WriteIndentedOptions);

                if (mediaFileInfo is not { Exists: true })
                {
                    await statusContext.ToastError($"Media file not found for {loopRef.ContentId}");
                    continue;
                }

                var videoFile = new FileInfo(mediaFileInfo.FullPath);

                var (saveGenerationReturn, _) = await VideoGenerator.SaveAndGenerateHtml(videoContent, videoFile,
                    null, statusContext.ProgressTracker());

                var bracketCodeCheck = await CommonContentValidation.CheckStringForBadContentReferences(
                    $"{videoContent.BodyContent ?? string.Empty} {videoContent.UpdateNotes ?? string.Empty} {FakeMainPhotoBracketCode(videoContent.MainPicture)}", db,
                    statusContext.ProgressTracker());

                if (saveGenerationReturn.HasError || bracketCodeCheck.HasError)
                {
                    var editor = await VideoContentEditorWindow.CreateInstance(videoContent);
                    await editor.PositionWindowAndShowOnUiThread();

                    //Allow execution to continue so Automation can continue
                    _ = editor.StatusContext.ShowMessageWithOkButton("Problem Saving",
                        saveGenerationReturn.GenerationNote);

                    continue;
                }

                statusContext.Progress($"New Video Editor - based on {loopRef.ContentId} ");

                await ThreadSwitcher.ResumeBackgroundAsync();
            }
            catch (Exception ex)
            {
                await statusContext.ToastError($"Error processing content {loopRef.ContentId}: {ex.Message}");
                Log.Error(ex, "Error processing content from other site: {ContentId}", loopRef.ContentId);
            }
        }
    }

    public static async Task HandleReferencesFromOtherSites(List<ContentClipboardRepresentation> contentRefs,
        StatusControlContext statusContext)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        statusContext.Progress($"Processing {contentRefs.Count} content items from other sites...");

        // For now, just show what we received
        var contentDescriptions =
            contentRefs.Select(c => $"Content Type: {c.ContentType}, ID: {c.ContentId}, From Site: {c.SiteId}");
        var message = string.Join(Environment.NewLine, contentDescriptions);

        await statusContext.ToastSuccess($"Received content references:{Environment.NewLine}{message}");

        // Find and process content references by type
        var fileContentRefs = contentRefs.Where(c =>
            c.ContentType.Equals(Db.ContentTypeDisplayStringForFile, StringComparison.OrdinalIgnoreCase)).ToList();
        if (fileContentRefs.Any())
        {
            statusContext.Progress($"Found {fileContentRefs.Count} file content items to import");
            await HandleFileContentReferences(fileContentRefs, statusContext);
        }

        var geoJsonContentRefs = contentRefs.Where(c =>
            c.ContentType.Equals(Db.ContentTypeDisplayStringForGeoJson, StringComparison.OrdinalIgnoreCase)).ToList();
        if (geoJsonContentRefs.Any())
        {
            statusContext.Progress($"Found {geoJsonContentRefs.Count} GeoJson content items to import");
            await HandleGeoJsonContentReferences(geoJsonContentRefs, statusContext);
        }

        var imageContentRefs = contentRefs.Where(c =>
            c.ContentType.Equals(Db.ContentTypeDisplayStringForImage, StringComparison.OrdinalIgnoreCase)).ToList();
        if (imageContentRefs.Any())
        {
            statusContext.Progress($"Found {imageContentRefs.Count} image content items to import");
            await HandleImageContentReferences(imageContentRefs, statusContext);
        }

        var lineContentRefs = contentRefs.Where(c =>
            c.ContentType.Equals(Db.ContentTypeDisplayStringForLine, StringComparison.OrdinalIgnoreCase)).ToList();
        if (lineContentRefs.Any())
        {
            statusContext.Progress($"Found {lineContentRefs.Count} line content items to import");
            await HandleLineContentReferences(lineContentRefs, statusContext);
        }

        var linkContentRefs = contentRefs.Where(c =>
            c.ContentType.Equals(Db.ContentTypeDisplayStringForLink, StringComparison.OrdinalIgnoreCase)).ToList();
        if (linkContentRefs.Any())
        {
            statusContext.Progress($"Found {linkContentRefs.Count} link content items to import");
            await HandleLinkContentReferences(linkContentRefs, statusContext);
        }

        var noteContentRefs = contentRefs.Where(c =>
            c.ContentType.Equals(Db.ContentTypeDisplayStringForNote, StringComparison.OrdinalIgnoreCase)).ToList();
        if (noteContentRefs.Any())
        {
            statusContext.Progress($"Found {noteContentRefs.Count} note content items to import");
            await HandleNoteContentReferences(noteContentRefs, statusContext);
        }

        var photoContentRefs = contentRefs.Where(c =>
            c.ContentType.Equals(Db.ContentTypeDisplayStringForPhoto, StringComparison.OrdinalIgnoreCase)).ToList();
        if (photoContentRefs.Any())
        {
            statusContext.Progress($"Found {photoContentRefs.Count} photo content items to import");
            await HandlePhotoContentReferences(photoContentRefs, statusContext);
        }

        var postContentRefs = contentRefs.Where(c =>
            c.ContentType.Equals(Db.ContentTypeDisplayStringForPost, StringComparison.OrdinalIgnoreCase)).ToList();
        if (postContentRefs.Any())
        {
            statusContext.Progress($"Found {postContentRefs.Count} post content items to import");
            await HandlePostContentReferences(postContentRefs, statusContext);
        }

        var snippetContentRefs = contentRefs.Where(c =>
            c.ContentType.Equals(Db.ContentTypeDisplayStringForSnippet, StringComparison.OrdinalIgnoreCase)).ToList();
        if (snippetContentRefs.Any())
        {
            statusContext.Progress($"Found {snippetContentRefs.Count} snippet content items to import");
            await HandleSnippetContentReferences(snippetContentRefs, statusContext);
        }

        var trailContentRefs = contentRefs.Where(c =>
            c.ContentType.Equals(Db.ContentTypeDisplayStringForTrail, StringComparison.OrdinalIgnoreCase)).ToList();
        if (trailContentRefs.Any())
        {
            statusContext.Progress($"Found {trailContentRefs.Count} trail content items to import");
            await HandleTrailContentReferences(trailContentRefs, statusContext);
        }

        var videoContentRefs = contentRefs.Where(c =>
            c.ContentType.Equals(Db.ContentTypeDisplayStringForVideo, StringComparison.OrdinalIgnoreCase)).ToList();
        if (videoContentRefs.Any())
        {
            statusContext.Progress($"Found {videoContentRefs.Count} video content items to import");
            await HandleVideoContentReferences(videoContentRefs, statusContext);
        }

        // Log that we received the content
        Log.Information("Received content references from other sites: {ContentRefs}",
            contentRefs.Select(c => new { c.ContentType, c.ContentId, c.SiteId, c.SiteLocalApiUrl }).ToList());
    }
}