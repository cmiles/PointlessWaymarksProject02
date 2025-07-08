using System.Diagnostics;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using PointlessWaymarks.LlamaAspects;

namespace PointlessWaymarks.CmsWpfControls.LinkList;

[NotifyPropertyChanged]
public partial class LinkSnapshotImageItem
{
    public string Description { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
}