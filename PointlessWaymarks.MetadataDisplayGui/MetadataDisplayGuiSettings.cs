using PointlessWaymarks.LlamaAspects;

namespace PointlessWaymarks.MetadataDisplayGui;

[NotifyPropertyChanged]
public partial class MetadataDisplayGuiSettings
{
    public string ProgramUpdateDirectory { get; set; } =
        "https://software.pointlesswaymarks.com/Software/PointlessWaymarksSoftwareList.json";
}