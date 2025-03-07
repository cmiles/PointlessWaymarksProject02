using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CronExpressionDescriptor;
using Cronos;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.EntityFrameworkCore;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.PowerShellRunnerData;
using PointlessWaymarks.PowerShellRunnerData.Models;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.ColumnSort;
using PointlessWaymarks.WpfCommon.Status;
using Serilog;
using SkiaSharp;

namespace PointlessWaymarks.PowerShellRunnerGui.Controls;

[NotifyPropertyChanged]
[GenerateStatusCommands]
public partial class ScriptJobListContext
{
    private readonly PeriodicTimer _cronNextTimer = new(TimeSpan.FromSeconds(30));
    private string _databaseFile = string.Empty;
    private Guid _dbId = Guid.Empty;

    public ScriptJobListContext()
    {
        PropertyChanged += OnPropertyChanged;

        PreviousDaysStatisticsXAxis =
        [
            new Axis()
        ];

        PreviousDaysStatisticsYAxis =
        [
            new Axis
            {
                MinLimit = 0
            }
        ];

        DayStatisticsXAxis =
        [
            new Axis
            {
                Labels = null,
                ShowSeparatorLines = false,
                TextSize = 0
            }
        ];

        DayStatisticsYAxis =
        [
            new Axis
            {
                MinLimit = 0
            }
        ];
    }

    public required string DatabaseFile { get; set; }
    public PowerShellRunnerDayStatistics? DayStatistics { get; set; }
    public ISeries<int>[]? DayStatisticsSeries { get; set; }
    public Axis[] DayStatisticsXAxis { get; set; }
    public Axis[] DayStatisticsYAxis { get; set; }
    public bool FilterForLastRunError { get; set; }
    public bool FilterForLastRunSuccess { get; set; }
    public bool FilterForNotScheduled { get; set; }
    public bool FilterForRunning { get; set; }
    public bool FilterForScheduled { get; set; }
    public bool FilterForScheduleDisabled { get; set; }
    public required ObservableCollection<ScriptJobListListItem> Items { get; set; }
    public NotificationCatcher? JobDataNotificationsProcessor { get; set; }
    public required ColumnSortControlContext ListSort { get; set; }
    public List<PowerShellRunnerPreviousDayStatistics> PreviousDaysStatistics { get; set; } = [];
    public ISeries<int>[]? PreviousDaysStatisticsSeries { get; set; }
    public Axis[] PreviousDaysStatisticsXAxis { get; set; }
    public Axis[] PreviousDaysStatisticsYAxis { get; set; }
    public NotificationCatcher? RunDataNotificationsProcessor { get; set; }
    public ScriptJobListListItem? SelectedItem { get; set; }
    public List<ScriptJobListListItem> SelectedItems { get; set; } = [];
    public required StatusControlContext StatusContext { get; set; }
    public string? UserFilterText { get; set; }

    public static async Task<ScriptJobListContext> CreateInstance(StatusControlContext? statusContext,
        string databaseFile)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        var factoryStatusContext = await StatusControlContext.CreateInstance(statusContext);

        var dbId = await PowerShellRunnerDbQuery.DbId(databaseFile);

        factoryStatusContext.Progress("Starting to Set Up the Job List");

        var factoryContext = new ScriptJobListContext
        {
            StatusContext = factoryStatusContext,
            Items = [],
            DatabaseFile = databaseFile,
            ListSort = new ColumnSortControlContext
            {
                Items =
                [
                    new ColumnSortControlSortItem
                    {
                        DisplayName = "Name",
                        ColumnName = "DbEntry.Name",
                        Order = 1,
                        DefaultSortDirection = ListSortDirection.Ascending
                    },

                    new ColumnSortControlSortItem
                    {
                        DisplayName = "Next Run",
                        ColumnName = "NextRun",
                        DefaultSortDirection = ListSortDirection.Descending
                    }
                ]
            },
            _databaseFile = databaseFile,
            _dbId = dbId
        };

        await ThreadSwitcher.ResumeBackgroundAsync();

        factoryContext.BuildCommands();

        factoryStatusContext.Progress("Getting Job List Data");
        await factoryContext.RefreshList();

        factoryContext.JobDataNotificationsProcessor = new NotificationCatcher
        {
            JobDataNotification = factoryContext.ProcessJobDataUpdateNotification
        };

        factoryContext.JobDataNotificationsProcessor = new NotificationCatcher
        {
            RunDataNotification = factoryContext.ProcessRunDataUpdateNotification
        };

        factoryStatusContext.Progress("Updating Cron Information Part 1");

        factoryContext.UpdateCronExpressionInformation();

        factoryStatusContext.Progress("Updating Cron Information Part 2");

        _ = factoryContext.UpdateCronNextRun();

        factoryStatusContext.Progress("Sorting List");

        await ListContextSortHelpers.SortList(
            factoryContext.ListSort.SortDescriptions(), factoryContext.Items);

        factoryContext.ListSort.SortUpdated += (_, list) =>
            factoryContext.StatusContext.RunFireAndForgetNonBlockingTask(() =>
                ListContextSortHelpers.SortList(list, factoryContext.Items));

        return factoryContext;
    }

    [BlockingCommand]
    public async Task DeleteJob(ScriptJobListListItem? toDelete)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (toDelete == null)
        {
            await StatusContext.ToastWarning("Nothing Selected to Delete?");
            return;
        }

        await ThreadSwitcher.ResumeForegroundAsync();

        if ((await StatusContext.ShowMessageWithYesNoButton($"Delete Confirmation - Job: {toDelete.DbEntry.Name}",
                $"Deletes are permanent - this program does have a 'recycle bin' or save any information about a Deleted Job (all job run information will also be deleted!) - are you sure you want to delete {toDelete.DbEntry.Name}?"))
            .Equals("no", StringComparison.OrdinalIgnoreCase)) return;

        await ThreadSwitcher.ResumeBackgroundAsync();

        var db = await PowerShellRunnerDbContext.CreateInstance(_databaseFile);
        var currentItem = await db.ScriptJobs.SingleAsync(x => x.PersistentId == toDelete.DbEntry.PersistentId);
        var currentPersistentId = currentItem.PersistentId;

        var currentRuns = await db.ScriptJobRuns.Where(x => x.ScriptJobPersistentId == currentItem.PersistentId)
            .ToListAsync();
        db.ScriptJobRuns.RemoveRange(currentRuns);
        await db.SaveChangesAsync();

        Log.ForContext("JobPersistentId", currentItem.PersistentId)
            .ForContext(nameof(_dbId), _dbId)
            .ForContext(nameof(_databaseFile), _databaseFile)
            .ForContext(nameof(currentRuns), currentRuns.SafeObjectDump()).Information(
                "Deleting {0} ScriptJobRuns from '{1}' as part of Deleting the Job List GUI.", currentRuns.Count,
                currentItem.Name);

        foreach (var loopScriptJobs in currentRuns)
            DataNotifications.PublishRunDataNotification("Script Job Run List",
                DataNotifications.DataNotificationUpdateType.Delete, _dbId, loopScriptJobs.ScriptJobPersistentId,
                loopScriptJobs.PersistentId);

        db.ScriptJobs.Remove(currentItem);
        await db.SaveChangesAsync();

        Log.ForContext(nameof(_dbId), _dbId)
            .ForContext(nameof(_databaseFile), _databaseFile)
            .Information("Deleted Script Job {Name} - {PersistentId}", currentItem.Name, currentPersistentId);

        DataNotifications.PublishJobDataNotification("Script Job List",
            DataNotifications.DataNotificationUpdateType.Delete, _dbId, currentPersistentId);
    }

    [NonBlockingCommand]
    public async Task DiffLatestRuns(ScriptJobListListItem? toEdit)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (toEdit == null)
        {
            await StatusContext.ToastWarning("Nothing Selected?");
            return;
        }

        var db = await PowerShellRunnerDbContext.CreateInstance(_databaseFile);
        var topRun = db.ScriptJobRuns.Where(x => x.ScriptJobPersistentId == toEdit.DbEntry.PersistentId)
            .OrderByDescending(x => x.CompletedOnUtc)
            .FirstOrDefault();

        if (topRun == null)
        {
            await StatusContext.ToastWarning("No Runs to Compare?");
            return;
        }

        await ScriptJobRunOutputDiffWindow.CreateInstance(topRun.PersistentId, null, DatabaseFile);
    }

    [NonBlockingCommand]
    public async Task EditJob(ScriptJobListListItem? toEdit)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (toEdit == null)
        {
            await StatusContext.ToastWarning("Nothing Selected to Edit?");
            return;
        }

        await ThreadSwitcher.ResumeForegroundAsync();

        await ScriptJobEditorLauncher.CreateInstance(toEdit.DbEntry, DatabaseFile);
    }

    public async Task FilterList()
    {
        if (!Items.Any()) return;

        await ThreadSwitcher.ResumeForegroundAsync();

        var cleanedFilterText = UserFilterText.TrimNullToEmpty();

        var scheduledFilter = cleanedFilterText.Contains("!scheduled", StringComparison.OrdinalIgnoreCase) ||
                              FilterForScheduled;
        var notScheduledFilter = cleanedFilterText.Contains("!notScheduled", StringComparison.OrdinalIgnoreCase) ||
                                 FilterForNotScheduled;
        var scheduleDisabledFilter =
            cleanedFilterText.Contains("!scheduleDisabled", StringComparison.OrdinalIgnoreCase) ||
            FilterForScheduleDisabled;
        var lastRunErrorFilter = cleanedFilterText.Contains("!lastRunError", StringComparison.OrdinalIgnoreCase) ||
                                 FilterForLastRunError;
        var lastRunSuccessFilter = cleanedFilterText.Contains("!lastRunSuccess", StringComparison.OrdinalIgnoreCase) ||
                                   FilterForLastRunSuccess;
        var runningFilter = cleanedFilterText.Contains("!running", StringComparison.OrdinalIgnoreCase) ||
                            FilterForRunning;

        ((CollectionView)CollectionViewSource.GetDefaultView(Items)).Filter = o =>
        {
            if (o is not ScriptJobListListItem toFilter) return false;

            if (scheduledFilter)
                if (!toFilter.DbEntry.ScheduleEnabled || string.IsNullOrWhiteSpace(toFilter.DbEntry.CronExpression))
                    return false;
            if (notScheduledFilter)
                if (toFilter.DbEntry.ScheduleEnabled && !string.IsNullOrWhiteSpace(toFilter.DbEntry.CronExpression))
                    return false;
            if (scheduleDisabledFilter)
                if (toFilter.DbEntry.ScheduleEnabled || string.IsNullOrWhiteSpace(toFilter.DbEntry.CronExpression))
                    return false;
            if (lastRunErrorFilter)
            {
                if (!toFilter.Items.Any()) return false;
                var latestByStart = toFilter.Items.MaxBy(x => x.StartedOnUtc);
                if (latestByStart is null) return false;
                if (latestByStart.CompletedOnUtc is null) return false;
                if (!latestByStart.Errors) return false;
            }

            if (lastRunSuccessFilter)
            {
                if (!toFilter.Items.Any()) return false;
                var latestByStart = toFilter.Items.MaxBy(x => x.StartedOnUtc);
                if (latestByStart is null) return false;
                if (latestByStart.CompletedOnUtc is null) return false;
                if (latestByStart.Errors) return false;
            }

            if (runningFilter)
            {
                if (!toFilter.Items.Any()) return false;
                var latestByStart = toFilter.Items.MaxBy(x => x.StartedOnUtc);
                if (latestByStart is null) return false;
                if (latestByStart.CompletedOnUtc is not null) return false;
            }

            if (string.IsNullOrWhiteSpace(cleanedFilterText)) return true;

            return toFilter.DbEntry.Name.Contains(cleanedFilterText, StringComparison.OrdinalIgnoreCase)
                   || toFilter.DbEntry.PersistentId.ToString()
                       .Contains(cleanedFilterText, StringComparison.OrdinalIgnoreCase);
        };
    }

    [NonBlockingCommand]
    public async Task NewCsScriptJob()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var newJob = new ScriptJob
        {
            Name = "New Script Job",
            LastEditOn = DateTime.Now,
            ScriptType = ScriptKind.CsScript.ToString()
        };

        await ThreadSwitcher.ResumeForegroundAsync();

        await ScriptJobEditorLauncher.CreateInstance(newJob, DatabaseFile);
    }


    [NonBlockingCommand]
    public async Task NewPowerShellJob()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var newJob = new ScriptJob
        {
            Name = "New Script Job",
            LastEditOn = DateTime.Now,
            ScriptType = ScriptKind.PowerShell.ToString()
        };

        await ThreadSwitcher.ResumeForegroundAsync();

        await ScriptJobEditorLauncher.CreateInstance(newJob, DatabaseFile);
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.PropertyName)) return;
        if (e.PropertyName.Equals(nameof(UserFilterText)) ||
            e.PropertyName.StartsWith("FilterFor", StringComparison.InvariantCultureIgnoreCase))
            StatusContext.RunFireAndForgetNonBlockingTask(FilterList);
    }

    private async Task ProcessJobDataUpdateNotification(
        DataNotifications.InterProcessJobDataNotification interProcessUpdateNotification)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (interProcessUpdateNotification.DatabaseId != _dbId) return;

        if (interProcessUpdateNotification is
            {
                UpdateType: DataNotifications.DataNotificationUpdateType.Delete
            })

        {
            await ThreadSwitcher.ResumeForegroundAsync();

            var toRemove = Items.Where(x => x.DbEntry.PersistentId == interProcessUpdateNotification.JobPersistentId)
                .ToList();
            toRemove.ForEach(x => Items.Remove(x));
            StatusContext.RunFireAndForgetNonBlockingTask(RefreshDayStatistics);
            return;
        }

        if (interProcessUpdateNotification is
            {
                UpdateType: DataNotifications.DataNotificationUpdateType.Update
                or DataNotifications.DataNotificationUpdateType.New
            })
        {
            var listItem =
                Items.SingleOrDefault(x => x.DbEntry.PersistentId == interProcessUpdateNotification.JobPersistentId);

            //List items catch their own job update notifications - just handle new items.
            if (listItem != null) return;

            var db = await PowerShellRunnerDbContext.CreateInstance(_databaseFile);
            var dbItem =
                await db.ScriptJobs.SingleOrDefaultAsync(x =>
                    x.PersistentId == interProcessUpdateNotification.JobPersistentId);

            if (dbItem == null) return;

            var toAdd = await ScriptJobListListItem.CreateInstance(dbItem, this, _databaseFile);

            await ThreadSwitcher.ResumeForegroundAsync();

            Items.Add(toAdd);

            StatusContext.RunFireAndForgetNonBlockingTask(FilterList);
            StatusContext.RunFireAndForgetNonBlockingTask(RefreshDayStatistics);
        }
    }

    private async Task ProcessRunDataUpdateNotification(
        DataNotifications.InterProcessRunDataNotification interProcessUpdateNotification)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (interProcessUpdateNotification.DatabaseId != _dbId) return;

        await RefreshDayStatistics();
    }

    public async Task RefreshDayStatistics()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var frozenLocalToday = DateTime.Now.Date;

        var dayStatisticsPartial = await PowerShellRunnerDbQuery.DayStatistics(_databaseFile);
        dayStatisticsPartial.Scheduled = Items.Count(x => x.NextRun.Date == frozenLocalToday);

        DayStatistics = dayStatisticsPartial;

        DayStatisticsXAxis =
        [
            new Axis
            {
                Labels = frozenLocalToday.ToString("M/d").AsList(),
                ShowSeparatorLines = false
            }
        ];

        DayStatisticsSeries =
        [
            new ColumnSeries<int>
            {
                Name = "Success",
                Values = DayStatistics.Success.AsList(),
                MaxBarWidth = 20,
                Padding = 4,
                Fill = new SolidColorPaint(SKColors.RoyalBlue)
            },
            new ColumnSeries<int>
            {
                Name = "Error",
                Values = DayStatistics.Error.AsList(),
                MaxBarWidth = 20,
                Padding = 4,
                Fill = new SolidColorPaint(SKColors.Red)
            },
            new ColumnSeries<int>
            {
                Name = "Scheduled",
                Values = DayStatistics.Scheduled.AsList(),
                MaxBarWidth = 20,
                Padding = 4,
                Fill = new SolidColorPaint(SKColors.DarkGreen)
            },
            new ColumnSeries<int>
            {
                Name = "Running",
                Values = DayStatistics.Running.AsList(),
                MaxBarWidth = 20,
                Padding = 4,
                Fill = new SolidColorPaint(SKColors.LawnGreen)
            }
        ];
    }


    [BlockingCommand]
    public async Task RefreshList()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var db = await PowerShellRunnerDbContext.CreateInstance(_databaseFile);

        var jobs = await db.ScriptJobs.ToListAsync();

        await ThreadSwitcher.ResumeForegroundAsync();

        Items.Clear();

        foreach (var x in jobs) Items.Add(await ScriptJobListListItem.CreateInstance(x, this, _databaseFile));

        await FilterList();

        UpdateCronExpressionInformation();

        await ListContextSortHelpers.SortList(
            ListSort.SortDescriptions(), Items);

        await RefreshDayStatistics();
        await RefreshPreviousDayStatistics();
    }

    public async Task RefreshPreviousDayStatistics()
    {
        PreviousDaysStatistics =
            await PowerShellRunnerDbQuery.PreviousDayStatistics(_databaseFile, 20, PreviousDaysStatistics);

        PreviousDaysStatisticsXAxis =
        [
            new Axis
            {
                Labels = PreviousDaysStatistics.Select(x => x.Day.ToString("M/d")).ToList()
            }
        ];

        PreviousDaysStatisticsSeries =
        [
            new ColumnSeries<int>
            {
                Name = "Success",
                Values = PreviousDaysStatistics.Select(x => x.Success).ToList(),
                MaxBarWidth = 16,
                Padding = 4,
                Fill = new SolidColorPaint(SKColors.RoyalBlue)
            },
            new ColumnSeries<int>
            {
                Name = "Error",
                Values = PreviousDaysStatistics.Select(x => x.Error).ToList(),
                MaxBarWidth = 16,
                Padding = 4,
                Fill = new SolidColorPaint(SKColors.Red)
            }
        ];
    }

    [NonBlockingCommand]
    public async Task RunJob(ScriptJobListListItem? toRun)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (toRun == null)
        {
            await StatusContext.ToastWarning("Nothing Selected to Run?");
            return;
        }

        await PowerShellRunner.ExecuteJob(toRun.DbEntry.PersistentId, toRun.DbEntry.AllowSimultaneousRuns, DatabaseFile,
            "Run From PowerShell Runner Gui");
    }

    [NonBlockingCommand]
    public async Task RunJobsFromSelectedItems()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var currentSelection = SelectedItems;

        if (!currentSelection.Any())
        {
            await StatusContext.ToastWarning("Nothing Selected to Run?");
            return;
        }

        foreach (var loopSelected in currentSelection)
            await PowerShellRunner.ExecuteJob(loopSelected.DbEntry.PersistentId,
                loopSelected.DbEntry.AllowSimultaneousRuns, DatabaseFile,
                "Run From PowerShell Runner Gui");
    }

    [NonBlockingCommand]
    public async Task RunWithProgressWindow(ScriptJobListListItem? toRun)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (toRun == null)
        {
            await StatusContext.ToastWarning("Nothing Selected to Run?");
            return;
        }

        await PowerShellRunner.ExecuteJob(toRun.DbEntry.PersistentId, toRun.DbEntry.AllowSimultaneousRuns, DatabaseFile,
            "Run From PowerShell Runner Gui",
            async run =>
            {
                await ScriptProgressWindow.CreateInstance(run.ScriptJobPersistentId.AsList(), run.PersistentId.AsList(),
                    _databaseFile);
            });
    }

    private void UpdateCronExpressionInformation()
    {
        var jobs = Items.ToList();

        foreach (var loopJobs in jobs)
        {
            if (!loopJobs.DbEntry.ScheduleEnabled || string.IsNullOrWhiteSpace(loopJobs.DbEntry.CronExpression))
            {
                loopJobs.NextRun = DateTime.MaxValue;
                loopJobs.CronDescription = string.Empty;
                continue;
            }

            try
            {
                var expression = CronExpression.Parse(loopJobs.DbEntry.CronExpression);
                var nextRun = expression.GetNextOccurrence(DateTimeOffset.Now, TimeZoneInfo.Local);
                if (nextRun != null) loopJobs.NextRun = nextRun.Value.LocalDateTime;
                loopJobs.CronDescription = ExpressionDescriptor.GetDescription(loopJobs.DbEntry.CronExpression);
            }
            catch (Exception)
            {
                loopJobs.NextRun = DateTime.MaxValue;
                loopJobs.CronDescription = string.Empty;
            }
        }
    }

    private async Task UpdateCronNextRun()
    {
        try
        {
            while (await _cronNextTimer.WaitForNextTickAsync())
            {
                if (!StatusContext.BlockUi)
                    UpdateCronExpressionInformation();
                if (DayStatistics is not null && DayStatistics.Day != DateTime.Now.Date)
                {
                    StatusContext.RunFireAndForgetNonBlockingTask(RefreshDayStatistics);
                    StatusContext.RunFireAndForgetNonBlockingTask(RefreshPreviousDayStatistics);
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    [NonBlockingCommand]
    public async Task ViewJobRun(ScriptJobRun? toShow)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (toShow == null)
        {
            await StatusContext.ToastWarning("Nothing Selected?");
            return;
        }

        await ScriptJobRunViewerWindow.CreateInstance(toShow.PersistentId, DatabaseFile);
    }

    [NonBlockingCommand]
    public async Task ViewLatestJobRun(ScriptJobListListItem? toShow)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (toShow == null)
        {
            await StatusContext.ToastWarning("Nothing Selected?");
            return;
        }

        var db = await PowerShellRunnerDbContext.CreateInstance(_databaseFile);
        var topRun = db.ScriptJobRuns.Where(x => x.ScriptJobPersistentId == toShow.DbEntry.PersistentId)
            .OrderByDescending(x => x.CompletedOnUtc)
            .FirstOrDefault();

        if (topRun == null)
        {
            await StatusContext.ToastWarning("No Runs to Compare?");
            return;
        }

        await ScriptJobRunViewerWindow.CreateInstance(topRun.PersistentId, DatabaseFile);
    }

    [NonBlockingCommand]
    public async Task ViewProgressWindow(ScriptJobListListItem? job)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (job == null)
        {
            await StatusContext.ToastWarning("Nothing Selected?");
            return;
        }

        await ScriptProgressWindow.CreateInstance(job.DbEntry.PersistentId.AsList(), [],
            _databaseFile);
    }

    [NonBlockingCommand]
    public async Task ViewRunList(ScriptJobListListItem? toShow)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (toShow == null)
        {
            await StatusContext.ToastWarning("Nothing Selected?");
            return;
        }

        await ScriptJobRunListWindow.CreateInstance(toShow.DbEntry.PersistentId.AsList(), DatabaseFile);
    }

    [NonBlockingCommand]
    public async Task ViewRunListForAllItems()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (!Items.Any())
        {
            await StatusContext.ToastWarning("Nothing to Show?");
            return;
        }

        await ScriptJobRunListWindow.CreateInstance(Items.Select(x => x.DbEntry.PersistentId).ToList(),
            DatabaseFile);
    }

    [NonBlockingCommand]
    public async Task ViewRunListFromSelectedItems()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (!SelectedItems.Any())
        {
            await StatusContext.ToastWarning("Nothing Selected?");
            return;
        }

        await ScriptJobRunListWindow.CreateInstance(SelectedItems.Select(x => x.DbEntry.PersistentId).ToList(),
            DatabaseFile);
    }

    [NonBlockingCommand]
    public async Task ViewScript(ScriptJobListListItem? toView)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (toView == null)
        {
            await StatusContext.ToastWarning("Nothing Selected to View?");
            return;
        }

        if (toView.DbEntry.ScriptType == ScriptKind.CsScript.ToString())
            await CsScriptViewWindow.CreateInstance(toView.DbEntry.PersistentId, DatabaseFile);
        else
            await ScriptViewWindow.CreateInstance(toView.DbEntry.PersistentId, DatabaseFile);
    }
}