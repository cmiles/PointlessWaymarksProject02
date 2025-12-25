using System.IO;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.SiteViewerGui.Controls;

namespace PointlessWaymarks.SiteViewerGui;

[NotifyPropertyChanged]
public partial class SecureCloudViewerSettingsFileListItem
{
    public required SecureCloudViewerSettings ParsedSettings { get; set; }
    public required FileInfo SettingsFile { get; set; }
}