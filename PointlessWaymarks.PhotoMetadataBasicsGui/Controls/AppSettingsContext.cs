using System.IO;
using Metalama.Patterns.Observability;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.Status;
using PointlessWaymarks.WpfCommon.StringDataEntry;

namespace PointlessWaymarks.PhotoMetadataBasicsGui.Controls;

[Observable]
[GenerateStatusCommands]
public partial class AppSettingsContext
{
    public required StatusControlContext StatusContext { get; set; }

    public required StringDataEntryContext CreatedByEntryContext { get; set; }

    public required StringDataEntryContext DefaultGpxDirectoryEntryContext { get; set; }
    
    public static Task<AppSettingsContext> CreateInstance(StatusControlContext? statusContext)
    {
        try
        {
            var createdByEntryContext = StringDataEntryContext.CreateInstance();
            createdByEntryContext.Title = "Default Name for Created By and License";
            createdByEntryContext.HelpText =
                "Default Name for Created By and License";

            var defaultGpxDirectoryEntryContext = StringDataEntryContext.CreateInstance();
            defaultGpxDirectoryEntryContext.Title = "Default GPX Directory";
            defaultGpxDirectoryEntryContext.HelpText =
                "Directory containing GPX files to automatically load when GeoTagging photos.";

            var settings = PhotoMetadataBasicsGuiSettingTools.ReadSettings();

            createdByEntryContext.ReferenceValue = settings.DefaultCreatedBy;
            createdByEntryContext.UserValue = settings.DefaultCreatedBy;

            defaultGpxDirectoryEntryContext.ReferenceValue = settings.DefaultGpxDirectory;
            defaultGpxDirectoryEntryContext.UserValue = settings.DefaultGpxDirectory;

            var factoryReturn = new AppSettingsContext
            {
                CreatedByEntryContext = createdByEntryContext,
                DefaultGpxDirectoryEntryContext = defaultGpxDirectoryEntryContext,
                StatusContext = statusContext ?? StatusControlContext.CreateInstance().Result
            };

            factoryReturn.BuildCommands();

            return Task.FromResult(factoryReturn);
        }
        catch (Exception exception)
        {
            return Task.FromException<AppSettingsContext>(exception);
        }
    }


    [NonBlockingCommand]
    public async Task BrowseForDefaultGpxDirectory()
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        var folderDialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Default GPX Directory"
        };

        var currentValue = DefaultGpxDirectoryEntryContext.UserValue.TrimNullToEmpty();
        if (Directory.Exists(currentValue))
            folderDialog.InitialDirectory = currentValue;

        if (folderDialog.ShowDialog() == true)
            DefaultGpxDirectoryEntryContext.UserValue = folderDialog.FolderName;
    }

    [BlockingCommand]
    public async Task SaveSettings()
    {
        var settings = PhotoMetadataBasicsGuiSettingTools.ReadSettings();
        settings.DefaultCreatedBy = CreatedByEntryContext.UserValue.TrimNullToEmpty();
        settings.DefaultGpxDirectory = DefaultGpxDirectoryEntryContext.UserValue.TrimNullToEmpty();
        await PhotoMetadataBasicsGuiSettingTools.WriteSettings(settings);

        CreatedByEntryContext.ReferenceValue = settings.DefaultCreatedBy;
        DefaultGpxDirectoryEntryContext.ReferenceValue = settings.DefaultGpxDirectory;
    }
}