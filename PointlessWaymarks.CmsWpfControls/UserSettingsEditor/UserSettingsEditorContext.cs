using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using Amazon;
using Omu.ValueInjecter;
using Ookii.Dialogs.Wpf;
using PointlessWaymarks.CmsData;
using PointlessWaymarks.CmsData.S3;
using PointlessWaymarks.CmsData.Spatial;
using PointlessWaymarks.CmsWpfControls.ContentList;
using PointlessWaymarks.CmsWpfControls.SitePictureSizesEditor;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.CommonTools.S3;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.GeoNamesControl;
using PointlessWaymarks.WpfCommon.Status;
using PointlessWaymarks.WpfCommon.Utility;

namespace PointlessWaymarks.CmsWpfControls.UserSettingsEditor;

[NotifyPropertyChanged]
[GenerateStatusCommands]
public partial class UserSettingsEditorContext
{
    private UserSettingsEditorContext(StatusControlContext statusContext, UserSettings toLoad)
    {
        StatusContext = statusContext;
        CommonCommands = new CmsCommonCommands(StatusContext);

        BuildCommands();

        CloudProviderChoices = new List<string> { string.Empty }.Concat(Enum.GetNames(typeof(S3Providers))).ToList();
        RegionChoices = RegionEndpoint.EnumerableAllRegions.Select(x => x.SystemName).ToList();
        EditorSettings = toLoad;
    }

    public List<string> CloudProviderChoices { get; set; }
    public CmsCommonCommands CommonCommands { get; set; }
    public UserSettings EditorSettings { get; set; }

    public static string HelpMarkdownCalTopoMapsApiKey =>
        "If you have a CalTopo Maps API key you can enter it here - this will allow access to some CalTopo layers in the maps. This is NOT required for maps to be functional.";

    public static string HelpMarkdownDefaultCreatedByName =>
        "Set this to fill in a Default Created By when creating new content. Example 'Charles Miles'.";

    public static string HelpMarkdownDefaultLatitudeLongitude =>
        "The default Latitude and Longitude (in dd.dddd format) are used as the default starting point for maps. Example Latitude '32.4432', Longitude '-110.7577'.";

    public static string HelpMarkdownDomain =>
        "This is the subdomain + domain and optionally port - for example 'PointlessWaymarks.com'. This software will " +
        "prepend protocol and append paths to this.";

    public static string HelpMarkdownFeatureIntersectionSettingsFile =>
        "This program can check a Point or Line against a set of GeoJson files to generate tags. The settings file for that feature must be specified here.";

    public static string HelpMarkdownFeatureIntersectionTagOnImport =>
        "If checked - and the Feature Intersection Settings File is set/valid - newly imported content that has position information will have feature intersect tags added.";

    public static string HelpMarkdownFilesHavePublicDownloadLinkByDefault =>
        "Default setting for whether File Content has a download link. All Content is ALWAYS sent to the site!!! Controls like this only determine if there is an obvious link to the content - private content should not be added to this program.";

    public static string HelpMarkdownFooterSnippet =>
        "This is a snippet that will be included in your footer - in theory it could be anything but the real intent is for analytics/tracking js.";

    public static string HelpMarkdownGeoJsonHasPublicDownloadLinkByDefault =>
        "Default setting for whether GeoJson Content has a download link. All Content is ALWAYS sent to the site!!! Controls like this only determine if there is an obvious link to the content - private content should not be added to this program.";

    public static string HelpMarkdownGeoNamesInformation =>
        "[GeoNames](https://www.geonames.org/) offers an [API](https://www.geonames.org/export/web-services.html) that this program can use to search for geographic locations - this is completely optional! In order to use the API you must have a User Name with GeoNames, you must enable web API access (the no cost API access has some limits, be sure to read the GeoNames site for details) and you need to enter that User Name here. User Names are stored securely by Windows - these are NOT stored in the database or in the settings file, but be aware that anyone with access to your Windows Account has access to these credentials!";

    public static string HelpMarkdownLinesHavePublicDownloadLinkByDefault =>
        "Default setting for whether Line Content has a download link. All Content is ALWAYS sent to the site!!! Controls like this only determine if there is an obvious link to the content - private content should not be added to this program.";

    public static string HelpMarkdownLinesShowContentReferencesOnMapByDefault =>
        "Default setting for whether spatial content referenced in the Line Body are shown on the map by default.";

    public static string HelpMarkdownLocalMediaArchive =>
        "The original/source media files are stored separately from the generated site - this (local) directory is very " +
        "important because the generating the site depends on the settings file, database and the contents of this " +
        "directory. Ideally you should backup this directory.";

    public static string HelpMarkdownLocalSiteRootDirectory =>
        "This is the directory where the local generated site will be placed - this should be a local directory, the " +
        "intention is that this program will create a local generated site to this directory and provide tools " +
        "to help you sync that to a server if you want to publish a public version of the site.";

    public static string HelpMarkdownNumberOfItemsOnTheMainPage =>
        "Determines the maximum number of items that will be displayed on the main/home/index page of the site.";

    public static string HelpMarkdownPinboardApiKey =>
        "Sites, pages and links on the internet are constantly disappearing - [Pinboard](https://pinboard.in/) is a bookmarking site that has options to archive links for your personal use and this software has some functions that help you send links to Pinboard if you enter your Api Key. This is OPTIONAL - nothing in this software requires Pinboard.";

    public static string HelpMarkdownProgramUpdateLocation =>
        "The location the program should check for updates.";

    public static string HelpMarkdownS3Information =>
        "This is NOT required. Cloud S3 Storage from Amazon or Cloudflare - especially combined with Cloudflare for caching - can be an good way to host a static site like this program generates. This program can help you upload files and maintain files on S3, but to do so you must provide some information - S3 Bucket Name (this will often match your domain name), S3 Bucket Region and Site Credentials (these are not shown and are stored securely by Windows - these are NOT stored in the database or in the settings, file but be aware that anyone with access to your Windows Account has access to these credentials!).";

    public static string HelpMarkdownShowImageSizesByDefault =>
        "Used as the default value for Photos and Images 'Show Sizes' setting - if this is checked by default image pages will have links to every size available. ALL IMAGE FILES are 'public', but unless this is checked the user is never shown a direct link to any image file.";

    public static string HelpMarkdownShowInMainSiteFeedDefaults =>
        "Use these settings for the Show In Main Site Feed default value.";

    public static string HelpMarkdownShowPhotoPositionByDefault =>
        "Used as the default value for a Photo's 'Show Position' setting - if this is checked by default photo pages will show and link the position of a photo if the photo's latitude and longitude have values. ALL PHOTO FILES are 'public' so a determined user can examine the source of a page, download the image and extract metadata present in the photo, but unless 'Show Position' is checked a photographs position will never be displayed.";

    public static string HelpMarkdownShowPreviousNextContent =>
        "By default pages in the main feed will offer links to the previous/next post - this can be useful but for some simple sites may just get in the way...";

    public static string HelpMarkdownShowRelatedContent =>
        "By default content pages will show 'related' content to provide users links to items like files, daily photos and other items mentioned/used by the content. For many sites this is a nice benefit for users - for some sites it can clutter the page and can be turned off.";

    public static string HelpMarkdownSiteAuthors =>
        "A value for the site creators/authors - for example " + "'Pointless Waymarks Team'.";

    public static string HelpMarkdownFfmpegDir =>
        "The Video Editor can make use of [FFmpeg](https://ffmpeg.org/)'s ffmpeg.exe and ffprobe.exe programs to help make sure that video files are in a format that will play correctly on the web.";

    public static string HelpMarkdownSiteDirAttribute =>
        "Dir attribute indicating text direction for the site - see the [dir attribute on MDN](https://developer.mozilla.org/en-US/docs/Web/HTML/Global_attributes/dir) for more information.";

    public static string HelpMarkdownSiteEmailTo => "An Email To for the site - example 'PointlessWaymarks@gmail.com'.";

    public static string HelpMarkdownSiteKeywords =>
        "Used in as the tags for the overall/entire site - for example " +
        "'outdoors,hiking,running,landscape,photography,history'.";

    public static string HelpMarkdownSiteLangAttribute =>
        "Lang attribute indicating the default language for the site - see [lang attribute on MDN](https://developer.mozilla.org/en-US/docs/Web/HTML/Global_attributes/lang) for more information.";

    public static string HelpMarkdownSiteName => "The 'human readable' Site Name - for example 'Pointless Waymarks'.";

    public static string HelpMarkdownSubtitleSummary =>
        "Used as a sub-title and site summary - example 'Ramblings, Questionable Geographics, Photographic Half-truths'.";

    public List<string> RegionChoices { get; set; }
    public StatusControlContext StatusContext { get; set; }

    [BlockingCommand]
    public async Task ChooseFfmpegDirectory()
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        var folderPicker = new VistaFolderBrowserDialog
        {
            Description = "Select directory containing ffmpeg.exe and/or ffprobe.exe",
            UseDescriptionForTitle = true,
            Multiselect = false
        };

        if (!string.IsNullOrWhiteSpace(EditorSettings.FfmpegDirectory))
        {
            var currentFfmpegFile = new FileInfo(EditorSettings.FfmpegDirectory);
            if (currentFfmpegFile.Directory?.Exists == true)
                folderPicker.SelectedPath = $"{currentFfmpegFile.Directory.FullName}\\";
        }

        if (!(folderPicker.ShowDialog() ?? false)) return;

        await ThreadSwitcher.ResumeBackgroundAsync();

        var selectedDirectory = new DirectoryInfo(folderPicker.SelectedPath);

        if (!selectedDirectory.Exists)
        {
            await StatusContext.ToastError("Selected directory does not exist?");
            return;
        }

        var ffmpegFile = new FileInfo(Path.Combine(selectedDirectory.FullName, "ffmpeg.exe"));
        var ffprobeFile = new FileInfo(Path.Combine(selectedDirectory.FullName, "ffprobe.exe"));

        if (!ffmpegFile.Exists && !ffprobeFile.Exists)
        {
            await StatusContext.ToastWarning(
                $"Neither ffmpeg.exe nor ffprobe.exe found in {selectedDirectory.FullName}");
            return;
        }

        if (!ffmpegFile.Exists)
            await StatusContext.ToastWarning($"ffmpeg.exe not found in {selectedDirectory.FullName}");

        if (!ffprobeFile.Exists)
            await StatusContext.ToastWarning($"ffprobe.exe not found in {selectedDirectory.FullName}");

        EditorSettings.FfmpegDirectory = ffmpegFile.Directory?.FullName ?? string.Empty;

        await StatusContext.ToastSuccess(
            $"Set ffmpeg directory to {selectedDirectory.FullName}");
    }

    public static async Task<UserSettingsEditorContext> CreateInstance(StatusControlContext? statusContext,
        UserSettings toLoad)
    {
        var factoryStatusContext = await StatusControlContext.CreateInstance(statusContext);

        await ThreadSwitcher.ResumeBackgroundAsync();

        return new UserSettingsEditorContext(factoryStatusContext, toLoad);
    }

    [BlockingCommand]
    public async Task DeleteAwsCredentials()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        CloudStorageCredentialsFromUserSettings.RemoveS3SiteCredentials();
    }

    [BlockingCommand]
    public async Task DeleteGeoNamesUserName()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        GeoNamesApiCredentials.RemoveGeoNamesSiteCredentials(UserSettingsSingleton.CurrentSettings().SettingsId);
    }

    [BlockingCommand]
    public async Task DeleteS3ServiceUrls()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        CloudStorageCredentialsFromUserSettings.RemoveS3ServiceUrls();
    }

    [BlockingCommand]
    public async Task DownloadAndSetupFfmpeg()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        try
        {
            StatusContext.Progress("Fetching latest ffmpeg version information...");

            // Get latest version info from ffbinaries API
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "PointlessWaymarks-CMS");

            var apiResponse = await httpClient.GetStringAsync("https://ffbinaries.com/api/v1/version/latest");

            var apiData = JsonSerializer.Deserialize<JsonElement>(apiResponse);

            if (!apiData.TryGetProperty("bin", out var binElement) ||
                !binElement.TryGetProperty("windows-64", out var windows64Element))
            {
                await StatusContext.ToastError("Could not parse ffmpeg download information from API");
                return;
            }

            StatusContext.Progress($"Found {windows64Element}");

            var ffmpegUrl = windows64Element.TryGetProperty("ffmpeg", out var ffmpegElement)
                ? ffmpegElement.GetString()
                : null;
            var ffprobeUrl = windows64Element.TryGetProperty("ffprobe", out var ffprobeElement)
                ? ffprobeElement.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(ffmpegUrl) || string.IsNullOrWhiteSpace(ffprobeUrl))
            {
                await StatusContext.ToastError("Could not get ffmpeg download URLs from API");
                return;
            }

            var version = apiData.TryGetProperty("version", out var versionElement)
                ? versionElement.GetString()
                : "unknown";

            StatusContext.Progress($"Latest ffmpeg version: {version} - {ffmpegUrl}");

            // Let user choose destination directory
            await ThreadSwitcher.ResumeForegroundAsync();

            var folderPicker = new VistaFolderBrowserDialog
            {
                Description = $"Select directory to install ffmpeg {version} - {ffprobeUrl}",
                UseDescriptionForTitle = true,
                Multiselect = false
            };

            if (!string.IsNullOrWhiteSpace(EditorSettings.FfmpegDirectory))
            {
                var currentDir = new DirectoryInfo(EditorSettings.FfmpegDirectory);
                if (currentDir.Parent?.Exists == true)
                    folderPicker.SelectedPath = $"{currentDir.Parent.FullName}\\";
            }

            if (!(folderPicker.ShowDialog() ?? false))
            {
                await StatusContext.ToastWarning("Download cancelled");
                return;
            }

            await ThreadSwitcher.ResumeBackgroundAsync();

            var targetDirectory = new DirectoryInfo(folderPicker.SelectedPath);

            if (!targetDirectory.Exists)
            {
                await StatusContext.ToastError("Selected directory does not exist");
                return;
            }

            // Download ffmpeg
            StatusContext.Progress("Downloading ffmpeg...");
            var ffmpegZipPath = Path.Combine(targetDirectory.FullName, $"ffmpeg-{Guid.NewGuid()}.zip");

            using var ffmpegResponse = await httpClient.GetAsync(ffmpegUrl, HttpCompletionOption.ResponseHeadersRead);
            ffmpegResponse.EnsureSuccessStatusCode();

            var totalBytes = ffmpegResponse.Content.Headers.ContentLength ?? -1;

            await using (var ffmpegZipStream = await ffmpegResponse.Content.ReadAsStreamAsync())
            await using (var fileStream = File.Create(ffmpegZipPath))
            {
                var buffer = new byte[8192];
                var totalRead = 0L;
                int bytesRead;
                var lastReportedQuarter = 0;

                while ((bytesRead = await ffmpegZipStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    totalRead += bytesRead;

                    if (totalBytes > 0)
                    {
                        var progressPercentage = (double)totalRead / totalBytes * 100;
                        var currentQuarter = progressPercentage switch
                        {
                            >= 75 => 3,
                            >= 50 => 2,
                            >= 25 => 1,
                            _ => 0
                        };

                        if (currentQuarter > lastReportedQuarter)
                        {
                            StatusContext.Progress($"Downloading ffmpeg... {(currentQuarter * 25)}%");
                            lastReportedQuarter = currentQuarter;
                        }
                    }
                }
            }

            StatusContext.Progress("Downloading ffmpeg... Done");

            // Download ffprobe
            StatusContext.Progress("Downloading ffprobe...");
            var ffprobeZipPath = Path.Combine(targetDirectory.FullName, $"ffprobe-{Guid.NewGuid()}.zip");

            using var ffprobeResponse = await httpClient.GetAsync(ffprobeUrl, HttpCompletionOption.ResponseHeadersRead);
            ffprobeResponse.EnsureSuccessStatusCode();

            totalBytes = ffprobeResponse.Content.Headers.ContentLength ?? -1;

            await using (var ffprobeZipStream = await ffprobeResponse.Content.ReadAsStreamAsync())
            await using (var fileStream = File.Create(ffprobeZipPath))
            {
                var buffer = new byte[8192];
                var totalRead = 0L;
                int bytesRead;
                var lastReportedQuarter = 0;

                while ((bytesRead = await ffprobeZipStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    totalRead += bytesRead;

                    if (totalBytes > 0)
                    {
                        var progressPercentage = (double)totalRead / totalBytes * 100;
                        var currentQuarter = progressPercentage switch
                        {
                            >= 75 => 3,
                            >= 50 => 2,
                            >= 25 => 1,
                            _ => 0
                        };

                        if (currentQuarter > lastReportedQuarter)
                        {
                            StatusContext.Progress($"Downloading ffprobe... {(currentQuarter * 25)}%");
                            lastReportedQuarter = currentQuarter;
                        }
                    }
                }
            }

            StatusContext.Progress("Downloading ffprobe... Done");

            StatusContext.Progress("Extracting ffmpeg");

            // Extract ffmpeg
            StatusContext.Progress("Extracting ffmpeg...");
            await ZipFile.ExtractToDirectoryAsync(ffmpegZipPath, targetDirectory.FullName, true);

            StatusContext.Progress("Extracting ffprobe");

            // Extract ffprobe
            StatusContext.Progress("Extracting ffprobe...");
            await ZipFile.ExtractToDirectoryAsync(ffprobeZipPath, targetDirectory.FullName, true);

            // Clean up temp files
            try
            {
                StatusContext.Progress($"Cleaning up temp files {ffmpegZipPath} and {ffprobeZipPath}");

                File.Delete(ffmpegZipPath);
                File.Delete(ffprobeZipPath);
            }
            catch
            {
                // Ignore cleanup errors
            }

            // Verify extraction
            var ffmpegExe = new FileInfo(Path.Combine(targetDirectory.FullName, "ffmpeg.exe"));
            var ffprobeExe = new FileInfo(Path.Combine(targetDirectory.FullName, "ffprobe.exe"));

            if (!ffmpegExe.Exists || !ffprobeExe.Exists)
            {
                await StatusContext.ToastError(
                    "Download completed but ffmpeg.exe or ffprobe.exe not found in target directory");
                return;
            }

            // Set the directory in settings
            EditorSettings.FfmpegDirectory = targetDirectory.FullName;

            await StatusContext.ToastSuccess(
                $"Successfully downloaded and installed ffmpeg {version} to {targetDirectory.FullName}");
        }
        catch (HttpRequestException ex)
        {
            await StatusContext.ShowMessageWithOkButton("Download Failed",
                $"Failed to download ffmpeg: {ex.Message}\n\nPlease check your internet connection and try again.");
        }
        catch (Exception ex)
        {
            await StatusContext.ShowMessageWithOkButton("Setup Failed",
                $"An error occurred while setting up ffmpeg: {ex.Message}");
        }
    }

    [BlockingCommand]
    public async Task SaveSettings()
    {
        await EditorSettings.WriteSettings();

        UserSettingsSingleton.CurrentSettings().InjectFrom(EditorSettings);
    }

    [NonBlockingCommand]
    public async Task ShowSitePictureSizesEditorWindow()
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        var window = await SitePictureSizesEditorWindow.CreateInstance(null);
        await window.PositionWindowAndShowOnUiThread();
    }

    [BlockingCommand]
    public async Task UserAwsKeyAndSecretEntry()
    {
        var newKeyEntry = await StatusContext.ShowStringEntry("Cloud Access Key",
            "Enter the Cloud Access Key", string.Empty);

        if (!newKeyEntry.Item1)
        {
            await StatusContext.ToastWarning("Cloud Credential Entry Cancelled");
            return;
        }

        var cleanedKey = newKeyEntry.Item2.TrimNullToEmpty();

        if (string.IsNullOrWhiteSpace(cleanedKey)) return;

        var newSecretEntry = await StatusContext.ShowStringEntry("Cloud Secret Key",
            "Enter the Secret Key", string.Empty);

        if (!newSecretEntry.Item1) return;

        var cleanedSecret = newSecretEntry.Item2.TrimNullToEmpty();

        if (string.IsNullOrWhiteSpace(cleanedSecret))
        {
            await StatusContext.ToastError("Cloud Credential Entry Canceled - secret can not be blank");
            return;
        }

        CloudStorageCredentialsFromUserSettings.SaveS3SiteCredential(cleanedKey, cleanedSecret);

        if (EditorSettings.SiteS3CloudProvider != nameof(S3Providers.Amazon))
        {
            var serviceUrl = await StatusContext.ShowStringEntry("Service URL",
                "Enter the S3 service URL. For Cloudflare this will be https://{accountId}.r2.cloudflarestorage.com - other providers, like Wasabi, will have a Service URL based on region (for example s3.ca-central-1.wasabisys.com for Wasabi-Toronto)",
                string.Empty);

            if (!serviceUrl.Item1) return;

            var cleanedServiceUrl = serviceUrl.Item2.TrimNullToEmpty();

            if (string.IsNullOrWhiteSpace(cleanedServiceUrl))
            {
                await StatusContext.ToastError("Cloud Credential Entry Canceled - Service URL can not be blank");
                return;
            }

            CloudStorageCredentialsFromUserSettings.SaveS3ServiceUrl(cleanedServiceUrl);
        }
    }

    [BlockingCommand]
    public async Task UserGeoNamesUserName()
    {
        var newKeyEntry = await StatusContext.ShowStringEntry("GeoNames Web API Username",
            "Enter your GeoNames Web API Username", string.Empty);

        if (!newKeyEntry.Item1)
        {
            await StatusContext.ToastWarning(" GeoNames Web API Username Entry Cancelled");
            return;
        }

        var cleanedUsername = newKeyEntry.Item2.TrimNullToEmpty();

        if (string.IsNullOrWhiteSpace(cleanedUsername)) return;

        GeoNamesApiCredentials.SaveGeoNamesSiteCredential(cleanedUsername, UserSettingsSingleton.CurrentSettings().SettingsId);
    }
}