using System.IO;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.SiteViewerGui.Controls;

namespace PointlessWaymarks.SiteViewerGui;

[NotifyPropertyChanged]
public partial class OpenCloudViewerSettingsFileListItem
{
    public required OpenCloudViewerSettings ParsedSettings { get; set; }
    public required FileInfo SettingsFile { get; set; }
}