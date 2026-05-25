using PointlessWaymarks.CmsData.Database;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.CmsWpfControls.ContentList;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.Status;
using PointlessWaymarks.WpfCommon.Utility;

namespace PointlessWaymarks.CmsWpfControls.PointList;

[NotifyPropertyChanged]
[GenerateStatusCommands]
public partial class PointListWithActionsContext
{
    private PointListWithActionsContext(StatusControlContext statusContext, WindowIconStatus? windowStatus,
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
                ItemName = "Text Code to Clipboard",
                ItemCommand = TextBracketCodesToClipboardForSelectedCommand
            },
            new ContextMenuItemData
            {
                ItemName = "Point Details Code to Clipboard",
                ItemCommand = PointDetailsBracketCodesToClipboardForSelectedCommand
            },
            new ContextMenuItemData
            {
                ItemName = "Image Code to Clipboard",
                ItemCommand = ImageBracketCodesToClipboardForSelectedCommand
            },
            new ContextMenuItemData
            {
                ItemName = "External Directions Code to Clipboard",
                ItemCommand = ExternalDirectionsBracketCodesToClipboardForSelectedCommand
            },
            new ContextMenuItemData
            {
                ItemName = "Google Maps Point Code to Clipboard",
                ItemCommand = GoogleMapsBracketCodesToClipboardForSelectedCommand
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
                { ItemName = "Selected Points to GPX File", ItemCommand = SelectedToGpxFileCommand },
            new ContextMenuItemData
            {
                ItemName = "Selected Points to Clipboard - GeoJson", ItemCommand = GeoJsonToClipboardForSelectedCommand
            },
            new ContextMenuItemData
            {
                ItemName = "Selected Points Coordinates to Clipboard - Text",
                ItemCommand = CoordinatesToClipboardForSelectedCommand
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
        await PointActions.AddIntersectionTags(SelectedListItemsContent(), StatusContext, true, cancellationToken);
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task AddIntersectionTagsWithoutOsmToSelected(CancellationToken cancellationToken)
    {
        await PointActions.AddIntersectionTags(SelectedListItemsContent(), StatusContext, false, cancellationToken);
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task CoordinatesToClipboardForSelected()
    {
        await PointActions.CoordinateTextToClipboard(SelectedListItemsContent(), StatusContext);
    }

    public static async Task<PointListWithActionsContext> CreateInstance(StatusControlContext? statusContext,
        WindowIconStatus? windowStatus = null, IContentListLoader? listLoader = null, bool loadInBackground = true)
    {
        var factoryStatusContext = await StatusControlContext.CreateInstance(statusContext);

        await ThreadSwitcher.ResumeBackgroundAsync();

        var factoryListContext =
            await ContentListContext.CreateInstance(factoryStatusContext, listLoader ?? new PointListLoader(100),
                [Db.ContentTypeDisplayStringForPoint], windowStatus);

        return new PointListWithActionsContext(factoryStatusContext, windowStatus, factoryListContext,
            loadInBackground);
    }

    [NonBlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task ExternalDirectionsBracketCodesToClipboardForSelected()
    {
        await PointActions.ExternalDirectionsBracketCodesToClipboard(SelectedListItemsContent(), StatusContext);
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItemsAskIfOverMax(MaxSelectedItems = 100, ActionVerb = "copy to clipboard")]
    public async Task GeoJsonToClipboardForSelected()
    {
        await PointActions.GeoJsonToClipboard(SelectedListItemsContent(), StatusContext);
    }

    [NonBlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task GoogleMapsBracketCodesToClipboardForSelected()
    {
        await PointActions.GoogleMapsBracketCodesToClipboard(SelectedListItemsContent(), StatusContext);
    }


    [NonBlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task ImageBracketCodesToClipboardForSelected()
    {
        await PointActions.ImageBracketCodesToClipboard(SelectedListItemsContent(), StatusContext);
    }

    [NonBlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task PointDetailsBracketCodesToClipboardForSelected()
    {
        await PointActions.PointDetailsBracketCodesToClipboard(SelectedListItemsContent(), StatusContext);
    }

    [BlockingCommand]
    public async Task RefreshData()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        await ListContext.LoadData();
    }

    public List<PointListListItem> SelectedListItems()
    {
        return ListContext.ListSelection.SelectedItems.Where(x => x is PointListListItem).Cast<PointListListItem>()
            .ToList();
    }

    public List<PointContentDto> SelectedListItemsContent()
    {
        return ListContext.ListSelection.SelectedItems.Where(x => x is PointListListItem).Cast<PointListListItem>()
            .Select(x => x.DbEntry).ToList();
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task SelectedToGpxFile()
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        await PointActions.ToGpxFile(SelectedListItemsContent(), StatusContext);
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task ShowIntersectionTagsForSelected(CancellationToken cancellationToken)
    {
        await PointActions.ShowIntersectionTagsForSelected(SelectedListItemsContent(), StatusContext,
            cancellationToken);
    }

    [NonBlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task TextBracketCodesToClipboardForSelected()
    {
        await PointActions.TextBracketCodesToClipboard(SelectedListItemsContent(), StatusContext);
    }
}