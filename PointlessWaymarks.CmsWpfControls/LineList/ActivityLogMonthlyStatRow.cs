using PointlessWaymarks.LlamaAspects;

namespace PointlessWaymarks.CmsWpfControls.LineList;

[NotifyPropertyChanged]
public partial class ActivityLogMonthlyStatRow
{
    public int Activities { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public double Climb { get; set; }
    public double Descent { get; set; }
    public double Hours { get; set; }
    public List<Guid> LineContentIds { get; set; } = [];
    public int MaxElevation { get; set; }
    public double Miles { get; set; }
    public int MinElevation { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }
}