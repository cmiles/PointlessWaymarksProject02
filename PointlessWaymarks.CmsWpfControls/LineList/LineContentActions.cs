using System.ComponentModel;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using PointlessWaymarks.CmsData;
using PointlessWaymarks.CmsData.ContentHtml.LineHtml;
using PointlessWaymarks.CmsData.Database;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.CmsWpfControls.ContentHistoryView;
using PointlessWaymarks.CmsWpfControls.ContentList;
using PointlessWaymarks.CmsWpfControls.ContentMap;
using PointlessWaymarks.CmsWpfControls.LineContentEditor;
using PointlessWaymarks.CmsWpfControls.PhotoList;
using PointlessWaymarks.CmsWpfControls.SitePreview;
using PointlessWaymarks.CmsWpfControls.Utility;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.Status;
using PointlessWaymarks.WpfCommon.Utility;

namespace PointlessWaymarks.CmsWpfControls.LineList;

[NotifyPropertyChanged]
[GenerateStatusCommands]
public partial class LineContentActions : IContentActions<LineContent>
{
    public LineContentActions(StatusControlContext statusContext)
    {
        StatusContext = statusContext;
        BuildCommands();
    }

    public ContentClipboardRepresentation ClipboardObject(LineContent? content)
    {
        return ContentClipboardRepresentation.ClipboardObject(content);
    }

    public string DefaultBracketCode(LineContent content)
    {
        return LineActions.DefaultBracketCode(content);
    }

    [BlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task DefaultBracketCodeToClipboard(LineContent? content)
    {
        await LineActions.DefaultBracketCodesToClipboard(content!.AsList(), StatusContext);
    }

    [BlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task Delete(LineContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (content!.Id < 1)
        {
            await StatusContext.ToastError($"Line {content.Title} - Entry is not saved - Skipping?");
            return;
        }

        var settings = UserSettingsSingleton.CurrentSettings();

        await Db.DeleteLineContent(content.ContentId, StatusContext.ProgressTracker());

        var possibleContentDirectory = settings.LocalSiteLineContentDirectory(content, false);
        if (possibleContentDirectory.Exists)
        {
            StatusContext.Progress($"Deleting Generated Folder {possibleContentDirectory.FullName}");
            possibleContentDirectory.Delete(true);
        }
    }

    [NonBlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task Edit(LineContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var context = await Db.Context();

        var refreshedData = context.LineContents.SingleOrDefault(x => x.ContentId == content!.ContentId);

        if (refreshedData == null)
            await StatusContext.ToastError(
                $"{content!.Title} is no longer active in the database? Can not edit - look for a historic version...");

        var newContentWindow = await LineContentEditorWindow.CreateInstance(refreshedData);

        await newContentWindow.PositionWindowAndShowOnUiThread();
    }

    [BlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task ExtractNewLinks(LineContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (content == null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        var context = await Db.Context();

        var refreshedData = context.LineContents.SingleOrDefault(x => x.ContentId == content.ContentId);

        if (refreshedData == null) return;

        await LinkExtraction.ExtractNewAndShowLinkContentEditors(
            $"{refreshedData.BodyContent} {refreshedData.UpdateNotes}", StatusContext.ProgressTracker());
    }

    [BlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task GenerateHtml(LineContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        StatusContext.Progress($"Generating Html for {content!.Title}");

        var htmlContext = new SingleLinePage(content);

        await htmlContext.WriteLocalHtml();

        await StatusContext.ToastSuccess($"Generated {htmlContext.PageUrl}");
    }

    public StatusControlContext StatusContext { get; set; }

    [NonBlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task ViewHistory(LineContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var db = await Db.Context();

        StatusContext.Progress($"Looking up Historic Entries for {content!.Title}");

        var historicItems = await db.HistoricLineContents.Where(x => x.ContentId == content.ContentId).ToListAsync();

        StatusContext.Progress($"Found {historicItems.Count} Historic Entries");

        if (historicItems.Count < 1)
        {
            await StatusContext.ToastWarning("No History to Show...");
            return;
        }

        var historicView = new ContentViewHistoryPage($"Historic Entries - {content.Title}",
            UserSettingsSingleton.CurrentSettings().SiteName, $"Historic Entries - {content.Title}",
            historicItems.OrderByDescending(x => x.LastUpdatedOn.HasValue).ThenByDescending(x => x.LastUpdatedOn)
                .Select(LogTools.SafeObjectDump).ToList());

        historicView.WriteHtmlToTempFolderAndShow(StatusContext.ProgressTracker());
    }

    [BlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task ViewOnSite(LineContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var settings = UserSettingsSingleton.CurrentSettings();

        var url = settings.LinePageUrl(content!);

        var ps = new ProcessStartInfo(url) { UseShellExecute = true, Verb = "open" };
        Process.Start(ps);
    }

    [BlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task ViewSitePreview(LineContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var settings = UserSettingsSingleton.CurrentSettings();

        var url = settings.LinePageUrl(content!);

        await ThreadSwitcher.ResumeForegroundAsync();

        var sitePreviewWindow = await SiteOnDiskPreviewWindow.CreateInstance(url);

        await sitePreviewWindow.PositionWindowAndShowOnUiThread();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    [BlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task AddIntersectionTagsWithOsm(LineContent? content, CancellationToken cancellationToken)
    {
        await LineActions.AddIntersectionTags(content!.AsList(), StatusContext, true, cancellationToken);
    }

    [BlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task AddIntersectionTagsWithoutOsm(LineContent? content, CancellationToken cancellationToken)
    {
        await LineActions.AddIntersectionTags(content!.AsList(), StatusContext, false, cancellationToken);
    }

    [BlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task ElevationChartBracketCodeToClipboard(LineContent? content)
    {
        await LineActions.ElevationChartBracketCodesToClipboard(content!.AsList(), StatusContext);
    }

    [BlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task GeoJsonToClipboard(LineContent? content)
    {
        await LineActions.GeoJsonToClipboard(content!.AsList(), StatusContext);
    }

    [BlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task LinkBracketCodeToClipboard(LineContent? content)
    {
        await LineActions.LinkBracketCodesToClipboard(content!.AsList(), StatusContext);
    }

    public static async Task<LineListListItem> ListItemFromDbItem(LineContent content, LineContentActions itemActions,
        bool showType)
    {
        var item = await LineListListItem.CreateInstance(itemActions);
        item.DbEntry = content;
        var (smallImageUrl, displayImageUrl) = ContentListContext.GetContentItemImageUrls(content);
        item.SmallImageUrl = smallImageUrl;
        item.DisplayImageUrl = displayImageUrl;
        item.ShowType = showType;
        return item;
    }

    /// <summary>
    ///     Uses the recorded on values of the Line Content to create a date range to search - this will always return a
    ///     valid date range but if you pass in LineContent with null for both Recorded on values you will get a 'Now' date
    ///     range (which is valid, but doesn't make sense) - you should guard against that in calling code
    /// </summary>
    /// <param name="content"></param>
    /// <returns></returns>
    public static (DateTime start, DateTime end) SearchRecordedDatesForPhotoContentDateRange(LineContent content)
    {
        var dateSearchStart = content.RecordingStartedOn?.Date ?? content.RecordingEndedOn?.Date ?? DateTime.Now.Date;
        var dateSearchEnd = content.RecordingEndedOn?.Date.AddDays(1) ??
                            content.RecordingStartedOn?.Date.AddDays(1) ?? DateTime.Now.Date.AddDays(1);

        return (dateSearchStart, dateSearchEnd);
    }

    [NonBlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task SearchRecordedOnDaysForPhotoContent(LineContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (content!.RecordingStartedOnUtc is null || content.RecordingEndedOnUtc is null)
        {
            await StatusContext.ToastError(
                "Line doesn't have Recorded On dates to work with? - Can not search for Photo Content");
            return;
        }

        var dateSearchStart = content.RecordingStartedOnUtc.Value.ToLocalTime().Date.ToUniversalTime();
        var dateSearchEnd = content.RecordingEndedOnUtc.Value.ToLocalTime().Date.AddDays(1).ToUniversalTime();

        await PhotoContentActions.RunReport(async () => await SearchRecordedOnDaysForPhotoContentFilter(content),
            $"Line {content.Title ?? string.Empty} - {dateSearchStart.ToLocalTime():M/d/yyyy} to {dateSearchEnd.ToLocalTime():M/d/yyyy}");
    }

    public async Task<List<object>> SearchRecordedOnDaysForPhotoContentFilter(LineContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (content == null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return [];
        }

        if (content.RecordingStartedOnUtc == null || content.RecordingEndedOnUtc == null)
        {
            await StatusContext.ToastError("Line doesn't have Recorded On UTC dates to work with?");
            return [];
        }

        var dateSearchStart = content.RecordingStartedOnUtc.Value.ToLocalTime().Date.ToUniversalTime();
        var dateSearchEnd = content.RecordingEndedOnUtc.Value.ToLocalTime().Date.AddDays(1).ToUniversalTime();

        var db = await Db.Context();

        return
            (await db.PhotoContents
                .Where(x =>
                    x.PhotoCreatedOnUtc != null
                        ? x.PhotoCreatedOnUtc >= dateSearchStart && x.PhotoCreatedOnUtc <= dateSearchEnd
                        : x.PhotoCreatedOn >= dateSearchStart.ToLocalTime() &&
                          x.PhotoCreatedOn <= dateSearchEnd.ToLocalTime())
                .ToListAsync()).Cast<object>().ToList();
    }

    [NonBlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task SearchRecordedOnForPhotoContent(LineContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (content!.RecordingStartedOnUtc is null || content.RecordingEndedOnUtc is null)
        {
            await StatusContext.ToastError(
                "Line doesn't have Recorded On dates to work with? - Can not search for Photo Content");
            return;
        }

        await PhotoContentActions.RunReport(async () => await SearchRecordedOnForPhotoContentFilter(content),
            $"Line {content.Title ?? string.Empty} - {content.RecordingStartedOnUtc.Value.AddMinutes(-5):M/d/yyyy hh:mm:ss tt} to {content.RecordingEndedOnUtc.Value.AddMinutes(5):M/d/yyyy hh:mm:ss tt}");
    }

    public async Task<List<object>> SearchRecordedOnForPhotoContentFilter(LineContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (content == null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return [];
        }

        if (content.RecordingStartedOnUtc == null || content.RecordingEndedOnUtc == null)
        {
            await StatusContext.ToastError("Line doesn't have Recorded On UTC dates to work with?");
            return [];
        }

        var dateSearchStart = content.RecordingStartedOnUtc.Value.AddMinutes(-5);
        var dateSearchEnd = content.RecordingEndedOnUtc.Value.AddMinutes(5);

        var db = await Db.Context();

        return
            (await db.PhotoContents
                .Where(x =>
                    x.PhotoCreatedOnUtc != null
                        ? x.PhotoCreatedOnUtc >= dateSearchStart && x.PhotoCreatedOnUtc <= dateSearchEnd
                        : x.PhotoCreatedOn >= dateSearchStart.ToLocalTime() &&
                          x.PhotoCreatedOn <= dateSearchEnd.ToLocalTime())
                .ToListAsync()).Cast<object>().ToList();
    }

    [BlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task ShowIntersectionTags(LineContent? content, CancellationToken cancellationToken)
    {
        await LineActions.ShowIntersectionTagsForSelected(content!.AsList(), StatusContext, cancellationToken);
    }

    [NonBlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task ShowOnMap(LineContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (content!.Id < 1)
        {
            await StatusContext.ToastError("Entry is not saved - Skipping?");
            return;
        }

        await ThreadSwitcher.ResumeForegroundAsync();

        var mapWindow =
            await ContentMapWindow.CreateInstance(new ContentMapListLoader("Mapped Content",
                [content.ContentId]));

        await mapWindow.PositionWindowAndShowOnUiThread();
    }

    [BlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task StatsBracketCodeToClipboard(LineContent? content)
    {
        await LineActions.StatsBracketCodesToClipboard(content!.AsList(), StatusContext);
    }

    [BlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task TextStatsBracketCodeToClipboard(LineContent? content)
    {
        await LineActions.TextStatsBracketCodesToClipboard(content!.AsList(), StatusContext);
    }

    [BlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task ToGpxFile(LineContent? content)
    {
        await LineActions.ToGpxFile(content!.AsList(), StatusContext);
    }
}