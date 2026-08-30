using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using PointlessWaymarks.CmsData.Database;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.Status;
using PointlessWaymarks.WpfCommon.Utility;

namespace PointlessWaymarks.CmsWpfControls.WorkoutItemsList;

[NotifyPropertyChanged]
[GenerateStatusCommands]
[StaThreadConstructorGuard]
public partial class WorkoutActivityLogMonthlySummaryWindow
{
    public WorkoutActivityLogMonthlySummaryWindow()
    {
        InitializeComponent();

        DataContext = this;

        PropertyChanged += OnPropertyChanged;
    }

    public required ObservableCollection<WorkoutActivityLogMonthlyStatRow> Items { get; init; }
    public WorkoutActivityLogMonthlyStatRow? SelectedItem { get; set; }
    public List<WorkoutActivityLogMonthlyStatRow> SelectedItems { get; set; } = [];
    public int SelectedRowCount { get; set; }
    public required StatusControlContext StatusContext { get; init; }
    public int TotalCalories { get; set; }
    public int TotalClimb { get; set; }
    public int TotalDescent { get; set; }
    public double TotalHours { get; set; }
    public double TotalMiles { get; set; }
    public int TotalWorkouts { get; set; }

    public static async Task<WorkoutActivityLogMonthlySummaryWindow> CreateInstance(List<WorkoutActivityLogMonthlyStatRow> statRows)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        var toReturn = new WorkoutActivityLogMonthlySummaryWindow
        {
            StatusContext = await StatusControlContext.CreateInstance(),
            Items = new ObservableCollection<WorkoutActivityLogMonthlyStatRow>(statRows)
        };

        toReturn.BuildCommands();

        return toReturn;
    }

    public static async Task<WorkoutActivityLogMonthlySummaryWindow> CreateInstance(List<Guid> workoutContentIds)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var db = await Db.Context();

        var workouts = await db.WorkoutItems
            .Where(x => workoutContentIds.Contains(x.ContentId)).AsNoTracking()
            .ToListAsync();

        var grouped = workouts.GroupBy(x =>
                new
                {
                    x.WorkoutOn.Year,
                    x.WorkoutOn.Month,
                    WorkoutType = x.WorkoutType ?? string.Empty,
                    WorkoutBy = x.WorkoutBy ?? string.Empty
                })
            .OrderByDescending(x => x.Key.Year).ThenByDescending(x => x.Key.Month).ThenBy(x => x.Key.WorkoutType);

        var reportRows = grouped.Select(x => new WorkoutActivityLogMonthlyStatRow
        {
            WorkoutBy = x.Key.WorkoutBy,
            Year = x.Key.Year,
            Month = x.Key.Month,
            WorkoutType = x.Key.WorkoutType,
            Workouts = x.Count(),
            Miles = x.Sum(y => y.DistanceMiles ?? 0),
            Hours = x.Sum(y => y.DurationMinutes) / 60D,
            Climb = x.Sum(y => y.ClimbFeet),
            Descent = x.Sum(y => y.DescentFeet),
            Calories = x.Sum(y => y.Calories ?? 0),
            WorkoutContentIds = x.Select(y => y.ContentId).ToList()
        }).ToList();

        return await CreateInstance(reportRows);
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SelectedItems)) UpdateTotals();
    }

    public WorkoutActivityLogMonthlyStatRow? SelectedListItem()
    {
        return SelectedItem;
    }

    public List<WorkoutActivityLogMonthlyStatRow> SelectedListItems()
    {
        return SelectedItems;
    }

    private void Selector_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext == null) return;
        SelectedItems =
            WorkoutStatsDataGrid?.SelectedItems.Cast<WorkoutActivityLogMonthlyStatRow>().ToList() ??
            [];
    }

    private void UpdateTotals()
    {
        if (!SelectedItems.Any())
        {
            SelectedRowCount = 0;
            TotalWorkouts = 0;
            TotalMiles = 0;
            TotalHours = 0;
            TotalClimb = 0;
            TotalDescent = 0;
            TotalCalories = 0;
            return;
        }

        SelectedRowCount = SelectedItems.Count;
        TotalWorkouts = SelectedItems.Sum(x => x.Workouts);
        TotalMiles = SelectedItems.Sum(x => x.Miles);
        TotalHours = SelectedItems.Sum(x => x.Hours);
        TotalClimb = SelectedItems.Sum(x => x.Climb);
        TotalDescent = SelectedItems.Sum(x => x.Descent);
        TotalCalories = SelectedItems.Sum(x => x.Calories);
    }
}
