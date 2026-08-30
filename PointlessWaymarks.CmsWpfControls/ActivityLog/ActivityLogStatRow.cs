using PointlessWaymarks.LlamaAspects;

namespace PointlessWaymarks.CmsWpfControls.ActivityLog;

[NotifyPropertyChanged]
public partial class ActivityLogStatRow
{
    public int Activities { get; set; }
    public int Calories { get; set; }
    public int Climb { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public int Descent { get; set; }
    public double Hours { get; set; }
    public List<Guid> LineContentIds { get; set; } = [];
    public double Miles { get; set; }
    public int Month { get; set; }
    public List<Guid> WorkoutContentIds { get; set; } = [];
    public string WorkoutType { get; set; } = string.Empty;
    public int Year { get; set; }
}
