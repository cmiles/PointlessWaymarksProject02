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
    
    public static Task<AppSettingsContext> CreateInstance(StatusControlContext? statusContext)
    {
        try
        {
            var createdByEntryContext = StringDataEntryContext.CreateInstance();
            createdByEntryContext.Title = "Default Name for Created By and License";
            createdByEntryContext.HelpText =
                "Default Name for Created By and License";

            var settings = PhotoMetadataBasicsGuiSettingTools.ReadSettings();

            createdByEntryContext.ReferenceValue = settings.DefaultCreatedBy;
            createdByEntryContext.UserValue = settings.DefaultCreatedBy;

            var factoryReturn = new AppSettingsContext
            {
                CreatedByEntryContext = createdByEntryContext,
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


    [BlockingCommand]
    public async Task SaveSettings()
    {
        var settings = PhotoMetadataBasicsGuiSettingTools.ReadSettings();
        settings.DefaultCreatedBy = CreatedByEntryContext.UserValue.TrimNullToEmpty();
        await PhotoMetadataBasicsGuiSettingTools.WriteSettings(settings);

        CreatedByEntryContext.ReferenceValue = settings.DefaultCreatedBy;
    }
}