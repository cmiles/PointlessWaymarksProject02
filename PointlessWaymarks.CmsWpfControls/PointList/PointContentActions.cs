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
    public async Task DefaultBracketCodeToClipboard(PointContentDto? content)
    {
        if (content is null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        await PointActions.DefaultBracketCodesToClipboard(content.AsList(), StatusContext);
    }

    [BlockingCommand]
    public async Task Delete(PointContentDto? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (content == null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        if (content.Id < 1)
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
    public async Task Edit(PointContentDto? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (content == null) return;

        var context = await Db.Context();

        var refreshedData = context.PointContents.SingleOrDefault(x => x.ContentId == content.ContentId);

        if (refreshedData == null)
            await StatusContext.ToastError(
                $"{content.Title} is no longer active in the database? Can not edit - look for a historic version...");

        var newContentWindow = await PointContentEditorWindow.CreateInstance(refreshedData);

        await newContentWindow.PositionWindowAndShowOnUiThread();
    }

    [BlockingCommand]
    public async Task ExtractNewLinks(PointContentDto? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (content == null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        var context = await Db.Context();

        var refreshedData = context.PointContents.SingleOrDefault(x => x.ContentId == content.ContentId);

        if (refreshedData == null) return;

        await LinkExtraction.ExtractNewAndShowLinkContentEditors(
            $"{refreshedData.BodyContent} {refreshedData.UpdateNotes}", StatusContext.ProgressTracker());
    }

    [BlockingCommand]
    public async Task GenerateHtml(PointContentDto? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (content == null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        StatusContext.Progress($"Generating Html for {content.Title}");

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
    public async Task ViewHistory(PointContentDto? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (content == null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        var db = await Db.Context();

        StatusContext.Progress($"Looking up Historic Entries for {content.Title}");

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
    public async Task ViewOnSite(PointContentDto? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (content == null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        var settings = UserSettingsSingleton.CurrentSettings();

        var url = settings.PointPageUrl(content);

        var ps = new ProcessStartInfo(url) { UseShellExecute = true, Verb = "open" };
        Process.Start(ps);
    }


    [BlockingCommand]
    public async Task ViewSitePreview(PointContentDto? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (content == null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        var settings = UserSettingsSingleton.CurrentSettings();

        var url = settings.PointPageUrl(content);

        await ThreadSwitcher.ResumeForegroundAsync();

        var sitePreviewWindow = await SiteOnDiskPreviewWindow.CreateInstance(url);

        await sitePreviewWindow.PositionWindowAndShowOnUiThread();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    [BlockingCommand]
    public async Task AddIntersectionTagsWithOsm(PointContentDto? content)
    {
        if (content is null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        await PointActions.AddIntersectionTags(content.AsList(), StatusContext, true, CancellationToken.None);
    }

    [BlockingCommand]
    public async Task AddIntersectionTagsWithoutOsm(PointContentDto? content)
    {
        if (content is null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        await PointActions.AddIntersectionTags(content.AsList(), StatusContext, false, CancellationToken.None);
    }

    [BlockingCommand]
    public async Task CoordinatesToClipboard(PointContentDto? content)
    {
        if (content is null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        await PointActions.CoordinateTextToClipboard(content.AsList(), StatusContext);
    }

    [BlockingCommand]
    public async Task ExternalDirectionsBracketCodeToClipboard(PointContentDto? content)
    {
        if (content is null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        await PointActions.ExternalDirectionsBracketCodesToClipboard(content.AsList(), StatusContext);
    }

    [BlockingCommand]
    public async Task GoogleMapsBracketCodeToClipboard(PointContentDto? content)
    {
        if (content is null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        await PointActions.GoogleMapsBracketCodesToClipboard(content.AsList(), StatusContext);
    }

    [BlockingCommand]
    public async Task ImageBracketCodeToClipboard(PointContentDto? content)
    {
        if (content is null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        await PointActions.ImageBracketCodesToClipboard(content.AsList(), StatusContext);
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
    public async Task PointDetailsBracketCodeToClipboard(PointContentDto? content)
    {
        if (content is null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        await PointActions.PointDetailsBracketCodesToClipboard(content.AsList(), StatusContext);
    }

    [BlockingCommand]
    public async Task ShowIntersectionTags(PointContentDto? content)
    {
        if (content is null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        await PointActions.ShowIntersectionTagsForSelected(content.AsList(), StatusContext, CancellationToken.None);
    }

    [BlockingCommand]
    public async Task ShowOnGoogleMaps(PointContentDto? content)
    {
        if (content is null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        await PointActions.ShowInGoogleMapsWeb(content, StatusContext);
    }

    [NonBlockingCommand]
    public async Task ShowOnMap(PointContentDto? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (content == null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        if (content.Id < 1)
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
    public async Task ShowOnOsmCycleMaps(PointContentDto? content)
    {
        if (content is null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        await PointActions.ShowInOsmCycleMap(content, StatusContext);
    }

    [BlockingCommand]
    public async Task TextBracketCodeToClipboard(PointContentDto? content)
    {
        if (content is null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        await PointActions.TextBracketCodesToClipboard(content.AsList(), StatusContext);
    }

    [BlockingCommand]
    public async Task ToGpxFile(PointContentDto? content)
    {
        if (content is null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        await PointActions.ToGpxFile(content.AsList(), StatusContext);
    }
}