using System.ComponentModel;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using PointlessWaymarks.CmsData;
using PointlessWaymarks.CmsData.ContentHtml.PostHtml;
using PointlessWaymarks.CmsData.Database;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.CmsWpfControls.ContentHistoryView;
using PointlessWaymarks.CmsWpfControls.ContentList;
using PointlessWaymarks.CmsWpfControls.PostContentEditor;
using PointlessWaymarks.CmsWpfControls.SitePreview;
using PointlessWaymarks.CmsWpfControls.Utility;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.Status;
using PointlessWaymarks.WpfCommon.Utility;

namespace PointlessWaymarks.CmsWpfControls.PostList;

[NotifyPropertyChanged]
[GenerateStatusCommands]
public partial class PostContentActions : IContentActions<PostContent>
{
    public PostContentActions(StatusControlContext statusContext)
    {
        StatusContext = statusContext;
        BuildCommands();
    }

    public ContentClipboardRepresentation ClipboardObject(PostContent? content)
    {
        return ContentClipboardRepresentation.ClipboardObject(content);
    }

    public string DefaultBracketCode(PostContent? content)
    {
        return PostActions.DefaultBracketCode(content);
    }

    [BlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task DefaultBracketCodeToClipboard(PostContent? content)
    {
        await PostActions.DefaultBracketCodesToClipboard(content!.AsList(), StatusContext);
    }

    [NonBlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task Delete(PostContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (content!.Id < 1)
        {
            await StatusContext.ToastError($"Post {content.Title} - Entry is not saved - Skipping?");
            return;
        }

        var settings = UserSettingsSingleton.CurrentSettings();

        await Db.DeletePostContent(content.ContentId, StatusContext.ProgressTracker());

        var possibleContentDirectory = settings.LocalSitePostContentDirectory(content, false);
        if (possibleContentDirectory.Exists)
        {
            StatusContext.Progress($"Deleting Generated Folder {possibleContentDirectory.FullName}");
            possibleContentDirectory.Delete(true);
        }
    }

    [NonBlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task Edit(PostContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var context = await Db.Context();

        var refreshedData = context.PostContents.SingleOrDefault(x => x.ContentId == content!.ContentId);

        if (refreshedData == null)
        {
            await StatusContext.ToastError(
                $"{content!.Title} is no longer active in the database? Can not edit - look for a historic version...");
            return;
        }

        var newContentWindow = await PostContentEditorWindow.CreateInstance(refreshedData);

        await newContentWindow.PositionWindowAndShowOnUiThread();
    }

    [BlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task ExtractNewLinks(PostContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var context = await Db.Context();

        var refreshedData = context.PostContents.SingleOrDefault(x => x.ContentId == content!.ContentId);

        if (refreshedData == null) return;

        await LinkExtraction.ExtractNewAndShowLinkContentEditors(
            $"{refreshedData.BodyContent} {refreshedData.UpdateNotes}", StatusContext.ProgressTracker());
    }

    [BlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task GenerateHtml(PostContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        StatusContext.Progress($"Generating Html for {content!.Title}");

        var htmlContext = new SinglePostPage(content);

        await htmlContext.WriteLocalHtml();

        await StatusContext.ToastSuccess($"Generated {htmlContext.PageUrl}");
    }

    public StatusControlContext StatusContext { get; set; }

    [NonBlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task ViewHistory(PostContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var db = await Db.Context();

        StatusContext.Progress($"Looking up Historic Entries for {content!.Title}");

        var historicItems = await db.HistoricPostContents.Where(x => x.ContentId == content.ContentId).ToListAsync();

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
    public async Task ViewOnSite(PostContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var settings = UserSettingsSingleton.CurrentSettings();

        var url = settings.PostPageUrl(content!);

        var ps = new ProcessStartInfo(url) { UseShellExecute = true, Verb = "open" };
        Process.Start(ps);
    }

    [BlockingCommand]
    [StopAndWarnIfContentIsNull]
    public async Task ViewSitePreview(PostContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var settings = UserSettingsSingleton.CurrentSettings();

        var url = settings.PostPageUrl(content!);

        await ThreadSwitcher.ResumeForegroundAsync();

        var sitePreviewWindow = await SiteOnDiskPreviewWindow.CreateInstance(url);

        await sitePreviewWindow.PositionWindowAndShowOnUiThread();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public static async Task<PostListListItem> ListItemFromDbItem(PostContent content, PostContentActions itemActions,
        bool showType)
    {
        var item = await PostListListItem.CreateInstance(itemActions);
        item.DbEntry = content;
        var (smallImageUrl, displayImageUrl) = ContentListContext.GetContentItemImageUrls(content);
        item.SmallImageUrl = smallImageUrl;
        item.DisplayImageUrl = displayImageUrl;
        item.ShowType = showType;
        return item;
    }
}