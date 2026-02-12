using System.ComponentModel;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using PointlessWaymarks.CmsData;
using PointlessWaymarks.CmsData.ContentHtml.ImageHtml;
using PointlessWaymarks.CmsData.Database;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.CmsWpfControls.ContentHistoryView;
using PointlessWaymarks.CmsWpfControls.ContentList;
using PointlessWaymarks.CmsWpfControls.ContentMap;
using PointlessWaymarks.CmsWpfControls.ImageContentEditor;
using PointlessWaymarks.CmsWpfControls.SitePreview;
using PointlessWaymarks.CmsWpfControls.Utility;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.Status;
using PointlessWaymarks.WpfCommon.Utility;

namespace PointlessWaymarks.CmsWpfControls.ImageList;

[NotifyPropertyChanged]
[GenerateStatusCommands]
public partial class ImageContentActions : IContentActions<ImageContent>
{
    public ImageContentActions(StatusControlContext statusContext)
    {
        StatusContext = statusContext;
        BuildCommands();
    }

    public ContentClipboardRepresentation ClipboardObject(ImageContent? content)
    {
        return ContentClipboardRepresentation.ClipboardObject(content);
    }

    public string DefaultBracketCode(ImageContent? content)
    {
        return ImageActions.DefaultBracketCode(content);
    }

    [NonBlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task DefaultBracketCodeToClipboard(ImageContent? content)
    {
        await ImageActions.DefaultBracketCodesToClipboard(content!.AsList(), StatusContext);
    }

    [BlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task Delete(ImageContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (content!.Id < 1)
        {
            await StatusContext.ToastError($"Image {content.Title} - Entry is not saved - Skipping?");
            return;
        }

        var settings = UserSettingsSingleton.CurrentSettings();

        await Db.DeleteImageContent(content.ContentId, StatusContext.ProgressTracker());

        var possibleContentDirectory = settings.LocalSiteImageContentDirectory(content, false);
        if (possibleContentDirectory.Exists)
        {
            StatusContext.Progress($"Deleting Generated Folder {possibleContentDirectory.FullName}");
            possibleContentDirectory.Delete(true);
        }
    }

    [NonBlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task Edit(ImageContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var context = await Db.Context();

        var refreshedData = context.ImageContents.SingleOrDefault(x => x.ContentId == content!.ContentId);

        if (refreshedData == null)
            await StatusContext.ToastError(
                $"{content!.Title} is no longer active in the database? Can not edit - look for a historic version...");

        await ThreadSwitcher.ResumeForegroundAsync();

        var newContentWindow = await ImageContentEditorWindow.CreateInstance(refreshedData);

        newContentWindow.PositionWindowAndShow();

        await ThreadSwitcher.ResumeBackgroundAsync();
    }

    [BlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task ExtractNewLinks(ImageContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var context = await Db.Context();
        var refreshedData = context.ImageContents.SingleOrDefault(x => x.ContentId == content!.ContentId);

        if (refreshedData == null) return;

        await LinkExtraction.ExtractNewAndShowLinkContentEditors(
            $"{refreshedData.BodyContent} {refreshedData.UpdateNotes}", StatusContext.ProgressTracker());
    }

    [BlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task GenerateHtml(ImageContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        StatusContext.Progress($"Generating Html for {content!.Title}");

        var htmlContext = new SingleImagePage(content);

        await htmlContext.WriteLocalHtml();

        await StatusContext.ToastSuccess($"Generated {htmlContext.PageUrl}");
    }

    public StatusControlContext StatusContext { get; set; }

    [NonBlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task ViewHistory(ImageContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var db = await Db.Context();

        StatusContext.Progress($"Looking up Historic Entries for {content!.Title}");

        var historicItems = await db.HistoricImageContents.Where(x => x.ContentId == content.ContentId).ToListAsync();

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
    public async Task ViewOnSite(ImageContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var settings = UserSettingsSingleton.CurrentSettings();

        var url = settings.ImagePageUrl(content!);

        var ps = new ProcessStartInfo(url) { UseShellExecute = true, Verb = "open" };
        Process.Start(ps);
    }

    [BlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task ViewSitePreview(ImageContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var settings = UserSettingsSingleton.CurrentSettings();

        var url = settings.ImagePageUrl(content!);

        await ThreadSwitcher.ResumeForegroundAsync();

        var sitePreviewWindow = await SiteOnDiskPreviewWindow.CreateInstance(url);

        await sitePreviewWindow.PositionWindowAndShowOnUiThread();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    [BlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task AddIntersectionTagsWithOsm(ImageContent? content)
    {
        await ImageActions.AddIntersectionTags(content!.AsList(), StatusContext, true, CancellationToken.None);
    }

    [BlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task AddIntersectionTagsWithoutOsm(ImageContent? content)
    {
        await ImageActions.AddIntersectionTags(content!.AsList(), StatusContext, false, CancellationToken.None);
    }

    [BlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task ExportFile(ImageContent? content)
    {
        await ImageActions.ExportFiles(content!.AsList(), StatusContext, CancellationToken.None);
    }

    public static async Task<ImageListListItem> ListItemFromDbItem(ImageContent content,
        ImageContentActions itemActions,
        bool showType)
    {
        var item = await ImageListListItem.CreateInstance(itemActions);
        item.DbEntry = content;
        var (smallImageUrl, displayImageUrl) = ContentListContext.GetContentItemImageUrls(content);
        item.SmallImageUrl = smallImageUrl;
        item.DisplayImageUrl = displayImageUrl;
        item.ShowType = showType;
        return item;
    }

    [BlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task MetaDataReport(ImageContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        await ImageActions.ReportImageMetadata(content!.AsList(), StatusContext);
    }

    [NonBlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task ShowFileInExplorer(ImageContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (string.IsNullOrWhiteSpace(content!.OriginalFileName))
        {
            await StatusContext.ToastError("No File?");
            return;
        }

        var toOpen = UserSettingsSingleton.CurrentSettings().LocalMediaArchiveImageContentFile(content);

        if (toOpen is not { Exists: true })
        {
            await StatusContext.ToastError("File doesn't exist?");
            return;
        }

        var url = toOpen.FullName;

        await ProcessHelpers.OpenExplorerWindowForFile(url);
    }

    [NonBlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task ShowInPeakFinderWeb(ImageContent? content)
    {
        await ImageActions.ShowInPeakFinderWeb(content!, StatusContext);
    }

    [BlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task ShowIntersectionTags(ImageContent? content)
    {
        await ImageActions.ShowIntersectionTagsForSelected(content!.AsList(), StatusContext, CancellationToken.None);
    }

    [NonBlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task ShowOnGoogleMaps(ImageContent? content)
    {
        await ImageActions.ShowInGoogleMapsWeb(content!, StatusContext);
    }

    [NonBlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task ShowOnMap(ImageContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (content!.Id < 1)
        {
            await StatusContext.ToastError("Entry is not saved - Skipping?");
            return;
        }

        if (content.Latitude == null || content.Longitude == null)
        {
            await StatusContext.ToastError("No Location Data?");
            return;
        }

        await ThreadSwitcher.ResumeForegroundAsync();

        var mapWindow =
            await ContentMapWindow.CreateInstance(new ContentMapListLoader("Mapped Content",
                [content.ContentId]));

        await mapWindow.PositionWindowAndShowOnUiThread();
    }

    [NonBlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task ShowOnOsmCycleMaps(ImageContent? content)
    {
        await ImageActions.ShowInOsmCycleMap(content!, StatusContext);
    }

    [NonBlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task TextBracketCodeToClipboard(ImageContent? content)
    {
        await ImageActions.TextBracketCodesToClipboard(content!.AsList(), StatusContext);
    }

    [NonBlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task ViewFile(ImageContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (string.IsNullOrWhiteSpace(content!.OriginalFileName))
        {
            await StatusContext.ToastError("No Image?");
            return;
        }

        var toOpen = UserSettingsSingleton.CurrentSettings().LocalSiteImageContentFile(content);

        if (toOpen is not { Exists: true })
        {
            await StatusContext.ToastError("Image doesn't exist?");
            return;
        }

        var url = toOpen.FullName;

        var ps = new ProcessStartInfo(url) { UseShellExecute = true, Verb = "open" };
        Process.Start(ps);
    }
}