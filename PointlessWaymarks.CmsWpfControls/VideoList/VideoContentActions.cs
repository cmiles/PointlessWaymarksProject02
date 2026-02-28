using System.ComponentModel;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using PointlessWaymarks.CmsData;
using PointlessWaymarks.CmsData.ContentHtml.VideoHtml;
using PointlessWaymarks.CmsData.Database;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.CmsWpfControls.ContentHistoryView;
using PointlessWaymarks.CmsWpfControls.ContentList;
using PointlessWaymarks.CmsWpfControls.SitePreview;
using PointlessWaymarks.CmsWpfControls.Utility;
using PointlessWaymarks.CmsWpfControls.VideoContentEditor;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.Status;
using PointlessWaymarks.WpfCommon.Utility;

namespace PointlessWaymarks.CmsWpfControls.VideoList;

[NotifyPropertyChanged]
[GenerateStatusCommands]
public partial class VideoContentActions : IContentActions<VideoContent>
{
    public VideoContentActions(StatusControlContext statusContext)
    {
        StatusContext = statusContext;
        BuildCommands();
    }

    public ContentClipboardRepresentation ClipboardObject(VideoContent? content)
    {
        return ContentClipboardRepresentation.ClipboardObject(content);
    }

    public string DefaultBracketCode(VideoContent? content)
    {
        return VideoActions.DefaultBracketCode(content);
    }

    [BlockingCommand]
    [StopAndWarnIfFirstParameterIsNull]
    public async Task DefaultBracketCodeToClipboard(VideoContent? content)
    {
        await VideoActions.DefaultBracketCodesToClipboard(content!.AsList(), StatusContext);
    }

    [BlockingCommand]
    [StopAndWarnIfFirstParameterIsNull]
    public async Task Delete(VideoContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (content!.Id < 1)
        {
            await StatusContext.ToastError($"Video {content.Title} - Entry is not saved - Skipping?");
            return;
        }

        var settings = UserSettingsSingleton.CurrentSettings();

        await Db.DeleteVideoContent(content.ContentId, StatusContext.ProgressTracker());

        var possibleContentDirectory = settings.LocalSiteVideoContentDirectory(content, false);
        if (possibleContentDirectory.Exists)
        {
            StatusContext.Progress($"Deleting Generated Folder {possibleContentDirectory.FullName}");
            possibleContentDirectory.Delete(true);
        }
    }

    [NonBlockingCommand]
    [StopAndWarnIfFirstParameterIsNull]
    public async Task Edit(VideoContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var context = await Db.Context();

        var refreshedData = context.VideoContents.SingleOrDefault(x => x.ContentId == content!.ContentId);

        if (refreshedData == null)
            await StatusContext.ToastError(
                $"{content!.Title} is no longer active in the database? Can not edit - look for a historic version...");

        var newContentWindow = await VideoContentEditorWindow.CreateInstance(refreshedData);

        await newContentWindow.PositionWindowAndShowOnUiThread();
    }

    [BlockingCommand]
    [StopAndWarnIfFirstParameterIsNull]
    public async Task ExtractNewLinks(VideoContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var context = await Db.Context();
        var refreshedData = context.VideoContents.SingleOrDefault(x => x.ContentId == content!.ContentId);

        if (refreshedData == null) return;

        await LinkExtraction.ExtractNewAndShowLinkContentEditors(
            $"{refreshedData.BodyContent} {refreshedData.UpdateNotes}", StatusContext.ProgressTracker());
    }

    [BlockingCommand]
    [StopAndWarnIfFirstParameterIsNull]
    public async Task GenerateHtml(VideoContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        StatusContext.Progress($"Generating Html for {content!.Title}");

        var htmlContext = new SingleVideoPage(content);

        await htmlContext.WriteLocalHtml();

        await StatusContext.ToastSuccess($"Generated {htmlContext.PageUrl}");
    }

    public StatusControlContext StatusContext { get; set; }

    [NonBlockingCommand]
    [StopAndWarnIfFirstParameterIsNull]
    public async Task ViewHistory(VideoContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var db = await Db.Context();

        StatusContext.Progress($"Looking up Historic Entries for {content!.Title}");

        var historicItems = await db.HistoricVideoContents.Where(x => x.ContentId == content.ContentId).ToListAsync();

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
    [StopAndWarnIfFirstParameterIsNull]
    public async Task ViewOnSite(VideoContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var settings = UserSettingsSingleton.CurrentSettings();

        var url = $"{settings.VideoPageUrl(content!)}";

        var ps = new ProcessStartInfo(url) { UseShellExecute = true, Verb = "open" };
        Process.Start(ps);
    }

    [BlockingCommand]
    [StopAndWarnIfFirstParameterIsNull]
    public async Task ViewSitePreview(VideoContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var settings = UserSettingsSingleton.CurrentSettings();

        var url = settings.VideoPageUrl(content!);

        await ThreadSwitcher.ResumeForegroundAsync();

        var sitePreviewWindow = await SiteOnDiskPreviewWindow.CreateInstance(url);

        await sitePreviewWindow.PositionWindowAndShowOnUiThread();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    [BlockingCommand]
    [StopAndWarnIfFirstParameterIsNull]
    public async Task ExportFile(VideoContent? content)
    {
        await VideoActions.ExportFiles(content!.AsList(), StatusContext, CancellationToken.None);
    }

    [BlockingCommand]
    [StopAndWarnIfFirstParameterIsNull]
    public async Task ImageBracketCodeToClipboard(VideoContent? content)
    {
        await VideoActions.ImageBracketCodesToClipboard(content!.AsList(), StatusContext);
    }

    public static async Task<VideoListListItem> ListItemFromDbItem(VideoContent content,
        VideoContentActions itemActions,
        bool showType)
    {
        var item = await VideoListListItem.CreateInstance(itemActions);
        item.DbEntry = content;
        var (smallImageUrl, displayImageUrl) = ContentListContext.GetContentItemImageUrls(content);
        item.SmallImageUrl = smallImageUrl;
        item.DisplayImageUrl = displayImageUrl;
        item.ShowType = showType;

        return item;
    }

    [NonBlockingCommand]
    [StopAndWarnIfFirstParameterIsNull]
    public async Task MetaDataReport(VideoContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        await VideoActions.ReportVideoMetadata(content!.AsList(), StatusContext);
    }

    [NonBlockingCommand]
    [StopAndWarnIfFirstParameterIsNull]
    public async Task ShowFileInExplorer(VideoContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (string.IsNullOrWhiteSpace(content!.OriginalFileName))
        {
            await StatusContext.ToastError("No File?");
            return;
        }

        var toOpen = UserSettingsSingleton.CurrentSettings().LocalMediaArchiveVideoContentFile(content);

        if (toOpen is not { Exists: true })
        {
            await StatusContext.ToastError("File doesn't exist?");
            return;
        }

        var url = toOpen.FullName;

        await ProcessHelpers.OpenExplorerWindowForFile(url);
    }

    [BlockingCommand]
    [StopAndWarnIfFirstParameterIsNull]
    public async Task TextBracketCodeToClipboard(VideoContent? content)
    {
        await VideoActions.TextBracketCodesToClipboard(content!.AsList(), StatusContext);
    }

    [NonBlockingCommand]
    [StopAndWarnIfFirstParameterIsNull]
    public async Task ViewFile(VideoContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (string.IsNullOrWhiteSpace(content!.OriginalFileName))
        {
            await StatusContext.ToastError("No Video?");
            return;
        }

        var toOpen = UserSettingsSingleton.CurrentSettings().LocalSiteVideoContentFile(content);

        if (toOpen is not { Exists: true })
        {
            await StatusContext.ToastError("Video doesn't exist?");
            return;
        }

        var url = toOpen.FullName;

        var ps = new ProcessStartInfo(url) { UseShellExecute = true, Verb = "open" };
        Process.Start(ps);
    }
}