using System.IO;
using System.Windows;
using Ookii.Dialogs.Wpf;
using PointlessWaymarks.CmsData;
using PointlessWaymarks.CmsData.BracketCodes;
using PointlessWaymarks.CmsData.ContentHtml.FileHtml;
using PointlessWaymarks.CmsData.Database;
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
        WindowIconStatus? windowStatus, bool loadInBackground = true)
    {
        var factoryStatusContext = await StatusControlContext.CreateInstance(statusContext);

        await ThreadSwitcher.ResumeBackgroundAsync();

        var factoryListContext =
            await ContentListContext.CreateInstance(factoryStatusContext, new FileListLoader(100),
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