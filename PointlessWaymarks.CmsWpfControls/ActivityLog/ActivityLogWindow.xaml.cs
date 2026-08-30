using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using PointlessWaymarks.CmsData.Database;
using PointlessWaymarks.CmsWpfControls.ContentMap;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.Status;
using PointlessWaymarks.WpfCommon.Utility;

namespace PointlessWaymarks.CmsWpfControls.ActivityLog;

[NotifyPropertyChanged]
[GenerateStatusCommands]
[StaThreadConstructorGuard]
public partial class ActivityLogWindow
{
    public ActivityLogWindow()
    {
        InitializeComponent();

        DataContext = this;

        PropertyChanged += OnPropertyChanged;
    }

    public required ObservableCollection<ActivityLogStatRow> Items { get; init; }
    public ActivityLogStatRow? SelectedItem { get; set; }
    public List<ActivityLogStatRow> SelectedItems { get; set; } = [];
    public int SelectedRowCount { get; set; }
    public required StatusControlContext StatusContext { get; init; }
    public int TotalActivities { get; set; }
    public int TotalCalories { get; set; }
    public int TotalClimb { get; set; }
    public int TotalDescent { get; set; }
    public double TotalHours { get; set; }
    public double TotalMiles { get; set; }

    [BlockingCommand]
    public async Task ContentMap(ActivityLogStatRow? row)
    {
        if (row == null)
        {
            await StatusContext.ToastError("No Row Selected?");
            return;
        }

        if (!row.LineContentIds.Any())
        {
            await StatusContext.ToastWarning("No Mappable (Line) Content in this Row.");
            return;
        }

        var mapWindow =
            await ContentMapWindow.CreateInstance(new ContentMapListLoader("Mapped Content", row.LineContentIds));

        await mapWindow.PositionWindowAndShowOnUiThread();
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task ContentMapForAllSelected()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var allGuids = SelectedItems.SelectMany(x => x.LineContentIds).Distinct().ToList();

        if (!allGuids.Any())
        {
            await StatusContext.ToastWarning("No Mappable (Line) Content in Selected Rows.");
            return;
        }

        var mapWindow =
            await ContentMapWindow.CreateInstance(new ContentMapListLoader("Mapped Content", allGuids));

        await mapWindow.PositionWindowAndShowOnUiThread();
    }

    public static async Task<ActivityLogWindow> CreateInstance(List<ActivityLogStatRow> statRows)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        var toReturn = new ActivityLogWindow
        {
            StatusContext = await StatusControlContext.CreateInstance(),
            Items = new ObservableCollection<ActivityLogStatRow>(statRows)
        };

        toReturn.BuildCommands();

        return toReturn;
    }

    public static async Task<ActivityLogWindow> CreateInstance(List<Guid>? lineContentIds = null,
        List<Guid>? workoutContentIds = null)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var db = await Db.Context();

        var linesQuery = db.LineContents
            .Where(x => x.RecordingStartedOn != null &&
                        x.RecordingEndedOn != null && x.RecordingStartedOn < x.RecordingEndedOn &&
                        x.IncludeInActivityLog);

        if (lineContentIds != null)
        {
            linesQuery = linesQuery.Where(x => lineContentIds.Contains(x.ContentId));
        }

        var lines = await linesQuery.AsNoTracking().ToListAsync();

        var workoutsQuery = db.WorkoutItems.AsQueryable();

        if (workoutContentIds != null)
        {
            workoutsQuery = workoutsQuery.Where(x => workoutContentIds.Contains(x.ContentId));
        }

        var workouts = await workoutsQuery.AsNoTracking().ToListAsync();

        var unifiedActivities = new List<(int Year, int Month, string CreatedBy, string WorkoutType, double Miles, double Hours, int Climb, int Descent, int Calories, Guid? LineContentId, Guid? WorkoutContentId)>();

        foreach (var line in lines)
        {
            var activityType = string.IsNullOrWhiteSpace(line.ActivityType) ? "Tracked" : line.ActivityType.Trim();
            var startTime = line.RecordingStartedOn!.Value;
            var endTime = line.RecordingEndedOn!.Value;
            var durationHours = (endTime - startTime).TotalMinutes / 60D;

            unifiedActivities.Add((
                startTime.Year,
                startTime.Month,
                line.CreatedBy ?? string.Empty,
                activityType,
                line.LineDistance,
                durationHours,
                (int)Math.Round(line.ClimbElevation),
                (int)Math.Round(line.DescentElevation),
                0,
                line.ContentId,
                null
            ));
        }

        foreach (var workout in workouts)
        {
            var workoutType = string.IsNullOrWhiteSpace(workout.WorkoutType) ? "Workout" : workout.WorkoutType.Trim();
            var durationHours = workout.DurationMinutes / 60D;

            unifiedActivities.Add((
                workout.WorkoutOn.Year,
                workout.WorkoutOn.Month,
                workout.WorkoutBy ?? string.Empty,
                workoutType,
                workout.DistanceMiles ?? 0,
                durationHours,
                workout.ClimbFeet,
                workout.DescentFeet,
                workout.Calories ?? 0,
                null,
                workout.ContentId
            ));
        }

        var monthGroups = unifiedActivities
            .GroupBy(x => new { x.Year, x.Month })
            .OrderByDescending(x => x.Key.Year)
            .ThenByDescending(x => x.Key.Month);

        var reportRows = new List<ActivityLogStatRow>();

        foreach (var monthGroup in monthGroups)
        {
            var year = monthGroup.Key.Year;
            var month = monthGroup.Key.Month;
            var monthItems = monthGroup.ToList();

            // 1. CreatedBy / WorkoutType breakdown rows
            var userAndTypeGroups = monthItems
                .GroupBy(x => new { x.CreatedBy, x.WorkoutType })
                .OrderBy(x => x.Key.CreatedBy)
                .ThenBy(x => x.Key.WorkoutType);

            foreach (var group in userAndTypeGroups)
            {
                reportRows.Add(new ActivityLogStatRow
                {
                    CreatedBy = group.Key.CreatedBy,
                    WorkoutType = group.Key.WorkoutType,
                    Year = year,
                    Month = month,
                    Activities = group.Count(),
                    Miles = group.Sum(y => y.Miles),
                    Hours = group.Sum(y => y.Hours),
                    Climb = group.Sum(y => y.Climb),
                    Descent = group.Sum(y => y.Descent),
                    Calories = group.Sum(y => y.Calories),
                    LineContentIds = group.Where(y => y.LineContentId != null).Select(y => y.LineContentId!.Value).ToList(),
                    WorkoutContentIds = group.Where(y => y.WorkoutContentId != null).Select(y => y.WorkoutContentId!.Value).ToList()
                });
            }

            // 2. WorkoutType summary rows
            var typeGroups = monthItems
                .GroupBy(x => x.WorkoutType)
                .OrderBy(x => x.Key);

            foreach (var group in typeGroups)
            {
                reportRows.Add(new ActivityLogStatRow
                {
                    CreatedBy = string.Empty,
                    WorkoutType = group.Key,
                    Year = year,
                    Month = month,
                    Activities = group.Count(),
                    Miles = group.Sum(y => y.Miles),
                    Hours = group.Sum(y => y.Hours),
                    Climb = group.Sum(y => y.Climb),
                    Descent = group.Sum(y => y.Descent),
                    Calories = group.Sum(y => y.Calories),
                    LineContentIds = group.Where(y => y.LineContentId != null).Select(y => y.LineContentId!.Value).ToList(),
                    WorkoutContentIds = group.Where(y => y.WorkoutContentId != null).Select(y => y.WorkoutContentId!.Value).ToList()
                });
            }

            // 3. Month Total row
            reportRows.Add(new ActivityLogStatRow
            {
                CreatedBy = string.Empty,
                WorkoutType = "Total",
                Year = year,
                Month = month,
                Activities = monthItems.Count,
                Miles = monthItems.Sum(y => y.Miles),
                Hours = monthItems.Sum(y => y.Hours),
                Climb = monthItems.Sum(y => y.Climb),
                Descent = monthItems.Sum(y => y.Descent),
                Calories = monthItems.Sum(y => y.Calories),
                LineContentIds = monthItems.Where(y => y.LineContentId != null).Select(y => y.LineContentId!.Value).ToList(),
                WorkoutContentIds = monthItems.Where(y => y.WorkoutContentId != null).Select(y => y.WorkoutContentId!.Value).ToList()
            });
        }

        return await CreateInstance(reportRows);
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SelectedItems)) UpdateTotals();
    }

    public ActivityLogStatRow? SelectedListItem()
    {
        return SelectedItem;
    }

    public List<ActivityLogStatRow> SelectedListItems()
    {
        return SelectedItems;
    }

    private void Selector_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext == null) return;
        SelectedItems =
            ActivityStatsDataGrid?.SelectedItems.Cast<ActivityLogStatRow>().ToList() ??
            [];
    }

    private void UpdateTotals()
    {
        if (!SelectedItems.Any())
        {
            SelectedRowCount = 0;
            TotalActivities = 0;
            TotalMiles = 0;
            TotalHours = 0;
            TotalClimb = 0;
            TotalDescent = 0;
            TotalCalories = 0;
            return;
        }

        SelectedRowCount = SelectedItems.Count;
        TotalActivities = SelectedItems.Sum(x => x.Activities);
        TotalMiles = SelectedItems.Sum(x => x.Miles);
        TotalHours = SelectedItems.Sum(x => x.Hours);
        TotalClimb = SelectedItems.Sum(x => x.Climb);
        TotalDescent = SelectedItems.Sum(x => x.Descent);
        TotalCalories = SelectedItems.Sum(x => x.Calories);
    }
}
