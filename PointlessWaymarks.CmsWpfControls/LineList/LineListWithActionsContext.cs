using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using ClosedXML.Excel;
using MathNet.Numerics;
using Microsoft.EntityFrameworkCore;
using PointlessWaymarks.CmsData;
using PointlessWaymarks.CmsData.ContentGeneration;
using PointlessWaymarks.CmsData.ContentHtml.LineMonthlyActivitySummaryHtml;
using PointlessWaymarks.CmsData.Database;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.CmsWpfControls.ContentList;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.Status;
using PointlessWaymarks.WpfCommon.Utility;

namespace PointlessWaymarks.CmsWpfControls.LineList;

[NotifyPropertyChanged]
[GenerateStatusCommands]
public partial class LineListWithActionsContext
{
    private LineListWithActionsContext(StatusControlContext statusContext, WindowIconStatus? windowStatus,
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
                { ItemName = "Text Code to Clipboard", ItemCommand = LinkBracketCodesToClipboardForSelectedCommand },
            new ContextMenuItemData
            {
                ItemName = "Stats Text Code to Clipboard",
                ItemCommand = TextStatsBracketCodesToClipboardForSelectedCommand
            },
            new ContextMenuItemData
            {
                ItemName = "Stats Block Code to Clipboard", ItemCommand = StatsBracketCodesToClipboardForSelectedCommand
            },
            new ContextMenuItemData
            {
                ItemName = "Elevation Chart Code to Clipboard",
                ItemCommand = ElevationChartBracketCodesToClipboardForSelectedCommand
            },
            new ContextMenuItemData
            {
                ItemName = "Picture Gallery to Clipboard",
                ItemCommand = ListContext.PictureGalleryBracketCodeToClipboardSelectedCommand
            },
            new ContextMenuItemData
            {
                ItemName = "GeoJson to Clipboard", ItemCommand = GeoJsonToClipboardForSelectedCommand
            },
            new ContextMenuItemData
            {
                ItemName = "Save Selected to Gpx File - Single File", ItemCommand = SelectedToGpxFileCommand
            },
            new ContextMenuItemData
            {
                ItemName = "Save Selected to Gpx File - Individual Files", ItemCommand = SelectedToGpxFilesCommand
            },
            new ContextMenuItemData
            {
                ItemName = "Activity Log Monthly Stats Window",
                ItemCommand = ActivityLogMonthlyStatsWindowForSelectedCommand
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
            new ContextMenuItemData
            {
                ItemName = "Re-Save Selected", ItemCommand = ResaveSelectedCommand
            },
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
    public async Task ActivityLogMonthlyStatsWindowForAllLineContent()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var db = await Db.Context();

        var allActivities =
            await db.LineContents.LineContentFilteredForActivities().Select(x => x.ContentId).ToListAsync();

        var window =
            await ActivityLogMonthlySummaryWindow.CreateInstance(allActivities);

        await window.PositionWindowAndShowOnUiThread();
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task ActivityLogMonthlyStatsWindowForSelected()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var frozenSelected = SelectedListItems();

        var window =
            await ActivityLogMonthlySummaryWindow.CreateInstance(frozenSelected.Select(x => x.DbEntry.ContentId)
                .ToList());

        await window.PositionWindowAndShowOnUiThread();
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task AddIntersectionTagsWithOsmToSelected(CancellationToken cancellationToken)
    {
        await LineActions.AddIntersectionTags(SelectedListItemsContent(), StatusContext, true, cancellationToken);
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task AddIntersectionTagsWithoutOsmToSelected(CancellationToken cancellationToken)
    {
        await LineActions.AddIntersectionTags(SelectedListItemsContent(), StatusContext, false, cancellationToken);
    }

    public static async Task<LineListWithActionsContext> CreateInstance(StatusControlContext? statusContext,
        WindowIconStatus? windowStatus = null, bool loadInBackground = true)
    {
        var factoryStatusContext = await StatusControlContext.CreateInstance(statusContext);

        await ThreadSwitcher.ResumeBackgroundAsync();
        var factoryListContext =
            await ContentListContext.CreateInstance(factoryStatusContext, new LineListLoader(100),
                [Db.ContentTypeDisplayStringForLine], windowStatus);

        return new LineListWithActionsContext(factoryStatusContext, windowStatus, factoryListContext, loadInBackground);
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task ElevationChartBracketCodesToClipboardForSelected()
    {
        await LineActions.ElevationChartBracketCodesToClipboard(SelectedListItemsContent(), StatusContext);
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItemsAskIfOverMax(MaxSelectedItems = 3, ActionVerb = "copy to clipboard")]
    public async Task GeoJsonToClipboardForSelected()
    {
        await LineActions.GeoJsonToClipboard(SelectedListItemsContent(), StatusContext);
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task LineStatsToExcelForSelected()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var selectedItems = SelectedListItems();
        StatusContext.Progress($"Starting transfer of {selectedItems.Count} to Excel");

        var file = Path.Combine(FileLocationTools.TempStorageDirectory().FullName,
            $"{DateTime.Now:yyyy-MM-dd--HH-mm-ss}---{FileAndFolderTools.TryMakeFilenameValid("LineStatistics")}.xlsx");

        var settings = UserSettingsSingleton.CurrentSettings();

        var projectedItems = selectedItems.Select(x => new
        {
            x.DbEntry.Folder,
            x.DbEntry.Title,
            x.DbEntry.LineDistance,
            x.DbEntry.ClimbElevation,
            x.DbEntry.DescentElevation,
            x.DbEntry.MinimumElevation,
            x.DbEntry.MaximumElevation,
            Hours = x.DbEntry is { RecordingStartedOn: null, RecordingEndedOn: null }
                ? null
                : (x.DbEntry.RecordingEndedOn - x.DbEntry.RecordingStartedOn)?.TotalHours.Round(2),
            x.DbEntry.ActivityType,
            x.DbEntry.RecordingStartedOn,
            x.DbEntry.Tags,
            Url = settings.LinePageUrl(x.DbEntry)
        });

        StatusContext.Progress($"File Name: {file}");

        StatusContext.Progress("Creating Workbook");

        var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Exported Data");

        StatusContext.Progress("Inserting Data");

        var table = ws.Cell(1, 1).InsertTable(projectedItems);

        StatusContext.Progress("Applying Formatting");

        foreach (var loopRow in table.DataRange.Rows())
            loopRow.Cell(2).SetHyperlink(new XLHyperlink(loopRow.Cell(12).GetString()));

        table.DataRange.Column(3).Style.NumberFormat.Format = "#0.0";
        table.DataRange.Column(4).Style.NumberFormat.Format = "#,##0";
        table.DataRange.Column(5).Style.NumberFormat.Format = "#,##0";
        table.DataRange.Column(6).Style.NumberFormat.Format = "#,##0";
        table.DataRange.Column(7).Style.NumberFormat.Format = "#,##0";
        table.DataRange.Column(8).Style.NumberFormat.Format = "#,##0.0";
        table.DataRange.Column(10).Style.NumberFormat.Format = "yyyy-mm-dd h AM/PM";

        ws.Columns().AdjustToContents();

        foreach (var loopColumn in ws.ColumnsUsed().Where(x => x.Width > 70))
        {
            loopColumn.Width = 70;
            loopColumn.Style.Alignment.WrapText = true;
        }

        ws.Rows().AdjustToContents();

        foreach (var loopRow in ws.RowsUsed().Where(x => x.Height > 70))
            loopRow.Height = 70;

        StatusContext.Progress($"Saving Excel File {file}");

        wb.SaveAs(file);

        StatusContext.Progress($"Opening Excel File {file}");

        var ps = new ProcessStartInfo(file) { UseShellExecute = true, Verb = "open" };
        Process.Start(ps);
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task LinkBracketCodesToClipboardForSelected()
    {
        await LineActions.LinkBracketCodesToClipboard(SelectedListItemsContent(), StatusContext);
    }

    [BlockingCommand]
    public async Task RefreshData()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        await ListContext.LoadData();
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task ResaveSelected()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var selectedIds = SelectedListItems().Select(x => x.ContentId()).Where(x => x is not null).ToList();

        var db = await Db.Context().ConfigureAwait(false);

        var selectedToSave = await db.LineContents.Where(x => selectedIds.Contains(x.ContentId)).OrderBy(x => x.Title)
            .ToListAsync().ConfigureAwait(false);

        var totalCount = selectedToSave.Count;

        StatusContext.Progress($"Found {totalCount} Lines to Generate");

        var generationVersion = DateTime.Now.TrimDateTimeToSeconds().ToUniversalTime();

        ConcurrentBag<GenerationReturn> generationReturns = [];

        await Parallel.ForEachAsync(selectedToSave, async (loopItem, _) =>
        {
            StatusContext.Progress($"Saving and Writing HTML for Line {loopItem.Title}");

            generationReturns.Add(
                (await LineGenerator.SaveAndGenerateHtml(loopItem, generationVersion, StatusContext.ProgressTracker()))
                .generationReturn);
        }).ConfigureAwait(false);

        if (generationReturns.Any(x => x.HasError))
        {
            await StatusContext.ShowMessageWithOkButton("Error Saving Lines",
                string.Join(Environment.NewLine + Environment.NewLine,
                    generationReturns.Where(x => x.HasError).Select(x => x.ToErrorString())));
            return;
        }

        await MapComponentGenerator.GenerateAllLinesData();
        await MapComponentGenerator.GenerateAllActivityAnonymousDataFile();
        await new LineMonthlyActivitySummaryPage(generationVersion).WriteLocalHtml();
    }

    public List<LineListListItem> SelectedListItems()
    {
        return ListContext.ListSelection.SelectedItems.Where(x => x is LineListListItem).Cast<LineListListItem>()
            .ToList();
    }

    public List<LineContent> SelectedListItemsContent()
    {
        return ListContext.ListSelection.SelectedItems.Where(x => x is LineListListItem).Cast<LineListListItem>()
            .Select(x => x.DbEntry).ToList();
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task SelectedToGpxFile()
    {
        await LineActions.ToGpxFile(SelectedListItemsContent(), StatusContext);
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task SelectedToGpxFiles()
    {
        await LineActions.ToGpxFiles(SelectedListItemsContent(), StatusContext);
    }

    [BlockingCommand]
    [StopAndWarnIfNoOrMoreThanSelectedListItems(MaxSelectedItems = 5)]
    public async Task ShowIntersectionTagsForSelected(CancellationToken cancellationToken)
    {
        await LineActions.ShowIntersectionTagsForSelected(SelectedListItemsContent(), StatusContext, cancellationToken);
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task StatsBracketCodesToClipboardForSelected()
    {
        await LineActions.StatsBracketCodesToClipboard(SelectedListItemsContent(), StatusContext);
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task TextStatsBracketCodesToClipboardForSelected()
    {
        await LineActions.TextStatsBracketCodesToClipboard(SelectedListItemsContent(), StatusContext);
    }
}