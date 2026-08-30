using PointlessWaymarks.LlamaAspects;

namespace PointlessWaymarks.CmsWpfControls.WorkoutItemsList;

[NotifyPropertyChanged]
public partial class WorkoutActivityLogMonthlyStatRow
{
    public int Calories { get; set; }
    public int Climb { get; set; }
    public int Descent { get; set; }
    public double Hours { get; set; }
    public double Miles { get; set; }
    public int Month { get; set; }
    public string WorkoutBy { get; set; } = string.Empty;
    public List<Guid> WorkoutContentIds { get; set; } = [];
    public int Workouts { get; set; }
    public string WorkoutType { get; set; } = string.Empty;
    public int Year { get; set; }
}
