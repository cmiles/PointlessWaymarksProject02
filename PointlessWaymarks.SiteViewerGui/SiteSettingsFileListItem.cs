using System.IO;
using PointlessWaymarks.CmsData;
using PointlessWaymarks.LlamaAspects;

namespace PointlessWaymarks.SiteViewerGui;

[NotifyPropertyChanged]
public partial class SiteSettingsFileListItem
{
    public required UserSettings ParsedSettings { get; set; }
    public required FileInfo SettingsFile { get; set; }
}