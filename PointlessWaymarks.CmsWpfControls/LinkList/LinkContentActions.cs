using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.EntityFrameworkCore;
using Ookii.Dialogs.Wpf;
using PointlessWaymarks.CmsData;
using PointlessWaymarks.CmsData.ContentHtml.LinkListHtml;
using PointlessWaymarks.CmsData.Database;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.CmsData.Server;
using PointlessWaymarks.CmsWpfControls.ContentHistoryView;
using PointlessWaymarks.CmsWpfControls.ContentList;
using PointlessWaymarks.CmsWpfControls.LinkContentEditor;
using PointlessWaymarks.CmsWpfControls.SitePreview;
using PointlessWaymarks.CmsWpfControls.Utility;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.VisualWebWork;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.Status;
using PointlessWaymarks.WpfCommon.Utility;
using PointlessWaymarks.WpfCommon.WpfHtml;
using Serilog;
using SkiaSharp;

namespace PointlessWaymarks.CmsWpfControls.LinkList;

[NotifyPropertyChanged]
[GenerateStatusCommands]
public partial class LinkContentActions : IContentActions<LinkContent>
{
    public LinkContentActions(StatusControlContext statusContext)
    {
        StatusContext = statusContext;
        BuildCommands();
    }

    public ContentClipboardRepresentation ClipboardObject(LinkContent? content)
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

    public string DefaultBracketCode(LinkContent? content)
    {
        return content?.ContentId == null ? string.Empty : $"[{content.Title}]({content.Url})";
    }

    [BlockingCommand]
    public async Task DefaultBracketCodeToClipboard(LinkContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (content == null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        var finalString = $"[{content.Title}]({content.Url})";

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
    public async Task Delete(LinkContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (content == null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        if (content.Id < 1)
        {
            await StatusContext.ToastError($"Link {content.Title} - Entry is not saved - Skipping?");
            return;
        }

        await Db.DeleteLinkContent(content.ContentId, StatusContext.ProgressTracker());
    }

    [NonBlockingCommand]
    public async Task Edit(LinkContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (content == null) return;

        var context = await Db.Context();

        var refreshedData = context.LinkContents.SingleOrDefault(x => x.ContentId == content.ContentId);

        if (refreshedData == null)
            await StatusContext.ToastError(
                $"{content.Title} is no longer active in the database? Can not edit - look for a historic version...");

        var newContentWindow = await LinkContentEditorWindow.CreateInstance(refreshedData);

        await newContentWindow.PositionWindowAndShowOnUiThread();
    }

    [BlockingCommand]
    public async Task ExtractNewLinks(LinkContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (content == null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        var context = await Db.Context();

        var refreshedData = context.LinkContents.SingleOrDefault(x => x.ContentId == content.ContentId);

        if (refreshedData == null) return;

        await LinkExtraction.ExtractNewAndShowLinkContentEditors(
            $"{refreshedData.Comments} {refreshedData.Description}", StatusContext.ProgressTracker());
    }

    [BlockingCommand]
    public async Task GenerateHtml(LinkContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        StatusContext.Progress("Generating Html for Link List");

        var htmlContext = new LinkListPage();

        await htmlContext.WriteLocalHtmlRssAndJson();

        var settings = UserSettingsSingleton.CurrentSettings();

        await StatusContext.ToastSuccess($"Generated {settings.LinksListUrl()}");
    }

    public StatusControlContext StatusContext { get; set; }

    [NonBlockingCommand]
    public async Task ViewHistory(LinkContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (content == null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        var db = await Db.Context();

        StatusContext.Progress($"Looking up Historic Entries for {content.Title}");

        var historicItems = await db.HistoricLinkContents.Where(x => x.ContentId == content.ContentId).ToListAsync();

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
    public async Task ViewOnSite(LinkContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (content == null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        if (string.IsNullOrWhiteSpace(content.Url))
        {
            await StatusContext.ToastError("URL is Blank?");
            return;
        }

        var url = content.Url;

        var ps = new ProcessStartInfo(url) { UseShellExecute = true, Verb = "open" };
        Process.Start(ps);
    }

    [BlockingCommand]
    public async Task ViewSitePreview(LinkContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (content == null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        var settings = UserSettingsSingleton.CurrentSettings();

        var url = settings.LinksListUrl();

        await ThreadSwitcher.ResumeForegroundAsync();

        var sitePreviewWindow = await SiteOnDiskPreviewWindow.CreateInstance(url);

        await sitePreviewWindow.PositionWindowAndShowOnUiThread();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    [NonBlockingCommand]
    public async Task CopyLinkAsHtml(LinkContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (content == null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        var link =
            $"<a href=\"{WebUtility.HtmlEncode(content.Url ?? string.Empty)}\">{WebUtility.HtmlEncode(content.Title ?? string.Empty)}</a>";

        await ThreadSwitcher.ResumeForegroundAsync();

        if (string.IsNullOrWhiteSpace(link))
        {
            await StatusContext.ToastError("Nothing to Copy?");
            return;
        }

        Clipboard.SetText(link);

        await StatusContext.ToastSuccess($"Md To Clipboard {link}");
    }

    [NonBlockingCommand]
    public async Task CopyLinkAsMarkdown(LinkContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (content == null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        var link = $"[{content.Title}]({content.Url})";

        await ThreadSwitcher.ResumeForegroundAsync();

        if (string.IsNullOrWhiteSpace(link))
        {
            await StatusContext.ToastError("Nothing to Copy?");
            return;
        }

        Clipboard.SetText(link);

        await StatusContext.ToastSuccess($"Md To Clipboard {link}");
    }

    [NonBlockingCommand]
    public async Task CopyUrl(string? link)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        if (string.IsNullOrWhiteSpace(link))
        {
            await StatusContext.ToastError("Nothing to Copy?");
            return;
        }

        Clipboard.SetText(link);

        await StatusContext.ToastSuccess($"To Clipboard {link}");
    }

    [BlockingCommand]
    public async Task DeleteLinkSnapshotImage(object? multiParameter)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        // Extract parameters from the MultiBinding (converter returns an object[] clone)
        LinkSnapshotImageItem? imageItem;
        LinkListListItem? parentListItem;

        if (multiParameter is object[] { Length: >= 2 } arr)
        {
            imageItem = arr[0] as LinkSnapshotImageItem;
            parentListItem = arr[1] as LinkListListItem;
        }
        else if (multiParameter is Array { Length: >= 2 } genericArray)
        {
            var tmp = genericArray.Cast<object>().ToArray();
            imageItem = tmp[0] as LinkSnapshotImageItem;
            parentListItem = tmp[1] as LinkListListItem;
        }
        else
        {
            await StatusContext.ToastError("Invalid parameters for Delete Snapshot Image");
            return;
        }

        if (imageItem?.FileName is null || parentListItem?.DbEntry is null)
        {
            await StatusContext.ToastError("Missing parameters for Delete Snapshot Image");
            return;
        }

        File.Delete(imageItem.FileName);

        DataNotifications.PublishDataNotification("Link Snapshot Image", DataNotificationContentType.Link,
            DataNotificationUpdateType.Update, [parentListItem.DbEntry.ContentId]);
    }

    [BlockingCommand]
    public async Task LinkSnapshotImage(LinkContent? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (content == null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        if (string.IsNullOrWhiteSpace(content.Url))
        {
            await StatusContext.ToastError("URL is blank...");
            return;
        }

        try
        {
            var result = await PlaywrightScreenShot.CaptureScreenshot(content.Url, StatusContext.ProgressTracker());

            if (!result.Success)
            {
                await StatusContext.ShowMessageWithOkButton("Error Save Web Page Image", result.Message);
                return;
            }

            if (result.ImageBytes is null || !result.ImageBytes.Any())
            {
                await StatusContext.ShowMessageWithOkButton("Error Save Web Page Image",
                    $"Image is blank? {result.Message}");
                return;
            }

            var fileName =
                Path.Combine(UserSettingsSingleton.CurrentSettings().LocalMediaArchiveLinkDirectory().FullName,
                    $"{content.ContentId}--{DateTime.Now:yyyy-MM-dd-HHmm}.jpg");

            await File.WriteAllBytesAsync(fileName, result.ImageBytes);

            var ps = new ProcessStartInfo(fileName) { UseShellExecute = true, Verb = "open" };
            Process.Start(ps);

            DataNotifications.PublishDataNotification("Link Snapshot Image", DataNotificationContentType.Link,
                DataNotificationUpdateType.Update, [content.ContentId]);
        }
        catch (Exception e)
        {
            await StatusContext.ShowMessageWithOkButton("Error Save Web Page Image", e.ToString());
        }
    }


    [BlockingCommand]
    public async Task LinkSnapshotImageFromClipboard(LinkContent? content)
    {
        // Clipboard must be accessed on STA / UI thread
        await ThreadSwitcher.ResumeForegroundAsync();

        if (content == null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        if (!Clipboard.ContainsImage())
        {
            await StatusContext.ToastError("Clipboard does not contain an image.");
            return;
        }

        BitmapSource? clipboardImage;
        try
        {
            clipboardImage = Clipboard.GetImage();
        }
        catch (Exception ex)
        {
            await StatusContext.ToastError($"Failed to read image from clipboard: {ex.Message}");
            return;
        }

        if (clipboardImage == null)
        {
            await StatusContext.ToastError("Could not retrieve image from clipboard.");
            return;
        }

        // Encode to JPEG in-memory on UI thread (BitmapSource -> frames)
        byte[] jpegBytes;
        try
        {
            var encoder = new JpegBitmapEncoder { QualityLevel = 100 };
            encoder.Frames.Add(BitmapFrame.Create(clipboardImage));
            await using var mem = new MemoryStream();
            encoder.Save(mem);
            jpegBytes = mem.ToArray();
        }
        catch (Exception ex)
        {
            await StatusContext.ToastError($"Error encoding clipboard image: {ex.Message}");
            return;
        }

        // Persist on background thread
        await ThreadSwitcher.ResumeBackgroundAsync();

        try
        {
            var settings = UserSettingsSingleton.CurrentSettings();
            var targetDir = settings.LocalMediaArchiveLinkDirectory();
            if (!targetDir.Exists) targetDir.Create();

            var destinationPath = Path.Combine(targetDir.FullName,
                $"{content.ContentId}--{DateTime.Now:yyyy-MM-dd-HHmm}.jpg");

            await File.WriteAllBytesAsync(destinationPath, jpegBytes);

            DataNotifications.PublishDataNotification("Link Snapshot Image", DataNotificationContentType.Link,
                DataNotificationUpdateType.Update, [content.ContentId]);

            // Switch to foreground to open file and show UI feedback
            await ThreadSwitcher.ResumeForegroundAsync();

            var ps = new ProcessStartInfo(destinationPath) { UseShellExecute = true, Verb = "open" };
            Process.Start(ps);

            await StatusContext.ToastSuccess($"Clipboard image saved to {destinationPath}");
        }
        catch (Exception ex)
        {
            await StatusContext.ShowMessageWithOkButton("Error Save Clipboard Image", ex.ToString());
        }
    }

    [BlockingCommand]
    public async Task LinkSnapshotImageFromSelectedFile(LinkContent? content)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        if (content == null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        // Let the user pick a single JPG or PDF
        var openDialog = new VistaOpenFileDialog
        {
            Filter = "JPEG and PDF|*.jpg;*.jpeg;*.pdf",
            Multiselect = false
        };

        if (!openDialog.ShowDialog() ?? true) return;

        var selectedFile = openDialog.FileName;
        if (string.IsNullOrWhiteSpace(selectedFile))
        {
            await StatusContext.ToastError("No file selected?");
            return;
        }

        await ThreadSwitcher.ResumeBackgroundAsync();

        try
        {
            var settings = UserSettingsSingleton.CurrentSettings();
            var targetDir = settings.LocalMediaArchiveLinkDirectory();
            if (!targetDir.Exists) targetDir.Create();

            var destinationPath = Path.Combine(
                UserSettingsSingleton.CurrentSettings().LocalMediaArchiveLinkDirectory().FullName,
                $"{content.ContentId}--{DateTime.Now:yyyy-MM-dd-HHmm}.jpg");

            var ext = Path.GetExtension(selectedFile);
            ext = ext.ToLowerInvariant();

            if (ext is ".jpg" or ".jpeg")
            {
                // Copy JPG directly
                File.Copy(selectedFile, destinationPath);
            }
            else if (ext == ".pdf")
            {
                // Convert PDF -> JPEG then copy result
                // Choose reasonable conversion parameters
                var tempJpeg = await ImageHelpers.PdfToJpeg(selectedFile, 2400, 90, SKColors.White);

                if (string.IsNullOrWhiteSpace(tempJpeg) || !File.Exists(tempJpeg))
                {
                    await StatusContext.ShowMessageWithOkButton("Error Add Snapshot Image",
                        "PDF conversion did not produce an image.");
                    return;
                }

                File.Copy(tempJpeg, destinationPath);

                // Try to remove the temp jpeg created by PdfToJpeg if it's not the same as destination
                try
                {
                    if (!string.Equals(Path.GetFullPath(tempJpeg), Path.GetFullPath(destinationPath),
                            StringComparison.OrdinalIgnoreCase)
                        && File.Exists(tempJpeg))
                        File.Delete(tempJpeg);
                }
                catch (Exception ex)
                {
                    Log.ForContext(nameof(tempJpeg), tempJpeg).ForContext(nameof(destinationPath), destinationPath)
                        .Error(ex, "Silent Error cleaning up PdfToJpg image in LinkSnapshotImageFromSelectedFile");
                }
            }
            else
            {
                await StatusContext.ToastError("Unsupported file type. Please select a JPG or PDF.");
                return;
            }

            DataNotifications.PublishDataNotification("Link Snapshot Image", DataNotificationContentType.Link,
                DataNotificationUpdateType.Update, [content.ContentId]);

            await ThreadSwitcher.ResumeForegroundAsync();

            // Open the saved image and notify
            var ps = new ProcessStartInfo(destinationPath) { UseShellExecute = true, Verb = "open" };
            Process.Start(ps);
        }
        catch (Exception e)
        {
            await StatusContext.ShowMessageWithOkButton("Error Add Snapshot Image", e.ToString());
        }
    }

    [NonBlockingCommand]
    public async Task LinkSnapshotImageInteractive(LinkContent? content)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        if (string.IsNullOrWhiteSpace(content?.Url))
        {
            await StatusContext.ToastError("Nothing is selected or URL is blank?");
            return;
        }

        string FileName()
        {
            return Path.Combine(UserSettingsSingleton.CurrentSettings().LocalMediaArchiveLinkDirectory().FullName,
                $"{content.ContentId}--{DateTime.Now:yyyy-MM-dd-HHmm}.jpg");
        }

        var jpegUrlWindow =
            await InteractiveWebViewJpegImageWindow.CreateInstance(await StatusControlContext.CreateInstance(),
                content.Url, FileName);
        jpegUrlWindow.CloseOnSave = true;

        void OnImageSaved(object? sender, InteractiveWebViewJpegImageWindowSavedEventArgs e)
        {
            StatusContext.RunBlockingTask(async () =>
            {
                try
                {
                    var ps = new ProcessStartInfo(e.NewFilename) { UseShellExecute = true, Verb = "open" };
                    Process.Start(ps);

                    DataNotifications.PublishDataNotification("Link Snapshot Image", DataNotificationContentType.Link,
                        DataNotificationUpdateType.Update, [content.ContentId]);
                }
                catch (Exception exception)
                {
                    await StatusContext.ToastError($"Error adding image from URL - {exception.Message}");
                    Debug.WriteLine(exception);
                }
            });
        }

        jpegUrlWindow.ImageSaved += OnImageSaved;

        void OnWindowClosed(object? sender, EventArgs e)
        {
            try
            {
                jpegUrlWindow.ImageSaved -= OnImageSaved;
                jpegUrlWindow.Closed -= OnWindowClosed;
            }
            catch (Exception exception)
            {
                Log.ForContext("LinkContent", content.SafeObjectDump())
                    .Error(exception, "Error creating interactive snapshot for a Link");
            }
        }

        jpegUrlWindow.Closed += OnWindowClosed;

        await jpegUrlWindow.PositionWindowAndShowOnUiThread();
    }

    public static async Task<LinkListListItem> ListItemFromDbItem(LinkContent content, LinkContentActions itemActions,
        bool showType)
    {
        var item = await LinkListListItem.CreateInstance(itemActions);
        item.DbEntry = content;
        item.ShowType = showType;
        return item;
    }

    [BlockingCommand]
    public async Task ViewLinkSnapshotImage(string? filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
        {
            await StatusContext.ToastError("No file?");
            return;
        }

        var ps = new ProcessStartInfo(filename) { UseShellExecute = true, Verb = "open" };
        Process.Start(ps);
    }
}