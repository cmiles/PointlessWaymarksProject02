using System.IO;
using Amazon;
using Ookii.Dialogs.Wpf;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.CommonTools.S3;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.Status;

namespace PointlessWaymarks.SiteViewerGui.Controls;

[NotifyPropertyChanged]
[GenerateStatusCommands]
public partial class OpenCloudViewerSettingsEditorContext
{
    public EventHandler<bool>? EditFinished;

    public OpenCloudViewerSettingsEditorContext(StatusControlContext statusContext, OpenCloudViewerSettings toLoad,
        string settingsFullFileName)
    {
        StatusContext = statusContext;
        BuildCommands();

        CloudProviderChoices = [string.Empty, .. Enum.GetNames<S3Providers>()];
        RegionChoices = [.. RegionEndpoint.EnumerableAllRegions.Select(x => x.SystemName)];
        EditorSettings = toLoad;
        SettingsFullFileName = settingsFullFileName;
    }

    public List<string> CloudProviderChoices { get; set; }

    public OpenCloudViewerSettings EditorSettings { get; set; }

    public static string HelpMarkdownDomain =>
        "This is the subdomain + domain and optionally port - for example 'PointlessWaymarks.com' or 'software.pointlesswaymarks.com' - without the protocol (no 'https' for example).";

    public static string HelpMarkdownName =>
        "Settings must be given a name to identify these settings.";

    public static string HelpMarkdownS3Information =>
        "Cloud S3 Storage from Amazon, Cloudflare or Wasabi where the site is stored. You will need to enter the provider and information below - this should only be used for Read Only credentials that are limited to storage reading use only. These credentials WILL NOT BE STORED WITH ANY SECURITY, they will be in a plain text file!";

    public List<string> RegionChoices { get; set; }
    public string SettingsFullFileName { get; set; }
    public StatusControlContext StatusContext { get; set; }

    [BlockingCommand]
    public async Task Cancel()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();
        // Signal window close event with success = false
        EditFinished?.Invoke(this, false);
    }

    public static async Task<OpenCloudViewerSettingsEditorContext> CreateInstance(StatusControlContext? statusContext,
        OpenCloudViewerSettings toLoad, string settingsFullFileName)
    {
        var factoryStatusContext = await StatusControlContext.CreateInstance(statusContext);

        await ThreadSwitcher.ResumeBackgroundAsync();

        return new OpenCloudViewerSettingsEditorContext(factoryStatusContext, toLoad, settingsFullFileName);
    }

    [BlockingCommand]
    public async Task Save()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        // Validate settings first
        if (!await SaveSettingsValidation())
            return;

        // Check if we have a valid file name to save to
        if (string.IsNullOrWhiteSpace(SettingsFullFileName))
        {
            // No filename set, use SaveAs
            await SaveAs();
            return;
        }

        var settingsFile = new FileInfo(SettingsFullFileName);

        // Check if the file path is valid and the directory exists
        if (settingsFile.Directory is not { Exists: true })
        {
            // Invalid file path, use SaveAs
            await StatusContext.ToastWarning("Current settings file path is not valid - please choose a location");
            await SaveAs();
            return;
        }

        try
        {
            // Write settings to the existing file
            await OpenCloudViewerSettings.WriteSettings(EditorSettings, SettingsFullFileName);

            await StatusContext.ToastSuccess($"Settings saved to {SettingsFullFileName}");

            // Signal window close event with success = true
            EditFinished?.Invoke(this, true);
        }
        catch (Exception ex)
        {
            await StatusContext.ShowMessageWithOkButton("Save Failed",
                $"Failed to save settings: {ex.Message}");
        }
    }

    [BlockingCommand]
    public async Task SaveAs()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        // Validate settings first
        if (!await SaveSettingsValidation())
            return;

        await ThreadSwitcher.ResumeForegroundAsync();

        // Prompt user for file location
        var saveDialog = new VistaSaveFileDialog
        {
            Title = "Save Site Settings As",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = ".json",
            AddExtension = true,
            FileName = $"{FileAndFolderTools.TryMakeFilenameValid(EditorSettings.CloudViewerSettingsName)}.json"
        };

        // Set initial directory if current settings file exists
        if (!string.IsNullOrWhiteSpace(SettingsFullFileName))
        {
            var currentFile = new FileInfo(SettingsFullFileName);
            if (currentFile.Directory?.Exists ?? false)
                saveDialog.InitialDirectory = currentFile.Directory.FullName;
        }

        if (!(saveDialog.ShowDialog() ?? false))
        {
            await StatusContext.ToastWarning("Save cancelled");
            return;
        }

        await ThreadSwitcher.ResumeBackgroundAsync();

        var selectedFileName = saveDialog.FileName;

        try
        {
            // Write settings to the selected file
            await OpenCloudViewerSettings.WriteSettings(EditorSettings, selectedFileName);

            // Update the settings file name
            SettingsFullFileName = selectedFileName;

            await StatusContext.ToastSuccess($"Settings saved to {selectedFileName}");

            // Signal window close event with success = true
            EditFinished?.Invoke(this, true);
        }
        catch (Exception ex)
        {
            await StatusContext.ShowMessageWithOkButton("Save Failed",
                $"Failed to save settings: {ex.Message}");
        }
    }

    public async Task<bool> SaveSettingsValidation()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (string.IsNullOrWhiteSpace(EditorSettings.CloudViewerSettingsName))
        {
            await StatusContext.ToastError("Site Name can not be blank");
            return false;
        }

        if (string.IsNullOrWhiteSpace(EditorSettings.CloudViewerSiteDomain))
        {
            await StatusContext.ToastError("Site Domain can not be blank");
            return false;
        }

        if (string.IsNullOrWhiteSpace(EditorSettings.CloudViewerProvider))
        {
            await StatusContext.ToastError("Provider can not be blank");
            return false;
        }

        if (string.IsNullOrWhiteSpace(EditorSettings.CloudViewerAccessKey))
        {
            await StatusContext.ToastError(
                "An Access Key is required");
            return false;
        }

        if (string.IsNullOrWhiteSpace(EditorSettings.CloudViewerSecret))
        {
            await StatusContext.ToastError(
                "A Secret must be provided");
            return false;
        }

        if (EditorSettings.CloudViewerProvider == nameof(S3Providers.Amazon))
        {
            EditorSettings.CloudServiceUrl = string.Empty;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(EditorSettings.CloudServiceUrl))
            {
                await StatusContext.ToastError("Service URL is missing or invalid");
                return false;
            }
        }

        return true;
    }
}