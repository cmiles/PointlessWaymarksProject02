using PointlessWaymarks.CmsData.Database;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.CmsWpfControls.ContentList;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.Status;
using PointlessWaymarks.WpfCommon.Utility;

namespace PointlessWaymarks.CmsWpfControls.TrailList;

[NotifyPropertyChanged]
[GenerateStatusCommands]
public partial class TrailListWithActionsContext
{
    private TrailListWithActionsContext(StatusControlContext statusContext, WindowIconStatus? windowStatus,
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
                ItemCommand = ListContext.BracketCodeToClipboardSelectedCommand
            },
            new ContextMenuItemData
            {
                ItemName = "Image Code to Clipboard", ItemCommand = ImageBracketCodesToClipboardForSelectedCommand
            },
            new ContextMenuItemData
            {
                ItemName = "Stats Code to Clipboard", ItemCommand = TextStatsBracketCodesToClipboardForSelectedCommand
            },
            new ContextMenuItemData
            {
                ItemName = "Extended Stats Code to Clipboard",
                ItemCommand = TextStatsExtendedBracketCodesToClipboardForSelectedCommand
            },
            new ContextMenuItemData
            {
                ItemName = "Picture Block to Clipboard",
                ItemCommand = ListContext.PictureBlockBracketCodeToClipboardSelectedCommand
            },
            new ContextMenuItemData
            {
                ItemName = "Picture Gallery to Clipboard",
                ItemCommand = ListContext.PictureGalleryBracketCodeToClipboardSelectedCommand
            },
            new ContextMenuItemData
                { ItemName = "Extract New Links", ItemCommand = ListContext.ExtractNewLinksSelectedCommand },
            new ContextMenuItemData { ItemName = "Open URL", ItemCommand = ListContext.ViewOnSiteCommand },
            new ContextMenuItemData { ItemName = "Delete", ItemCommand = ListContext.DeleteSelectedCommand },
            new ContextMenuItemData { ItemName = "View History", ItemCommand = ListContext.ViewHistorySelectedCommand },
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
    public StatusControlContext StatusContext { get; set; }
    public WindowIconStatus? WindowStatus { get; set; }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task BracketCodesToClipboardForSelected()
    {
        await TrailActions.BracketCodesToClipboard(SelectedListItemsContent(), StatusContext);
    }

    public static async Task<TrailListWithActionsContext> CreateInstance(StatusControlContext? statusContext,
        WindowIconStatus? windowStatus = null, IContentListLoader? listLoader = null, bool loadInBackground = true)
    {
        var factoryStatusContext = await StatusControlContext.CreateInstance(statusContext);

        await ThreadSwitcher.ResumeBackgroundAsync();

        var factoryListContext =
            await ContentListContext.CreateInstance(factoryStatusContext, listLoader ?? new TrailListLoader(100),
                [Db.ContentTypeDisplayStringForTrail], windowStatus);

        return new TrailListWithActionsContext(factoryStatusContext, windowStatus, factoryListContext,
            loadInBackground);
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task ImageBracketCodesToClipboardForSelected()
    {
        await TrailActions.ImageBracketCodesToClipboard(SelectedListItemsContent(), StatusContext);
    }

    [BlockingCommand]
    public async Task RefreshData()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        await ListContext.LoadData();
    }

    public List<TrailListListItem> SelectedListItems()
    {
        return ListContext.ListSelection.SelectedItems.Where(x => x is TrailListListItem).Cast<TrailListListItem>()
            .ToList();
    }

    public List<TrailContent> SelectedListItemsContent()
    {
        return ListContext.ListSelection.SelectedItems.Where(x => x is TrailListListItem).Cast<TrailListListItem>()
            .Select(x => x.DbEntry).ToList();
    }


    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task TextStatsBracketCodesToClipboardForSelected()
    {
        await TrailActions.TextStatsBracketCodesToClipboard(SelectedListItemsContent(), StatusContext);
    }


    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task TextStatsExtendedBracketCodesToClipboardForSelected()
    {
        await TrailActions.TextStatsExtendedBracketCodesToClipboard(SelectedListItemsContent(), StatusContext);
    }
}