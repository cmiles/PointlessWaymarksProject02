using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Ookii.Dialogs.Wpf;
using PointlessWaymarks.CmsData;
using PointlessWaymarks.CmsData.BracketCodes;
using PointlessWaymarks.CmsData.ContentGeneration;
using PointlessWaymarks.CmsData.ContentHtml.FileHtml;
using PointlessWaymarks.CmsData.Database;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.CmsWpfControls.ContentList;
using PointlessWaymarks.CmsWpfControls.Utility;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.Status;
using PointlessWaymarks.WpfCommon.Utility;

namespace PointlessWaymarks.CmsWpfControls.FileList;

[NotifyPropertyChanged]
[GenerateStatusCommands]
public partial class FileListWithActionsContext : IListSelectionWithContext<FileListListItem>
{
    private FileListWithActionsContext(StatusControlContext statusContext, WindowIconStatus? windowStatus,
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
                ItemName = "Text Code to Clipboard",
                ItemCommand = FilePageLinkCodesToClipboardForSelectedCommand
            },

            new ContextMenuItemData
            {
                ItemName = "Image Code to Clipboard",
                ItemCommand = ListContext.BracketCodeToClipboardSelectedCommand
            },

            new ContextMenuItemData
            {
                ItemName = "Download Code to Clipboard",
                ItemCommand = FileDownloadLinkCodesToClipboardForSelectedCommand
            },

            new ContextMenuItemData
            {
                ItemName = "Embed Code to Clipboard",
                ItemCommand = FileEmbedCodesToClipboardForSelectedCommand
            },

            new ContextMenuItemData
            {
                ItemName = "URL Code to Clipboard", ItemCommand = FileUrlLinkCodesToClipboardForSelectedCommand
            },

            new ContextMenuItemData
            {
                ItemName = "Picture Gallery to Clipboard",
                ItemCommand = ListContext.PictureGalleryBracketCodeToClipboardSelectedCommand
            },

            new ContextMenuItemData { ItemName = "View Files", ItemCommand = ViewSelectedFilesCommand },
            new ContextMenuItemData { ItemName = "Open URL", ItemCommand = ListContext.ViewOnSiteCommand },
            new ContextMenuItemData { ItemName = "Delete", ItemCommand = ListContext.DeleteSelectedCommand },
            new ContextMenuItemData
            {
                ItemName = "Map Selected Items", ItemCommand = ListContext.SpatialItemsToContentMapWindowSelectedCommand
            },
            new ContextMenuItemData
            {
                ItemName = "View Selected Pictures",
                ItemCommand = ListContext.PicturesAndVideosViewWindowSelectedCommand
            },
            new ContextMenuItemData { ItemName = "Refresh Data", ItemCommand = RefreshDataCommand }
        ];

        if (loadInBackground) StatusContext.RunFireAndForgetBlockingTask(RefreshData);
    }

    public CmsCommonCommands CommonCommands { get; set; }
    public ContentListContext ListContext { get; set; }
    public WindowIconStatus? WindowStatus { get; set; }

    public FileListListItem? SelectedListItem()
    {
        return ListContext.ListSelection.Selected as FileListListItem;
    }

    public List<FileListListItem> SelectedListItems()
    {
        return ListContext.ListSelection.SelectedItems.Where(x => x is FileListListItem).Cast<FileListListItem>()
            .ToList();
    }

    public StatusControlContext StatusContext { get; set; }

    public static async Task<FileListWithActionsContext> CreateInstance(StatusControlContext? statusContext,
        WindowIconStatus? windowStatus, IContentListLoader? listLoader = null, bool loadInBackground = true)
    {
        var factoryStatusContext = await StatusControlContext.CreateInstance(statusContext);

        await ThreadSwitcher.ResumeBackgroundAsync();

        var factoryListContext =
            await ContentListContext.CreateInstance(factoryStatusContext, listLoader ?? new FileListLoader(100),
                [Db.ContentTypeDisplayStringForFile], windowStatus);

        return new FileListWithActionsContext(factoryStatusContext, windowStatus, factoryListContext, loadInBackground);
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
                .LocalMediaArchiveFileContentFile(loopSelected.DbEntry);

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
    public async Task FileDownloadLinkCodesToClipboardForSelected()
    {
        var finalString = string.Empty;

        foreach (var loopSelected in SelectedListItems())
            finalString += $"{BracketCodeFileDownloads.Create(loopSelected.DbEntry)}{Environment.NewLine}";

        await ThreadSwitcher.ResumeForegroundAsync();

        Clipboard.SetText(finalString);

        await StatusContext.ToastSuccess($"To Clipboard {finalString}");
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task FileEmbedCodesToClipboardForSelected()
    {
        var finalString = string.Empty;

        foreach (var loopSelected in SelectedListItems())
            finalString += $"{BracketCodeFileEmbed.Create(loopSelected.DbEntry)}{Environment.NewLine}";

        await ThreadSwitcher.ResumeForegroundAsync();

        Clipboard.SetText(finalString);

        await StatusContext.ToastSuccess($"To Clipboard {finalString}");
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task FileImageLinkCodesToClipboardForSelected()
    {
        var finalString = string.Empty;

        foreach (var loopSelected in SelectedListItems())
            finalString += $"{BracketCodeFileImageLink.Create(loopSelected.DbEntry)}{Environment.NewLine}";

        await ThreadSwitcher.ResumeForegroundAsync();

        Clipboard.SetText(finalString);

        await StatusContext.ToastSuccess($"To Clipboard {finalString}");
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task FilePageLinkCodesToClipboardForSelected()
    {
        var finalString = string.Empty;

        foreach (var loopSelected in SelectedListItems())
            finalString += $"{BracketCodeFiles.Create(loopSelected.DbEntry)}{Environment.NewLine}";

        await ThreadSwitcher.ResumeForegroundAsync();

        Clipboard.SetText(finalString);

        await StatusContext.ToastSuccess($"To Clipboard {finalString}");
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task FileTitleToFilename()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var frozenSelected = SelectedListItems();

        var settings = UserSettingsSingleton.CurrentSettings();

        var errors = new List<string>();
        var successCounter = 0;
        var skipCounter = 0;

        foreach (var loopFile in frozenSelected)
        {
            if (string.IsNullOrWhiteSpace(loopFile.DbEntry.Title))
            {
                skipCounter++;
                continue;
            }

            try
            {
                var selectedFile = settings.LocalMediaArchiveFileContentFile(loopFile.DbEntry);

                if (selectedFile is null)
                {
                    errors.Add($"{loopFile.DbEntry.Title} - No file found?");
                    continue;
                }

                if (selectedFile is not { Exists: true })
                {
                    errors.Add($"{loopFile.DbEntry.Title} - file {selectedFile.FullName} does not exist?");
                    continue;
                }

                var cleanedName = SlugTools.CreateSlug(false, loopFile.DbEntry.Title.TrimNullToEmpty());

                if (string.IsNullOrWhiteSpace(cleanedName))
                {
                    errors.Add($"{loopFile.DbEntry.Title} - Can't rename the file to an empty string...");
                    continue;
                }

                if (!FileAndFolderTools.IsNoUrlEncodingNeeded(cleanedName))
                {
                    errors.Add(
                        $"{loopFile.DbEntry.Title} - {cleanedName} - File Names must be limited to A - Z a - z 0 - 9 - . _");
                    continue;
                }

                if (string.Equals(loopFile.DbEntry.OriginalFileName,
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
                    errors.Add($"{loopFile.DbEntry.Title} - {moveToName} - Suggested new Filename Already Exists");
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
                    errors.Add($"{loopFile.DbEntry.Title} - {moveToName} - {e.Message}");
                    continue;
                }

                var finalFile = new FileInfo(moveToName);
                loopFile.DbEntry.OriginalFileName = finalFile.Name;

                if (string.IsNullOrWhiteSpace(loopFile.DbEntry.LastUpdatedBy))
                    loopFile.DbEntry.LastUpdatedBy = loopFile.DbEntry.CreatedBy;
                if (loopFile.DbEntry.LastUpdatedOn is null || loopFile.DbEntry.LastUpdatedOn == DateTime.MinValue)
                    loopFile.DbEntry.LastUpdatedOn = DateTime.Now;

                var saveResult = await FileGenerator.SaveAndGenerateHtml(loopFile.DbEntry, finalFile, null,
                    StatusContext.ProgressTracker());

                if (saveResult.generationReturn.HasError)
                {
                    errors.Add(
                        $"{loopFile.DbEntry.Title} - {moveToName} - {saveResult.generationReturn.ToErrorString()}");
                    continue;
                }

                successCounter++;
            }
            catch (Exception e)
            {
                errors.Add($"{loopFile.DbEntry.Title} - {e.Message}");
            }
        }

        if (errors.Any())
            await StatusContext.ShowMessageWithOkButton("Errors Renaming",
                $"{successCounter} Succeeded, {skipCounter} Already Equal, {errors.Count} Failed: {Environment.NewLine}{Environment.NewLine}{string.Join($"{Environment.NewLine}{Environment.NewLine}", errors)}");
        else
            await StatusContext.ToastSuccess($"Renamed {successCounter} files, {skipCounter} Names already match.");
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task FileUrlLinkCodesToClipboardForSelected()
    {
        var finalString = string.Empty;

        foreach (var loopSelected in SelectedListItems())
            finalString += $"{BracketCodeFileUrl.Create(loopSelected.DbEntry)}{Environment.NewLine}";

        await ThreadSwitcher.ResumeForegroundAsync();

        Clipboard.SetText(finalString);

        await StatusContext.ToastSuccess($"To Clipboard {finalString}");
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItemsAskIfOverMax(MaxSelectedItems = 10)]
    public async Task FirstPagePreviewFromPdf()
    {
        var frozenSelected = SelectedListItems();

        await ImageExtractionHelpers.PdfPageToImage(StatusContext, frozenSelected.Select(x => x.DbEntry).ToList(), 1);
    }

    [BlockingCommand]
    public async Task RefreshData()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        await ListContext.LoadData();
    }

    [NonBlockingCommand]
    public async Task ReportTitleAndFileNameDoNotMatch()
    {
        await RunReport(ReportTitleAndFileNameDoNotMatchGenerator, "Title and Filename Don't Match");
    }

    private async Task<List<object>> ReportTitleAndFileNameDoNotMatchGenerator()
    {
        var db = await Db.Context();

        var allContents = await db.FileContents.OrderByDescending(x => x.CreatedOn).ToListAsync();

        var returnList = new List<FileContent>();

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
            await FileListWindow.CreateInstance(
                await CreateInstance(null, null, reportLoader));
        newWindow.WindowTitle = title;
        await newWindow.PositionWindowAndShowOnUiThread();
    }

    public List<FileContent> SelectedListItemsContent()
    {
        return ListContext.ListSelection.SelectedItems.Where(x => x is FileListListItem).Cast<FileListListItem>()
            .Select(x => x.DbEntry).ToList();
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItemsAskIfOverMax(MaxSelectedItems = 10)]
    public async Task ViewSelectedFiles(CancellationToken cancelToken)
    {
        var frozenSelected = SelectedListItems();

        foreach (var loopSelected in frozenSelected)
        {
            cancelToken.ThrowIfCancellationRequested();
            await loopSelected.ItemActions.ViewFile(loopSelected.DbEntry);
        }
    }
}