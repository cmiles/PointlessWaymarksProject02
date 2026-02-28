using System.Diagnostics;
using System.IO;
using System.Text;
using System.Web;
using Microsoft.EntityFrameworkCore;
using Ookii.Dialogs.Wpf;
using PointlessWaymarks.CmsData;
using PointlessWaymarks.CmsData.ContentGeneration;
using PointlessWaymarks.CmsData.ContentHtml.VideoHtml;
using PointlessWaymarks.CmsData.Database;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.CmsWpfControls.ContentList;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.FileMetadataDisplay;
using PointlessWaymarks.WpfCommon.Status;
using PointlessWaymarks.WpfCommon.Utility;
using PointlessWaymarks.WpfCommon.WpfHtml;

namespace PointlessWaymarks.CmsWpfControls.VideoList;

[NotifyPropertyChanged]
[GenerateStatusCommands]
public partial class VideoListWithActionsContext
{
    private VideoListWithActionsContext(StatusControlContext statusContext, WindowIconStatus? windowStatus,
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
                ItemName = "Embed Code to Clipboard",
                ItemCommand = ListContext.BracketCodeToClipboardSelectedCommand
            },

            new ContextMenuItemData
            {
                ItemName = "Image Code to Clipboard",
                ItemCommand = ImageBracketCodesToClipboardForSelectedCommand
            },

            new ContextMenuItemData
            {
                ItemName = "Text Code to Clipboard",
                ItemCommand = TextBracketCodesToClipboardForSelectedCommand
            },

            new ContextMenuItemData
            {
                ItemName = "Picture Gallery to Clipboard",
                ItemCommand = ListContext.PictureGalleryBracketCodeToClipboardSelectedCommand
            },
            new ContextMenuItemData { ItemName = "View Videos - Individual", ItemCommand = ViewSelectedVideosCommand },
            new ContextMenuItemData
            {
                ItemName = "View Videos - Group", ItemCommand = ListContext.PicturesAndVideosViewWindowSelectedCommand
            },
            new ContextMenuItemData { ItemName = "Export Files", ItemCommand = ExportFilesCommand },
            new ContextMenuItemData { ItemName = "Open URL", ItemCommand = ListContext.ViewOnSiteCommand },
            new ContextMenuItemData { ItemName = "Delete", ItemCommand = ListContext.DeleteSelectedCommand },
            new ContextMenuItemData
            {
                ItemName = "Map Selected Items", ItemCommand = ListContext.SpatialItemsToContentMapWindowSelectedCommand
            },

            new ContextMenuItemData { ItemName = "Refresh Data", ItemCommand = RefreshDataCommand }
        ];

        if (loadInBackground) StatusContext.RunFireAndForgetBlockingTask(RefreshData);
    }

    public CmsCommonCommands CommonCommands { get; set; }
    public ContentListContext ListContext { get; set; }
    public StatusControlContext StatusContext { get; set; }
    public WindowIconStatus? WindowStatus { get; set; }

    public static async Task<VideoListWithActionsContext> CreateInstance(StatusControlContext? statusContext,
        WindowIconStatus? windowStatus, IContentListLoader? listLoader, bool loadInBackground = true)
    {
        var factoryStatusContext = await StatusControlContext.CreateInstance(statusContext);

        await ThreadSwitcher.ResumeBackgroundAsync();

        var factoryListContext =
            await ContentListContext.CreateInstance(factoryStatusContext, listLoader ?? new VideoListLoader(100),
                [Db.ContentTypeDisplayStringForVideo], windowStatus);

        return new VideoListWithActionsContext(factoryStatusContext, windowStatus, factoryListContext,
            loadInBackground);
    }

    [BlockingCommand]
    [StopAndWarnIfNotOneSelectedListItems]
    public async Task EmailHtmlToClipboard()
    {
        var frozenSelected = SelectedListItems().First();

        var emailHtml = await Email.ToHtmlEmail(frozenSelected.DbEntry, StatusContext.ProgressTracker());

        await ThreadSwitcher.ResumeForegroundAsync();

        HtmlClipboardHelpers.CopyToClipboard(emailHtml, emailHtml);

        await StatusContext.ToastSuccess("Email Html on Clipboard");
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItemsAskIfOverMax(MaxSelectedItems = 10)]
    public async Task ExportFiles(CancellationToken cancellationToken)
    {
        await VideoActions.ExportFiles(SelectedListItemsContent(), StatusContext, cancellationToken);
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task ImageBracketCodesToClipboardForSelected()
    {
        await VideoActions.ImageBracketCodesToClipboard(SelectedListItemsContent(), StatusContext);
    }

    [BlockingCommand]
    private async Task RefreshData()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        await ListContext.LoadData();
    }

    [NonBlockingCommand]
    public async Task ReportTitleAndFileNameDoNotMatch()
    {
        await RunReport(ReportTitleAndFileNameDoNotMatchGenerator, "Title and Filename Don't Match");
    }

    private async Task<List<object>> ReportTitleAndFileNameDoNotMatchGenerator()
    {
        var db = await Db.Context();

        var allContents = await db.VideoContents.OrderByDescending(x => x.VideoCreatedOn).ToListAsync();

        var returnList = new List<VideoContent>();

        foreach (var loopContents in allContents)
        {
            var titleFilename = SlugTagTools.CreateSlug(false, loopContents.Title.TrimNullToEmpty());

            if (string.IsNullOrWhiteSpace(titleFilename)) returnList.Add(loopContents);

            if (string.Equals(titleFilename, Path.GetFileNameWithoutExtension(loopContents.OriginalFileName))) continue;

            returnList.Add(loopContents);
        }

        return returnList.Cast<object>().ToList();
    }

    [NonBlockingCommand]
    public async Task ReportVideoEncodingIssue()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var possibleProbe = UserSettingsSingleton.CurrentSettings().FfprobeExe();

        if (string.IsNullOrWhiteSpace(possibleProbe))
        {
            await StatusContext.ToastError(
                "ffprobe.exe not set or not found - this report depends on ffprobe, see the site settings to set the location of ffprobe");
            return;
        }

        await RunReport(ReportVideoEncodingIssueGenerator, "Video Encoding Issue");
    }

    private async Task<List<object>> ReportVideoEncodingIssueGenerator()
    {
        var db = await Db.Context();

        var allContents = await db.VideoContents.OrderByDescending(x => x.VideoCreatedOn).ToListAsync();

        var ffprobe = UserSettingsSingleton.CurrentSettings().FfprobeExe();

        var returnList = new List<VideoContent>();

        var missingMediaArchiveFiles = new List<VideoContent>();
        var ffprobeProblems = new List<(VideoContent content, string errorMessage)>();

        foreach (var loopContents in allContents)
        {
            var mediaLibraryFile =
                UserSettingsSingleton.CurrentSettings().LocalMediaArchiveVideoContentFile(loopContents);

            if (mediaLibraryFile is not { Exists: true })
            {
                missingMediaArchiveFiles.Add(loopContents);
                continue;
            }

            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = ffprobe,
                    Arguments =
                        $"-v error -select_streams v:0 -show_entries stream=codec_name -of default=noprint_wrappers=1:nokey=1 \"{mediaLibraryFile.FullName}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process();
                process.StartInfo = processStartInfo;
                process.Start();

                var codecOutput = await process.StandardOutput.ReadToEndAsync();
                var errorOutput = await process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    ffprobeProblems.Add((loopContents,
                        $"FFprobe failed with exit code {process.ExitCode}: {errorOutput}"));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(codecOutput))
                {
                    ffprobeProblems.Add((loopContents, "FFprobe returned no codec information"));
                    continue;
                }

                var codecName = codecOutput.Trim().ToLowerInvariant();

                // HTML5 video tag friendly codecs
                var htmlFriendlyCodecs = new[] { "h264", "vp8", "vp9", "av1" };

                // If the codec is not HTML5 friendly, add to return list
                if (!htmlFriendlyCodecs.Contains(codecName)) returnList.Add(loopContents);
            }
            catch (Exception ex)
            {
                // Add exception to problems list
                ffprobeProblems.Add((loopContents, $"Exception while checking video: {ex.Message}"));
            }
        }

        // If there are any issues, show a report before returning the main results
        if (missingMediaArchiveFiles.Any() || ffprobeProblems.Any())
        {
            var reportBuilder = new StringBuilder();

            if (missingMediaArchiveFiles.Any())
            {
                reportBuilder.AppendLine("<h2>Missing Media Archive Files</h2>");
                reportBuilder.AppendLine(
                    $"<p>Found {missingMediaArchiveFiles.Count} video(s) with missing media archive files:</p>");
                reportBuilder.AppendLine("<table class='pure-table pure-table-striped'>");
                reportBuilder.AppendLine(
                    "<thead><tr><th>Title</th><th>Original Filename</th><th>Content ID</th><th>Created On</th></tr></thead>");
                reportBuilder.AppendLine("<tbody>");

                foreach (var missing in missingMediaArchiveFiles)
                {
                    reportBuilder.AppendLine("<tr>");
                    reportBuilder.AppendLine($"<td>{HttpUtility.HtmlEncode(missing.Title)}</td>");
                    reportBuilder.AppendLine($"<td>{HttpUtility.HtmlEncode(missing.OriginalFileName ?? "N/A")}</td>");
                    reportBuilder.AppendLine($"<td>{missing.ContentId}</td>");
                    reportBuilder.AppendLine($"<td>{missing.VideoCreatedOn:yyyy-MM-dd}</td>");
                    reportBuilder.AppendLine("</tr>");
                }

                reportBuilder.AppendLine("</tbody></table>");
            }

            if (ffprobeProblems.Any())
            {
                reportBuilder.AppendLine("<br><h2>FFprobe Check Problems</h2>");
                reportBuilder.AppendLine($"<p>Found {ffprobeProblems.Count} video(s) with ffprobe issues:</p>");
                reportBuilder.AppendLine("<table class='pure-table pure-table-striped'>");
                reportBuilder.AppendLine(
                    "<thead><tr><th>Title</th><th>Original Filename</th><th>Content ID</th><th>Error Message</th></tr></thead>");
                reportBuilder.AppendLine("<tbody>");

                foreach (var (content, errorMessage) in ffprobeProblems)
                {
                    reportBuilder.AppendLine("<tr>");
                    reportBuilder.AppendLine($"<td>{HttpUtility.HtmlEncode(content.Title)}</td>");
                    reportBuilder.AppendLine($"<td>{HttpUtility.HtmlEncode(content.OriginalFileName ?? "N/A")}</td>");
                    reportBuilder.AppendLine($"<td>{content.ContentId}</td>");
                    reportBuilder.AppendLine($"<td>{HttpUtility.HtmlEncode(errorMessage)}</td>");
                    reportBuilder.AppendLine("</tr>");
                }

                reportBuilder.AppendLine("</tbody></table>");
            }

            await ThreadSwitcher.ResumeForegroundAsync();

            var reportWindow = await WebViewWindow.CreateInstance();
            await reportWindow.PositionWindowAndShowOnUiThread();

            await reportWindow.SetupDocumentWithPureCss(reportBuilder.ToString(),
                "Video Encoding Issue Report - Problems Found");
        }

        return returnList.Cast<object>().ToList();
    }

    [BlockingCommand]
    [StopAndWarnIfNoOrMoreThanSelectedListItems(MaxSelectedItems = 10)]
    public async Task ReportVideoMetadata()
    {
        await VideoActions.ReportVideoMetadata(SelectedListItemsContent(), StatusContext);
    }

    private static async Task RunReport(Func<Task<List<object>>> toRun, string title)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var reportLoader = new ContentListLoaderReport(toRun);

        var newWindow =
            await VideoListWindow.CreateInstance(
                await CreateInstance(null, null, reportLoader));
        newWindow.WindowTitle = title;
        await newWindow.PositionWindowAndShowOnUiThread();
    }

    public List<VideoListListItem> SelectedListItems()
    {
        return ListContext.ListSelection.SelectedItems.Where(x => x is VideoListListItem).Cast<VideoListListItem>()
            .ToList();
    }

    public List<VideoContent> SelectedListItemsContent()
    {
        return ListContext.ListSelection.SelectedItems.Where(x => x is VideoListListItem).Cast<VideoListListItem>()
            .Select(x => x.DbEntry).ToList();
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task TextBracketCodesToClipboardForSelected()
    {
        await VideoActions.TextBracketCodesToClipboard(SelectedListItemsContent(), StatusContext);
    }

    [BlockingCommand]
    public async Task VideoMetadataFromPickedFile()
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        var dialog = new VistaOpenFileDialog { Multiselect = false, Filter = "All files (*.*)|*.*" };

        if (!(dialog.ShowDialog() ?? false)) return;

        var selectedFile = dialog.FileName;

        if (string.IsNullOrWhiteSpace(selectedFile)) return;
        if (!File.Exists(selectedFile)) return;
        var file = new FileInfo(selectedFile);

        var metadataWindow =
            await FileMetadataDisplayWindow.CreateInstance(file.FullName,
                UserSettingsSingleton.CurrentSettings().FfprobeExe());
        await metadataWindow.PositionWindowAndShowOnUiThread();
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task VideoTitleToFilename()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var frozenSelected = SelectedListItems();

        var settings = UserSettingsSingleton.CurrentSettings();

        var errors = new List<string>();
        var successCounter = 0;
        var skipCounter = 0;

        foreach (var loopVideo in frozenSelected)
        {
            if (string.IsNullOrWhiteSpace(loopVideo.DbEntry.Title))
            {
                skipCounter++;
                continue;
            }

            try
            {
                var selectedFile = settings.LocalMediaArchiveVideoContentFile(loopVideo.DbEntry);

                if (selectedFile is null)
                {
                    errors.Add($"{loopVideo.DbEntry.Title} - No file found?");
                    continue;
                }

                if (selectedFile is not { Exists: true })
                {
                    errors.Add($"{loopVideo.DbEntry.Title} - file {selectedFile.FullName} does not exist?");
                    continue;
                }

                var cleanedName = SlugTagTools.CreateSlug(false, loopVideo.DbEntry.Title.TrimNullToEmpty());

                if (string.IsNullOrWhiteSpace(cleanedName))
                {
                    errors.Add($"{loopVideo.DbEntry.Title} - Can't rename the file to an empty string...");
                    continue;
                }

                if (!FileAndFolderTools.IsNoUrlEncodingNeeded(cleanedName))
                {
                    errors.Add(
                        $"{loopVideo.DbEntry.Title} - {cleanedName} - File Names must be limited to A - Z a - z 0 - 9 - . _");
                    continue;
                }

                if (string.Equals(loopVideo.DbEntry.OriginalFileName,
                        $"{cleanedName}{Path.GetExtension(selectedFile.Name)}"))
                {
                    skipCounter++;
                    continue;
                }

                var moveToName = Path.Combine(selectedFile.Directory?.FullName ?? string.Empty,
                    $"{cleanedName}{Path.GetExtension(selectedFile.Name)}");

                // Check if a different file (not just case difference) already exists
                if (File.Exists(moveToName) &&
                    !string.Equals(selectedFile.FullName, moveToName, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"{loopVideo.DbEntry.Title} - {moveToName} - Suggested new Filename Already Exists");
                    continue;
                }

                try
                {
                    // For case-only renames, we need to do a two-step rename on case-insensitive filesystems
                    if (string.Equals(selectedFile.FullName, moveToName, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(selectedFile.FullName, moveToName, StringComparison.Ordinal))
                    {
                        // Case-only rename: use temporary file
                        var tempName = Path.Combine(selectedFile.Directory?.FullName ?? string.Empty,
                            $"{Guid.NewGuid()}{Path.GetExtension(selectedFile.Name)}");
                        File.Move(selectedFile.FullName, tempName);
                        File.Move(tempName, moveToName);
                    }
                    else
                    {
                        // Normal copy
                        File.Copy(selectedFile.FullName, moveToName);
                    }
                }
                catch (Exception e)
                {
                    errors.Add($"{loopVideo.DbEntry.Title} - {moveToName} - {e.Message}");
                    continue;
                }

                var finalFile = new FileInfo(moveToName);
                loopVideo.DbEntry.OriginalFileName = finalFile.Name;

                if (string.IsNullOrWhiteSpace(loopVideo.DbEntry.LastUpdatedBy))
                    loopVideo.DbEntry.LastUpdatedBy = loopVideo.DbEntry.CreatedBy;
                if (loopVideo.DbEntry.LastUpdatedOn is null || loopVideo.DbEntry.LastUpdatedOn == DateTime.MinValue)
                    loopVideo.DbEntry.LastUpdatedOn = DateTime.Now;

                var saveResult = await VideoGenerator.SaveAndGenerateHtml(loopVideo.DbEntry, finalFile, null,
                    StatusContext.ProgressTracker());

                if (saveResult.generationReturn.HasError)
                {
                    errors.Add(
                        $"{loopVideo.DbEntry.Title} - {moveToName} - {saveResult.generationReturn.ToErrorString()}");
                    continue;
                }

                successCounter++;
            }
            catch (Exception e)
            {
                errors.Add($"{loopVideo.DbEntry.Title} - {e.Message}");
            }
        }

        if (errors.Any())
            await StatusContext.ShowMessageWithOkButton("Errors Renaming",
                $"{successCounter} Succeeded, {skipCounter} Already Equal, {errors.Count} Failed: {Environment.NewLine}{Environment.NewLine}{string.Join($"{Environment.NewLine}{Environment.NewLine}", errors)}");
        else
            await StatusContext.ToastSuccess($"Renamed {successCounter} files, {skipCounter} Names already match.");
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItemsAskIfOverMax(MaxSelectedItems = 10)]
    public async Task ViewSelectedVideos(CancellationToken cancelToken)
    {
        var currentSelected = SelectedListItems();

        foreach (var loopSelected in currentSelected)
        {
            cancelToken.ThrowIfCancellationRequested();

            await loopSelected.ItemActions.ViewFile(loopSelected.DbEntry);
        }
    }
}