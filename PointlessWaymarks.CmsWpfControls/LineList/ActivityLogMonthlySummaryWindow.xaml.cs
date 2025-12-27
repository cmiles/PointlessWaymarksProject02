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

namespace PointlessWaymarks.CmsWpfControls.LineList;

[NotifyPropertyChanged]
[GenerateStatusCommands]
[StaThreadConstructorGuard]
public partial class ActivityLogMonthlySummaryWindow
{
    public ActivityLogMonthlySummaryWindow()

    {
        InitializeComponent();

        DataContext = this;
        
        PropertyChanged += OnPropertyChanged;
    }

    public required ObservableCollection<ActivityLogMonthlyStatRow> Items { get; set; }
    public ActivityLogMonthlyStatRow? SelectedItem { get; set; }
    public List<ActivityLogMonthlyStatRow> SelectedItems { get; set; } = [];
    public required StatusControlContext StatusContext { get; set; }
    
    public int SelectedRowCount { get; set; }
    public int TotalActivities { get; set; }
    public double TotalMiles { get; set; }
    public double TotalHours { get; set; }
    public double TotalClimb { get; set; }
    public double TotalDescent { get; set; }
    public int MinimumElevation { get; set; }
    public int MaximumElevation { get; set; }

    [BlockingCommand]
    public async Task ContentMap(ActivityLogMonthlyStatRow? row)
    {
        if (row == null)
        {
            await StatusContext.ToastError("No Row Selected?");
            return;
        }

        var allGuids = SelectedItems.SelectMany(x => x.LineContentIds).ToList();

        var mapWindow =
            await ContentMapWindow.CreateInstance(new ContentMapListLoader("Mapped Content", allGuids));

        await mapWindow.PositionWindowAndShowOnUiThread();
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task ContentMapForAllSelected()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var allGuids = SelectedItems.SelectMany(x => x.LineContentIds).ToList();

        var mapWindow =
            await ContentMapWindow.CreateInstance(new ContentMapListLoader("Mapped Content", allGuids));

        await mapWindow.PositionWindowAndShowOnUiThread();
    }

    public static async Task<ActivityLogMonthlySummaryWindow> CreateInstance(List<ActivityLogMonthlyStatRow> statRows)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        var toReturn = new ActivityLogMonthlySummaryWindow
        {
            StatusContext = await StatusControlContext.CreateInstance(),
            Items = new ObservableCollection<ActivityLogMonthlyStatRow>(statRows)
        };

        toReturn.BuildCommands();

        return toReturn;
    }

    public static async Task<ActivityLogMonthlySummaryWindow> CreateInstance(List<Guid> lineContentIds)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var db = await Db.Context();

        var lines = await db.LineContents
            .Where(x => lineContentIds.Contains(x.ContentId) && x.RecordingStartedOn != null &&
                        x.RecordingEndedOn != null && x.RecordingStartedOn < x.RecordingEndedOn &&
                        x.IncludeInActivityLog).AsNoTracking()
            .ToListAsync();

        var grouped = lines.GroupBy(x =>
                new { x.RecordingStartedOn!.Value.Year, x.RecordingStartedOn.Value.Month, x.ActivityType, x.CreatedBy })
            .OrderByDescending(x => x.Key.Year).ThenByDescending(x => x.Key.Month);

        var reportRows = grouped.Select(x => new ActivityLogMonthlyStatRow
        {
            CreatedBy = x.Key.CreatedBy ?? string.Empty,
            Year = x.Key.Year,
            Month = x.Key.Month,
            ActivityType = x.Key.ActivityType ?? string.Empty,
            Activities = x.Count(),
            Miles = x.Sum(y => y.LineDistance),
            Hours = x.Where(y => y is { RecordingStartedOn: not null, RecordingEndedOn: not null } &&
                                y.RecordingStartedOn < y.RecordingEndedOn)
                    .Select(y => y.RecordingEndedOn!.Value - y.RecordingStartedOn!.Value).Sum(y => y.TotalMinutes) / 60D,
            MinElevation = (int)Math.Floor(x.Min(y => y.MinimumElevation)),
            MaxElevation = (int)Math.Floor(x.Max(y => y.MaximumElevation)),
            Climb = x.Sum(y => y.ClimbElevation),
            Descent = x.Sum(y => y.DescentElevation),
            LineContentIds = x.Select(y => y.ContentId).ToList()
        }).ToList();

        return await CreateInstance(reportRows);
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SelectedItems))
        {
            UpdateTotals();
        }
    }

    public ActivityLogMonthlyStatRow? SelectedListItem()
    {
        return SelectedItem;
    }

    public List<ActivityLogMonthlyStatRow> SelectedListItems()
    {
        return SelectedItems;
    }

    private void Selector_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext == null) return;
        SelectedItems =
            LineStatsDataGrid?.SelectedItems.Cast<ActivityLogMonthlyStatRow>().ToList() ??
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
            MinimumElevation = 0;
            MaximumElevation = 0;
            return;
        }

        SelectedRowCount = SelectedItems.Count;
        TotalActivities = SelectedItems.Sum(x => x.Activities);
        TotalMiles = SelectedItems.Sum(x => x.Miles);
        TotalHours = SelectedItems.Sum(x => x.Hours);
        TotalClimb = SelectedItems.Sum(x => x.Climb);
        TotalDescent = SelectedItems.Sum(x => x.Descent);
        MinimumElevation = SelectedItems.Any() ? SelectedItems.Min(x => x.MinElevation) : 0;
        MaximumElevation = SelectedItems.Any() ? SelectedItems.Max(x => x.MaxElevation) : 0;
    }
}