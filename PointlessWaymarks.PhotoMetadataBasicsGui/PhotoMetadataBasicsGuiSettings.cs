using PointlessWaymarks.LlamaAspects;

namespace PointlessWaymarks.PhotoMetadataBasicsGui;

[NotifyPropertyChanged]
public partial class PhotoMetadataBasicsGuiSettings
{
    public string DefaultCreatedBy { get; set; } = string.Empty;

    public string DefaultGpxDirectory { get; set; } = string.Empty;

    public string FeatureIntersectSettingsFile { get; set; } = string.Empty;

    public string ImportPhotosDestinationFolder { get; set; } = string.Empty;

    public bool MoveFinishedFilesOnImport { get; set; }

    public bool MoveWorkingFilesOnImport { get; set; }

    public bool OverwriteOnImport { get; set; }

    public string ProgramUpdateDirectory { get; set; } =
        @"https://software.pointlesswaymarks.com/Software/PointlessWaymarksSoftwareList.json";
}