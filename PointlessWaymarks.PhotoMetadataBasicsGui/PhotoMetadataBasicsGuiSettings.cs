using PointlessWaymarks.LlamaAspects;

namespace PointlessWaymarks.PhotoMetadataBasicsGui;

[NotifyPropertyChanged]
public partial class PhotoMetadataBasicsGuiSettings
{
    public string DefaultCreatedBy { get; set; } = string.Empty;

    public string DefaultLicense { get; set; } = string.Empty;

    public string FeatureIntersectSettingsFile { get; set; } = string.Empty;

    public string ProgramUpdateDirectory { get; set; } =
        @"https://software.pointlesswaymarks.com/Software/PointlessWaymarksSoftwareList.json";
}