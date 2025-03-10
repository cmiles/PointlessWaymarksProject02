using PointlessWaymarks.LlamaAspects;

namespace PointlessWaymarks.CmsWpfControls.ListFilterBuilder;

[NotifyPropertyChanged]
public partial class ContentIdListFilterFieldBuilder
{
    public required string FieldTitle { get; set; }
    public bool Not { get; set; }
    public string SearchText { get; set; } = string.Empty;
}