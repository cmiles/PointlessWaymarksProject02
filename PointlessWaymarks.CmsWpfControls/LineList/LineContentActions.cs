using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using PointlessWaymarks.CmsData;
using PointlessWaymarks.CmsData.BracketCodes;
using PointlessWaymarks.CmsData.ContentHtml.LineHtml;
using PointlessWaymarks.CmsData.Database;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.CmsData.Server;
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
        if (content == null)
            return new ContentClipboardRepresentation();

        var settings = UserSettingsSingleton.CurrentSettings();

        return new ContentClipboardRepresentation
        {
            FormatIdentifier = ContentClipboardRepresentation.ContentClipboardFormat,
            SiteId = settings.SettingsId,
            ContentId = content.ContentId,
            ContentType = Db.ContentTypeDisplayString(content),
            SiteLocalApiUrl = PartialContentPreviewServer.PreviewServerLocalApiUrl
        };
    }

    public string DefaultBracketCode(LineContent content)
    {
        return $"{BracketCodeLines.Create(content)}";
    }

    [BlockingCommand]
    public async Task DefaultBracketCodeToClipboard(LineContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (content == null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        var finalString = $"{BracketCodeLines.Create(content)}{Environment.NewLine}";

        try
        {
            // Get the ContentClipboardRepresentation from ClipboardObject
            var clipboardRepresentation = ClipboardObject(content);

            // Create a DataObject for multiple clipboard formats
            var dataObject = new DataObject();

            // Add the plain text format for compatibility
            dataObject.SetText(finalString);

            // Add the ContentClipboardRepresentation as an alternate format
            // Using the ContentClipboardFormat constant as the format name
            var clipboardJson = JsonSerializer.Serialize(clipboardRepresentation);
            dataObject.SetData(ContentClipboardRepresentation.ContentClipboardFormat, clipboardJson);

            await ThreadSwitcher.ResumeForegroundAsync();

            // Set the clipboard with multiple formats
            Clipboard.SetDataObject(dataObject, true);

            await StatusContext.ToastSuccess($"To Clipboard {finalString}");
        }
        catch (Exception ex)
        {
            // Fallback to simple text if the rich format fails
            await ThreadSwitcher.ResumeForegroundAsync();
            Clipboard.SetText(finalString);
            await StatusContext.ToastWarning($"Simple text copied - rich format failed: {ex.Message}");
        }
    }

    [BlockingCommand]
    public async Task Delete(LineContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (content == null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        if (content.Id < 1)
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
    public async Task Edit(LineContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (content == null) return;

        var context = await Db.Context();

        var refreshedData = context.LineContents.SingleOrDefault(x => x.ContentId == content.ContentId);

        if (refreshedData == null)
            await StatusContext.ToastError(
                $"{content.Title} is no longer active in the database? Can not edit - look for a historic version...");

        var newContentWindow = await LineContentEditorWindow.CreateInstance(refreshedData);

        await newContentWindow.PositionWindowAndShowOnUiThread();
    }

    [BlockingCommand]
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
    public async Task GenerateHtml(LineContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (content == null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        StatusContext.Progress($"Generating Html for {content.Title}");

        var htmlContext = new SingleLinePage(content);

        await htmlContext.WriteLocalHtml();

        await StatusContext.ToastSuccess($"Generated {htmlContext.PageUrl}");
    }

    public StatusControlContext StatusContext { get; set; }

    [NonBlockingCommand]
    public async Task ViewHistory(LineContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (content == null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        var db = await Db.Context();

        StatusContext.Progress($"Looking up Historic Entries for {content.Title}");

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
    public async Task ViewOnSite(LineContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (content == null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        var settings = UserSettingsSingleton.CurrentSettings();

        var url = settings.LinePageUrl(content);

        var ps = new ProcessStartInfo(url) { UseShellExecute = true, Verb = "open" };
        Process.Start(ps);
    }

    [BlockingCommand]
    public async Task ViewSitePreview(LineContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (content == null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        var settings = UserSettingsSingleton.CurrentSettings();

        var url = settings.LinePageUrl(content);

        await ThreadSwitcher.ResumeForegroundAsync();

        var sitePreviewWindow = await SiteOnDiskPreviewWindow.CreateInstance(url);

        await sitePreviewWindow.PositionWindowAndShowOnUiThread();
    }

    [BlockingCommand]
    public async Task LinkBracketCodesToClipboard(LineContent? content)
    {
        if (content == null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        await LineActions.LinkBracketCodesToClipboard(content.AsList(), StatusContext);
    }

    [BlockingCommand]
    public async Task TextStatsBracketCodesToClipboard(LineContent? content)
    {
        if (content == null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        await LineActions.TextStatsBracketCodesToClipboard(content.AsList(), StatusContext);
    }

    [BlockingCommand]
    public async Task StatsBracketCodesToClipboard(LineContent? content)
    {
        if (content == null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        await LineActions.StatsBracketCodesToClipboard(content.AsList(), StatusContext);
    }

    [BlockingCommand]
    public async Task ElevationChartBracketCodesToClipboard(LineContent? content)
    {
        if (content == null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        await LineActions.ElevationChartBracketCodesToClipboard(content.AsList(), StatusContext);
    }

    [BlockingCommand]
    public async Task GeoJsonToClipboard(LineContent? content)
    {
        if (content == null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        await LineActions.GeoJsonToClipboard(content.AsList(), StatusContext);
    }

    [BlockingCommand]
    public async Task SelectedToGpxFile(LineContent? content)
    {
        if (content == null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        await LineActions.ToGpxFile(content.AsList(), StatusContext);
    }

    [BlockingCommand]
    public async Task SelectedToGpxFiles(LineContent? content)
    {
        if (content == null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        await LineActions.ToGpxFiles(content.AsList(), StatusContext);
    }

    [BlockingCommand]
    public async Task ShowIntersectionTags(LineContent? content, CancellationToken cancellationToken)
    {
        if (content == null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        await LineActions.ShowIntersectionTagsForSelected(content.AsList(), StatusContext, cancellationToken);
    }

    [BlockingCommand]
    public async Task AddIntersectionTagsWithOsm(LineContent? content, CancellationToken cancellationToken)
    {
        if (content == null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        await LineActions.AddIntersectionTags(content.AsList(), StatusContext, true, cancellationToken);
    }

    [BlockingCommand]
    public async Task AddIntersectionTagsWithoutOsm(LineContent? content, CancellationToken cancellationToken)
    {
        if (content == null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        await LineActions.AddIntersectionTags(content.AsList(), StatusContext, false, cancellationToken);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

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
    public async Task SearchRecordedOnDaysForPhotoContent(LineContent? lineContent)
    {
        if (lineContent == null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        if (lineContent.RecordingStartedOnUtc is null || lineContent.RecordingEndedOnUtc is null)
        {
            await StatusContext.ToastError(
                "Line doesn't have Recorded On dates to work with? - Can not search for Photo Content");
            return;
        }


        var dateSearchStart = lineContent.RecordingStartedOnUtc.Value.ToLocalTime().Date.ToUniversalTime();
        var dateSearchEnd = lineContent.RecordingEndedOnUtc.Value.ToLocalTime().Date.AddDays(1).ToUniversalTime();

        await PhotoContentActions.RunReport(async () => await SearchRecordedOnDaysForPhotoContentFilter(lineContent),
            $"Line {lineContent.Title ?? string.Empty} - {dateSearchStart.ToLocalTime():M/d/yyyy} to {dateSearchEnd.ToLocalTime():M/d/yyyy}");
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
    public async Task SearchRecordedOnForPhotoContent(LineContent? lineContent)
    {
        if (lineContent == null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        if (lineContent.RecordingStartedOnUtc is null || lineContent.RecordingEndedOnUtc is null)
        {
            await StatusContext.ToastError(
                "Line doesn't have Recorded On dates to work with? - Can not search for Photo Content");
            return;
        }

        await PhotoContentActions.RunReport(async () => await SearchRecordedOnForPhotoContentFilter(lineContent),
            $"Line {lineContent.Title ?? string.Empty} - {lineContent.RecordingStartedOnUtc.Value.AddMinutes(-5):M/d/yyyy hh:mm:ss tt} to {lineContent.RecordingEndedOnUtc.Value.AddMinutes(5):M/d/yyyy hh:mm:ss tt}");
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


    [NonBlockingCommand]
    public async Task ShowOnMap(LineContent? content)
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
}