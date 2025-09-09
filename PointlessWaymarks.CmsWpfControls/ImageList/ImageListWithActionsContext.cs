using System.IO;
using System.Windows;
using Ookii.Dialogs.Wpf;
using PointlessWaymarks.CmsData;
using PointlessWaymarks.CmsData.BracketCodes;
using PointlessWaymarks.CmsData.ContentGeneration;
using PointlessWaymarks.CmsData.ContentHtml.ImageHtml;
using PointlessWaymarks.CmsData.Database;
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

    public List<ImageListListItem> SelectedListItems()
    {
        return ListContext.ListSelection.SelectedItems.Where(x => x is ImageListListItem).Cast<ImageListListItem>()
            .ToList();
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