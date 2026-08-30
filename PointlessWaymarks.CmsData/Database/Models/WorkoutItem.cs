namespace PointlessWaymarks.CmsData.Database.Models;

public class WorkoutItem
{
    public int? Calories { get; set; }
    public required Guid ContentId { get; set; }
    public double? DistanceMiles { get; set; }
    public int DurationMinutes { get; set; }
    public int ClimbFeet { get; set; }
    public int DescentFeet { get; set; }
    public int Id { get; set; }
    public string Note { get; set; } = string.Empty;
    public string WorkoutBy { get; set; } = string.Empty;
    public required DateTime WorkoutOn { get; set; }
    public string WorkoutType { get; set; } = string.Empty;
}