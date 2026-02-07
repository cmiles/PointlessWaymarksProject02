using System.ComponentModel;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using PointlessWaymarks.CmsData;
using PointlessWaymarks.CmsData.ContentHtml.PointHtml;
using PointlessWaymarks.CmsData.Database;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.CmsWpfControls.ContentHistoryView;
using PointlessWaymarks.CmsWpfControls.ContentList;
using PointlessWaymarks.CmsWpfControls.ContentMap;
using PointlessWaymarks.CmsWpfControls.PointContentEditor;
using PointlessWaymarks.CmsWpfControls.SitePreview;
using PointlessWaymarks.CmsWpfControls.Utility;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.Status;
using PointlessWaymarks.WpfCommon.Utility;

namespace PointlessWaymarks.CmsWpfControls.PointList;

[NotifyPropertyChanged]
[GenerateStatusCommands]
public partial class PointContentActions : IContentActions<PointContentDto>
{
    public PointContentActions(StatusControlContext statusContext)
    {
        StatusContext = statusContext;
        BuildCommands();
    }

    public ContentClipboardRepresentation ClipboardObject(PointContentDto? content)
    {
        return ContentClipboardRepresentation.ClipboardObject(content);
    }

    public string DefaultBracketCode(PointContentDto? content)
    {
        return PointActions.DefaultBracketCode(content);
    }

    [BlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task DefaultBracketCodeToClipboard(PointContentDto? content)
    {
        await PointActions.DefaultBracketCodesToClipboard(content!.AsList(), StatusContext);
    }

    [BlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task Delete(PointContentDto? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (content!.Id < 1)
        {
            await StatusContext.ToastError($"Point {content.Title} - Entry is not saved - Skipping?");
            return;
        }

        var settings = UserSettingsSingleton.CurrentSettings();

        await Db.DeletePointContent(content.ContentId, StatusContext.ProgressTracker());

        var possibleContentDirectory = settings.LocalSitePointContentDirectory(content, false);
        if (possibleContentDirectory.Exists)
        {
            StatusContext.Progress($"Deleting Generated Folder {possibleContentDirectory.FullName}");
            possibleContentDirectory.Delete(true);
        }
    }

    [NonBlockingCommand]
    [StopAndWarnIfContentIsNull]

    public async Task Edit(PointContentDto? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var context = await Db.Context();

        var refreshedData = context.PointContents.SingleOrDefault(x => x.ContentId == content!.ContentId);

        if (refreshedData == null)
            await StatusContext.ToastError(
                $"{content!.Title} is no longer active in the database? Can not edit - look for a historic version...");

        var newContentWindow = await PointContentEditorWindow.CreateInstance(refreshedData);

        await newContentWindow.PositionWindowAndShowOnUiThread();
    }

    [BlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task ExtractNewLinks(PointContentDto? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var context = await Db.Context();

        var refreshedData = context.PointContents.SingleOrDefault(x => x.ContentId == content!.ContentId);

        if (refreshedData == null) return;

        await LinkExtraction.ExtractNewAndShowLinkContentEditors(
            $"{refreshedData.BodyContent} {refreshedData.UpdateNotes}", StatusContext.ProgressTracker());
    }

    [BlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task GenerateHtml(PointContentDto? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        StatusContext.Progress($"Generating Html for {content!.Title}");

        var fullItem = await Db.PointContentDto(content.ContentId);

        if (fullItem == null)
        {
            await StatusContext.ToastError("Item no longer exists in DB?");
            return;
        }

        var htmlContext = new SinglePointPage(fullItem);

        await htmlContext.WriteLocalHtml();

        await StatusContext.ToastSuccess($"Generated {htmlContext.PageUrl}");
    }

    public StatusControlContext StatusContext { get; set; }

    [NonBlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task ViewHistory(PointContentDto? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var db = await Db.Context();

        StatusContext.Progress($"Looking up Historic Entries for {content!.Title}");

        var historicItems = await db.HistoricPointContents.Where(x => x.ContentId == content.ContentId).ToListAsync();

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
    public async Task ViewOnSite(PointContentDto? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var settings = UserSettingsSingleton.CurrentSettings();

        var url = settings.PointPageUrl(content!);

        var ps = new ProcessStartInfo(url) { UseShellExecute = true, Verb = "open" };
        Process.Start(ps);
    }


    [BlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task ViewSitePreview(PointContentDto? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var settings = UserSettingsSingleton.CurrentSettings();

        var url = settings.PointPageUrl(content!);

        await ThreadSwitcher.ResumeForegroundAsync();

        var sitePreviewWindow = await SiteOnDiskPreviewWindow.CreateInstance(url);

        await sitePreviewWindow.PositionWindowAndShowOnUiThread();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    [BlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task AddIntersectionTagsWithOsm(PointContentDto? content)
    {
        await PointActions.AddIntersectionTags(content!.AsList(), StatusContext, true, CancellationToken.None);
    }

    [BlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task AddIntersectionTagsWithoutOsm(PointContentDto? content)
    {
        await PointActions.AddIntersectionTags(content!.AsList(), StatusContext, false, CancellationToken.None);
    }

    [BlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task CoordinatesToClipboard(PointContentDto? content)
    {
        await PointActions.CoordinateTextToClipboard(content!.AsList(), StatusContext);
    }

    [BlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task ExternalDirectionsBracketCodeToClipboard(PointContentDto? content)
    {
        await PointActions.ExternalDirectionsBracketCodesToClipboard(content!.AsList(), StatusContext);
    }

    [BlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task GoogleMapsBracketCodeToClipboard(PointContentDto? content)
    {
        await PointActions.GoogleMapsBracketCodesToClipboard(content!.AsList(), StatusContext);
    }

    [BlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task ImageBracketCodeToClipboard(PointContentDto? content)
    {
        await PointActions.ImageBracketCodesToClipboard(content!.AsList(), StatusContext);
    }

    public static async Task<PointListListItem> ListItemFromDbItem(PointContent content,
        PointContentActions itemActions,
        bool showType)
    {
        var item = await PointListListItem.CreateInstance(itemActions);
        var dto = await Db.PointContentDtoFromPoint(content, await Db.Context());
        item.DbEntry = dto;
        var (smallImageUrl, displayImageUrl) = ContentListContext.GetContentItemImageUrls(content);
        item.SmallImageUrl = smallImageUrl;
        item.DisplayImageUrl = displayImageUrl;
        item.ShowType = showType;
        return item;
    }

    public static async Task<PointListListItem> ListItemFromDbItem(PointContentDto content,
        PointContentActions itemActions,
        bool showType)
    {
        var item = await PointListListItem.CreateInstance(itemActions);
        item.DbEntry = content;
        var (smallImageUrl, displayImageUrl) = ContentListContext.GetContentItemImageUrls(content);
        item.SmallImageUrl = smallImageUrl;
        item.DisplayImageUrl = displayImageUrl;
        item.ShowType = showType;
        return item;
    }

    [BlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task PointDetailsBracketCodeToClipboard(PointContentDto? content)
    {
        await PointActions.PointDetailsBracketCodesToClipboard(content!.AsList(), StatusContext);
    }

    [BlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task ShowIntersectionTags(PointContentDto? content)
    {
        await PointActions.ShowIntersectionTagsForSelected(content!.AsList(), StatusContext, CancellationToken.None);
    }

    [BlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task ShowOnGoogleMaps(PointContentDto? content)
    {
        await PointActions.ShowInGoogleMapsWeb(content!, StatusContext);
    }

    [NonBlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task ShowOnMap(PointContentDto? content)
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
    public async Task ShowOnOsmCycleMaps(PointContentDto? content)
    {
        await PointActions.ShowInOsmCycleMap(content!, StatusContext);
    }

    [BlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task TextBracketCodeToClipboard(PointContentDto? content)
    {
        await PointActions.TextBracketCodesToClipboard(content!.AsList(), StatusContext);
    }

    [BlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task ToGpxFile(PointContentDto? content)
    {
        await PointActions.ToGpxFile(content!.AsList(), StatusContext);
    }
}