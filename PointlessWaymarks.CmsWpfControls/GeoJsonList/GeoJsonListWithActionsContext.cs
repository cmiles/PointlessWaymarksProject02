using PointlessWaymarks.CmsData.Database;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.CmsWpfControls.ContentList;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.Status;
using PointlessWaymarks.WpfCommon.Utility;

namespace PointlessWaymarks.CmsWpfControls.GeoJsonList;

[NotifyPropertyChanged]
[GenerateStatusCommands]
public partial class GeoJsonListWithActionsContext
{
    private GeoJsonListWithActionsContext(StatusControlContext statusContext, WindowIconStatus? windowStatus,
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
                ItemName = "Map Code to Clipboard",
                ItemCommand = ListContext.BracketCodeToClipboardSelectedCommand
            },

            new ContextMenuItemData
            {
                ItemName = "Text Code to Clipboard", ItemCommand = TextBracketCodesToClipboardForSelectedCommand
            },

            new ContextMenuItemData
            {
                ItemName = "Image Code to Clipboard", ItemCommand = ImageBracketCodesToClipboardForSelectedCommand
            },

            new ContextMenuItemData
            {
                ItemName = "Picture Gallery to Clipboard",
                ItemCommand = ListContext.PictureGalleryBracketCodeToClipboardSelectedCommand
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
    public async Task AddIntersectionTagsWithOsmToSelected(CancellationToken cancellationToken)
    {
        await GeoJsonActions.AddIntersectionTags(SelectedListItemsContent(), StatusContext, true, cancellationToken);
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task AddIntersectionTagsWithoutOsmToSelected(CancellationToken cancellationToken)
    {
        await GeoJsonActions.AddIntersectionTags(SelectedListItemsContent(), StatusContext, false, cancellationToken);
    }

    public static async Task<GeoJsonListWithActionsContext> CreateInstance(StatusControlContext? statusContext,
        WindowIconStatus? windowStatus = null, bool loadInBackground = true)
    {
        var factoryStatusContext = await StatusControlContext.CreateInstance(statusContext);

        await ThreadSwitcher.ResumeBackgroundAsync();

        var factoryListContext =
            await ContentListContext.CreateInstance(factoryStatusContext, new GeoJsonListLoader(100),
                [Db.ContentTypeDisplayStringForGeoJson], windowStatus);

        return new GeoJsonListWithActionsContext(factoryStatusContext, windowStatus, factoryListContext,
            loadInBackground);
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task ImageBracketCodesToClipboardForSelected()
    {
        await GeoJsonActions.ImageBracketCodesToClipboard(SelectedListItemsContent(), StatusContext);
    }

    [BlockingCommand]
    public async Task RefreshData()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        await ListContext.LoadData();
    }

    public List<GeoJsonListListItem> SelectedListItems()
    {
        return ListContext.ListSelection.SelectedItems.Where(x => x is GeoJsonListListItem)
            .Cast<GeoJsonListListItem>().ToList();
    }

    public List<GeoJsonContent> SelectedListItemsContent()
    {
        return ListContext.ListSelection.SelectedItems.Where(x => x is GeoJsonListListItem).Cast<GeoJsonListListItem>()
            .Select(x => x.DbEntry).ToList();
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task ShowIntersectionTagsForSelected(CancellationToken cancellationToken)
    {
        await GeoJsonActions.ShowIntersectionTagsForSelected(SelectedListItemsContent(), StatusContext,
            cancellationToken);
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task TextBracketCodesToClipboardForSelected()
    {
        await GeoJsonActions.TextBracketCodesToClipboard(SelectedListItemsContent(), StatusContext);
    }
}