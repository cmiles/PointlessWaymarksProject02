using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Ookii.Dialogs.Wpf;
using PointlessWaymarks.CmsData;
using PointlessWaymarks.CmsData.BracketCodes;
using PointlessWaymarks.CmsData.ContentGeneration;
using PointlessWaymarks.CmsData.ContentHtml.ImageHtml;
using PointlessWaymarks.CmsData.Database;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.CmsData.ImageHelpers;
using PointlessWaymarks.CmsWpfControls.ContentList;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.Status;
using PointlessWaymarks.WpfCommon.Utility;

namespace PointlessWaymarks.CmsWpfControls.ImageList;

[NotifyPropertyChanged]
[GenerateStatusCommands]
public partial class ImageListWithActionsContext
{
    private ImageListWithActionsContext(StatusControlContext statusContext, WindowIconStatus? windowStatus,
        ContentListContext listContext, bool loadInBackground = true)
    {
        StatusContext = statusContext;
        WindowStatus = windowStatus;
        CommonCommands = new CmsCommonCommands(StatusContext, WindowStatus);

        BuildCommands();

        ListContext = listContext;

        ListContext.ContextMenuItems =
        [
            new ContextMenuItemData { ItemName = "Edit", ItemCommand = ListContext.EditSelectedCommand },
            new ContextMenuItemData
            {
                ItemName = "Image Code to Clipboard",
                ItemCommand = ListContext.BracketCodeToClipboardSelectedCommand
            },

            new ContextMenuItemData
            {
                ItemName = "Text Code to Clipboard",
                ItemCommand = ImageBracketLinkCodesToClipboardForSelectedCommand
            },

            new ContextMenuItemData
            {
                ItemName = "Picture Gallery to Clipboard",
                ItemCommand = ListContext.PictureGalleryBracketCodeToClipboardSelectedCommand
            },

            new ContextMenuItemData { ItemName = "View Images - Individual", ItemCommand = ViewSelectedFilesCommand },
            new ContextMenuItemData
            {
                ItemName = "View Images - Group",
                ItemCommand = ListContext.PicturesAndVideosViewWindowSelectedCommand
            },
            new ContextMenuItemData
            {
                ItemName = "Add Intersection Tags - With OSM", ItemCommand = AddIntersectionTagsWithOsmToSelectedCommand
            },
            new ContextMenuItemData
            {
                ItemName = "Add Intersection Tags - Without OSM",
                ItemCommand = AddIntersectionTagsWithoutOsmToSelectedCommand
            },
            new ContextMenuItemData
            {
                ItemName = "View Intersection Tags",
                ItemCommand = ShowIntersectionTagsForSelectedCommand
            },
            new ContextMenuItemData { ItemName = "Open URL", ItemCommand = ListContext.ViewOnSiteCommand },

            new ContextMenuItemData { ItemName = "Delete", ItemCommand = ListContext.DeleteSelectedCommand },
            new ContextMenuItemData
            {
                ItemName = "Map Selected Items", ItemCommand = ListContext.SpatialItemsToContentMapWindowSelectedCommand
            },

            new ContextMenuItemData { ItemName = "Refresh Data", ItemCommand = RefreshDataCommand }
        ];

        if (loadInBackground) StatusContext.RunFireAndForgetBlockingTask(RefreshData);
    }

    public CmsCommonCommands CommonCommands { get; set; }
    public ContentListContext ListContext { get; set; }
    public StatusControlContext StatusContext { get; set; }
    public WindowIconStatus? WindowStatus { get; set; }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task AddIntersectionTagsWithOsmToSelected(CancellationToken cancellationToken)
    {
        await ImageActions.AddIntersectionTags(SelectedListItemsContent(), StatusContext, true, cancellationToken);
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task AddIntersectionTagsWithoutOsmToSelected(CancellationToken cancellationToken)
    {
        await ImageActions.AddIntersectionTags(SelectedListItemsContent(), StatusContext, false, cancellationToken);
    }

    public static async Task<ImageListWithActionsContext> CreateInstance(StatusControlContext? statusContext,
        WindowIconStatus? windowStatus = null, bool loadInBackground = true)
    {
        var factoryStatusContext = await StatusControlContext.CreateInstance(statusContext);

        await ThreadSwitcher.ResumeBackgroundAsync();
        var factoryListContext =
            await ContentListContext.CreateInstance(factoryStatusContext, new ImageListLoader(100),
                [Db.ContentTypeDisplayStringForImage], windowStatus);

        return new ImageListWithActionsContext(factoryStatusContext, windowStatus, factoryListContext,
            loadInBackground);
    }

    [BlockingCommand]
    [StopAndWarnIfNotOneSelectedListItems]
    public async Task EmailHtmlToClipboard()
    {
        var frozenSelected = SelectedListItems().First();

        var emailHtml = await Email.ToHtmlEmail(frozenSelected.DbEntry, StatusContext.ProgressTracker());

        await ThreadSwitcher.ResumeForegroundAsync();

        HtmlClipboardHelpers.CopyToClipboard(emailHtml, emailHtml);

        await StatusContext.ToastSuccess("Email Html on Clipboard");
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItemsAskIfOverMax(MaxSelectedItems = 10)]
    public async Task ExportFiles(CancellationToken cancellationToken)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (!SelectedListItems().Any())
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        var frozenSelect = SelectedListItems().ToList();

        await ThreadSwitcher.ResumeForegroundAsync();

        var dialog = new VistaFolderBrowserDialog
        {
            Description = "Select folder to export files to",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() != true) return;

        var exportDirectory = new DirectoryInfo(dialog.SelectedPath);

        if (!exportDirectory.Exists)
        {
            await StatusContext.ToastError("Selected directory does not exist?");
            return;
        }

        await ThreadSwitcher.ResumeBackgroundAsync();

        var exportedCount = 0;

        foreach (var loopSelected in frozenSelect)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileToExport = UserSettingsSingleton.CurrentSettings()
                .LocalMediaArchiveImageContentFile(loopSelected.DbEntry);

            if (fileToExport is not { Exists: true }) continue;

            var destinationFileName = UniqueFileTools.UniqueFile(exportDirectory, fileToExport.Name);

            File.Copy(fileToExport.FullName, destinationFileName!.FullName);
            exportedCount++;
        }

        if (exportedCount > 0)
            await StatusContext.ToastSuccess($"Exported {exportedCount} files to {exportDirectory.FullName}");
        else
            await StatusContext.ToastWarning("No files to export?");
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task ForcedResize(CancellationToken cancellationToken)
    {
        var totalCount = SelectedListItems().Count;
        var currentLoop = 0;

        foreach (var loopSelected in SelectedListItems())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (++currentLoop % 10 == 0)
                StatusContext.Progress($"Cleaning Generated Images And Resizing {currentLoop} of {totalCount} - " +
                                       $"{loopSelected.DbEntry.Title}");
            var resizeResult =
                await PictureResizing.CopyCleanResizeImage(loopSelected.DbEntry, StatusContext.ProgressTracker());

            if (!resizeResult.HasError) continue;

            PointlessWaymarksLogTools.LogGenerationReturn(resizeResult, "Image Forced Resizing");

            if (currentLoop < totalCount)
            {
                if (await StatusContext.ShowMessage("Error Resizing",
                        $"There was an error resizing the image {loopSelected.DbEntry.OriginalFileName} in {loopSelected.DbEntry.Title}{Environment.NewLine}{Environment.NewLine}{resizeResult.GenerationNote}{Environment.NewLine}{Environment.NewLine}Continue?",
                        ["Yes", "No"]) == "No") return;
            }
            else
            {
                await StatusContext.ShowMessageWithOkButton("Error Resizing",
                    $"There was an error resizing the image {loopSelected.DbEntry.OriginalFileName} in {loopSelected.DbEntry.Title}{Environment.NewLine}{Environment.NewLine}{resizeResult.GenerationNote}");
            }
        }
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task ImageBracketLinkCodesToClipboardForSelected()
    {
        var finalString = SelectedListItems().Aggregate(string.Empty,
            (current, loopSelected) =>
                current + $"{BracketCodeImageLinks.Create(loopSelected.DbEntry)}{Environment.NewLine}");

        await ThreadSwitcher.ResumeForegroundAsync();

        Clipboard.SetText(finalString);

        await StatusContext.ToastSuccess($"To Clipboard: {finalString}");
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task ImageTitleToFilename()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var frozenSelected = SelectedListItems();

        var settings = UserSettingsSingleton.CurrentSettings();

        var errors = new List<string>();
        var successCounter = 0;
        var skipCounter = 0;

        foreach (var loopImage in frozenSelected)
        {
            if (string.IsNullOrWhiteSpace(loopImage.DbEntry.Title))
            {
                skipCounter++;
                continue;
            }

            try
            {
                var selectedFile = settings.LocalMediaArchiveImageContentFile(loopImage.DbEntry);

                if (selectedFile is null)
                {
                    errors.Add($"{loopImage.DbEntry.Title} - No file found?");
                    continue;
                }

                if (selectedFile is not { Exists: true })
                {
                    errors.Add($"{loopImage.DbEntry.Title} - file {selectedFile.FullName} does not exist?");
                    continue;
                }

                var cleanedName = SlugTools.CreateSlug(false, loopImage.DbEntry.Title.TrimNullToEmpty());

                if (string.IsNullOrWhiteSpace(cleanedName))
                {
                    errors.Add($"{loopImage.DbEntry.Title} - Can't rename the file to an empty string...");
                    continue;
                }

                if (!FileAndFolderTools.IsNoUrlEncodingNeeded(cleanedName))
                {
                    errors.Add(
                        $"{loopImage.DbEntry.Title} - {cleanedName} - File Names must be limited to A - Z a - z 0 - 9 - . _");
                    continue;
                }

                if (string.Equals(loopImage.DbEntry.OriginalFileName,
                        $"{cleanedName}{Path.GetExtension(selectedFile.Name)}"))
                {
                    skipCounter++;
                    continue;
                }

                var moveToName = Path.Combine(selectedFile.Directory?.FullName ?? string.Empty,
                    $"{cleanedName}{Path.GetExtension(selectedFile.Name)}");

                // Check if a different file (not just case difference) already exists
                if (File.Exists(moveToName) &&
                    !string.Equals(selectedFile.FullName, moveToName, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"{loopImage.DbEntry.Title} - {moveToName} - Suggested new Filename Already Exists");
                    continue;
                }

                try
                {
                    // For case-only renames, we need to do a two-step rename on case-insensitive filesystems
                    if (string.Equals(selectedFile.FullName, moveToName, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(selectedFile.FullName, moveToName, StringComparison.Ordinal))
                    {
                        // Case-only rename: use temporary file
                        var tempName = Path.Combine(selectedFile.Directory?.FullName ?? string.Empty,
                            $"{Guid.NewGuid()}{Path.GetExtension(selectedFile.Name)}");
                        File.Move(selectedFile.FullName, tempName);
                        File.Move(tempName, moveToName);
                    }
                    else
                    {
                        // Normal copy
                        File.Copy(selectedFile.FullName, moveToName);
                    }
                }
                catch (Exception e)
                {
                    errors.Add($"{loopImage.DbEntry.Title} - {moveToName} - {e.Message}");
                    continue;
                }

                var finalFile = new FileInfo(moveToName);
                loopImage.DbEntry.OriginalFileName = finalFile.Name;

                if (string.IsNullOrWhiteSpace(loopImage.DbEntry.LastUpdatedBy))
                    loopImage.DbEntry.LastUpdatedBy = loopImage.DbEntry.CreatedBy;
                if (loopImage.DbEntry.LastUpdatedOn is null || loopImage.DbEntry.LastUpdatedOn == DateTime.MinValue)
                    loopImage.DbEntry.LastUpdatedOn = DateTime.Now;

                var saveResult = await ImageGenerator.SaveAndGenerateHtml(loopImage.DbEntry, finalFile, false, null,
                    StatusContext.ProgressTracker());

                if (saveResult.generationReturn.HasError)
                {
                    errors.Add(
                        $"{loopImage.DbEntry.Title} - {moveToName} - {saveResult.generationReturn.ToErrorString()}");
                    continue;
                }

                successCounter++;
            }
            catch (Exception e)
            {
                errors.Add($"{loopImage.DbEntry.Title} - {e.Message}");
            }
        }

        if (errors.Any())
            await StatusContext.ShowMessageWithOkButton("Errors Renaming",
                $"{successCounter} Succeeded, {skipCounter} Already Equal, {errors.Count} Failed: {Environment.NewLine}{Environment.NewLine}{string.Join($"{Environment.NewLine}{Environment.NewLine}", errors)}");
        else
            await StatusContext.ToastSuccess($"Renamed {successCounter} files, {skipCounter} Names already match.");
    }

    [BlockingCommand]
    private async Task RefreshData()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        await ListContext.LoadData();
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task RegenerateHtmlAndReprocessImageForSelected(CancellationToken cancellationToken)
    {
        var loopCount = 0;
        var totalCount = SelectedListItems().Count;

        var db = await Db.Context();

        var errorList = new List<string>();

        foreach (var loopSelected in SelectedListItems())
        {
            if (cancellationToken.IsCancellationRequested) break;

            loopCount++;

            if (loopSelected.DbEntry.Id < 1)
            {
                StatusContext.Progress(
                    $"Re-processing Image and Generating Html for {loopCount} of {totalCount} failed - no saved DB Entry?");
                errorList.Add("There was a list item without a saved DB entry? This should never happen...");
                continue;
            }

            var currentVersion = db.ImageContents.SingleOrDefault(x => x.ContentId == loopSelected.DbEntry.ContentId);

            if (currentVersion == null)
            {
                StatusContext.Progress(
                    $"Re-processing Image and Generating Html for {loopSelected.DbEntry.Title} failed - not found in DB, {loopCount} of {totalCount}");
                errorList.Add($"Image Titled {loopSelected.DbEntry.Title} was not found in the database?");
                continue;
            }

            if (string.IsNullOrWhiteSpace(currentVersion.LastUpdatedBy))
                currentVersion.LastUpdatedBy = currentVersion.CreatedBy;
            currentVersion.LastUpdatedOn = DateTime.Now;

            StatusContext.Progress(
                $"Re-processing Image and Generating Html for {loopSelected.DbEntry.Title}, {loopCount} of {totalCount}");

            var localMediaFiles = UserSettingsSingleton.CurrentSettings()
                .LocalMediaArchiveImageContentFile(currentVersion);

            if (localMediaFiles == null)
            {
                StatusContext.Progress(
                    $"Re-processing Image and Generating Html for {loopSelected.DbEntry.Title} failed - file not found in Media Library, {loopCount} of {totalCount}");
                errorList.Add($"Image Titled {loopSelected.DbEntry.Title} was not found in the Media Library?");
                continue;
            }

            var (generationReturn, _) = await ImageGenerator.SaveAndGenerateHtml(currentVersion, localMediaFiles
                , true, null,
                StatusContext.ProgressTracker());

            if (generationReturn.HasError)
            {
                PointlessWaymarksLogTools.LogGenerationReturn(generationReturn,
                    "Error with Image Resizing and HTML Regeneration");
                StatusContext.Progress(
                    $"Re-processing Image and Generating Html for {loopSelected.DbEntry.Title} Error {generationReturn.GenerationNote}, {generationReturn.Exception}, {loopCount} of {totalCount}");
                errorList.Add(
                    $"Error processing Image Titled {loopSelected.DbEntry.Title} - {generationReturn.GenerationNote}");
            }
        }

        if (errorList.Any())
        {
            errorList.Reverse();
            await StatusContext.ShowMessageWithOkButton("Errors Resizing and Regenerating HTML",
                string.Join($"{Environment.NewLine}{Environment.NewLine}", errorList));
        }
    }

    [NonBlockingCommand]
    public async Task ReportTitleAndFileNameDoNotMatch()
    {
        await RunReport(ReportTitleAndFileNameDoNotMatchGenerator, "Title and Filename Don't Match");
    }

    private async Task<List<object>> ReportTitleAndFileNameDoNotMatchGenerator()
    {
        var db = await Db.Context();

        var allContents = await db.ImageContents.OrderByDescending(x => x.CreatedOn).ToListAsync();

        var returnList = new List<ImageContent>();

        foreach (var loopContents in allContents)
        {
            var titleFilename = SlugTools.CreateSlug(false, loopContents.Title.TrimNullToEmpty());

            if (string.IsNullOrWhiteSpace(titleFilename)) returnList.Add(loopContents);

            if (string.Equals(titleFilename, Path.GetFileNameWithoutExtension(loopContents.OriginalFileName))) continue;

            returnList.Add(loopContents);
        }

        return returnList.Cast<object>().ToList();
    }

    private static async Task RunReport(Func<Task<List<object>>> toRun, string title)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var reportLoader = new ContentListLoaderReport(toRun);

        var newWindow =
            await ImageListWindow.CreateInstance(
                await CreateInstance(null, null));
        newWindow.WindowTitle = title;
        await newWindow.PositionWindowAndShowOnUiThread();
    }

    public List<ImageListListItem> SelectedListItems()
    {
        return ListContext.ListSelection.SelectedItems.Where(x => x is ImageListListItem).Cast<ImageListListItem>()
            .ToList();
    }

    public List<ImageContent> SelectedListItemsContent()
    {
        return ListContext.ListSelection.SelectedItems.Where(x => x is ImageListListItem).Cast<ImageListListItem>()
            .Select(x => x.DbEntry).ToList();
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task ShowIntersectionTagsForSelected(CancellationToken cancellationToken)
    {
        await ImageActions.ShowIntersectionTagsForSelected(SelectedListItemsContent(), StatusContext,
            cancellationToken);
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItemsAskIfOverMax(MaxSelectedItems = 10)]
    public async Task ViewSelectedFiles(CancellationToken cancelToken)
    {
        var currentSelected = SelectedListItems();

        foreach (var loopSelected in currentSelected)
        {
            cancelToken.ThrowIfCancellationRequested();

            await loopSelected.ItemActions.ViewFile(loopSelected.DbEntry);
        }
    }
}