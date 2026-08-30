using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Data;
using GongSolutions.Wpf.DragDrop;
using Microsoft.EntityFrameworkCore;
using PointlessWaymarks.CmsData;
using PointlessWaymarks.CmsData.Database;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.CmsWpfControls.ContentList;
using PointlessWaymarks.CmsWpfControls.WorkoutItemEditor;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.ColumnSort;
using PointlessWaymarks.WpfCommon.Status;
using PointlessWaymarks.WpfCommon.Utility;
using Serilog;
using TinyIpc.Messaging;

namespace PointlessWaymarks.CmsWpfControls.WorkoutItemsList;

[NotifyPropertyChanged]
[GenerateStatusCommands]
public partial class WorkoutItemsListContext : IDropTarget
{
    private WorkoutItemsListContext(ObservableCollection<WorkoutItemsListListItem> items,
        StatusControlContext statusContext, ContentListSelected<WorkoutItemsListListItem> factoryListSelection,
        bool loadInBackground = true)
    {
        Items = items;
        StatusContext = statusContext;
        CommonCommands = new CmsCommonCommands(StatusContext);
        ListSelection = factoryListSelection;

        BuildCommands();

        DataNotificationsProcessor = new DataNotificationsWorkQueue { Processor = DataNotificationReceived };

        ListSort = new ColumnSortControlContext
        {
            Items =
            [
                new ColumnSortControlSortItem
                {
                    DisplayName = "Workout Date",
                    ColumnName = "DbEntry.WorkoutOn",
                    DefaultSortDirection = ListSortDirection.Descending,
                    Order = 1
                },
                new ColumnSortControlSortItem
                {
                    DisplayName = "Workout Type",
                    ColumnName = "DbEntry.WorkoutType",
                    DefaultSortDirection = ListSortDirection.Ascending
                },
                new ColumnSortControlSortItem
                {
                    DisplayName = "Duration",
                    ColumnName = "DbEntry.DurationMinutes",
                    DefaultSortDirection = ListSortDirection.Descending
                },
                new ColumnSortControlSortItem
                {
                    DisplayName = "Distance",
                    ColumnName = "DbEntry.DistanceMiles",
                    DefaultSortDirection = ListSortDirection.Descending
                },
                new ColumnSortControlSortItem
                {
                    DisplayName = "Climb",
                    ColumnName = "DbEntry.ClimbFeet",
                    DefaultSortDirection = ListSortDirection.Descending
                },
                new ColumnSortControlSortItem
                {
                    DisplayName = "Descent",
                    ColumnName = "DbEntry.DescentFeet",
                    DefaultSortDirection = ListSortDirection.Descending
                },
                new ColumnSortControlSortItem
                {
                    DisplayName = "Calories",
                    ColumnName = "DbEntry.Calories",
                    DefaultSortDirection = ListSortDirection.Descending
                },
                new ColumnSortControlSortItem
                {
                    DisplayName = "Workout By",
                    ColumnName = "DbEntry.WorkoutBy",
                    DefaultSortDirection = ListSortDirection.Ascending
                }
            ]
        };

        ListSort.SortUpdated += (_, list) =>
            StatusContext.RunFireAndForgetNonBlockingTask(() => ListContextSortHelpers.SortList(list, Items));

        PropertyChanged += OnPropertyChanged;

        if (loadInBackground) StatusContext.RunFireAndForgetBlockingTask(LoadData);
    }

    public CmsCommonCommands CommonCommands { get; set; }
    public DataNotificationsWorkQueue DataNotificationsProcessor { get; set; }
    public ObservableCollection<WorkoutItemsListListItem> Items { get; set; }
    public ContentListSelected<WorkoutItemsListListItem> ListSelection { get; set; }
    public ColumnSortControlContext ListSort { get; set; }
    public StatusControlContext StatusContext { get; set; }
    public string UserFilterText { get; set; } = string.Empty;

    public static async Task<WorkoutItemsListContext> CreateInstance(StatusControlContext? statusContext)
    {
        var factoryStatusContext = await StatusControlContext.CreateInstance(statusContext);

        var factoryItems = new ObservableCollection<WorkoutItemsListListItem>();
        var factoryListSelection =
            await ContentListSelected<WorkoutItemsListListItem>.CreateInstance(factoryStatusContext);

        return new WorkoutItemsListContext(factoryItems, factoryStatusContext, factoryListSelection);
    }

    private async Task DataNotificationReceived(TinyMessageReceivedEventArgs e)
    {
        var translatedMessage = DataNotifications.TranslateDataNotification(e.Message.ToString());

        if (translatedMessage.HasError)
        {
            Log.Error("Data Notification Failure. Error Note {0}. Status Control Context Id {1}",
                translatedMessage.ErrorNote, StatusContext.StatusControlContextId);
            return;
        }

        if (!translatedMessage.ContentIds.Any() ||
            translatedMessage.ContentType != DataNotificationContentType.Workout) return;

        var existingListItemsMatchingNotification = new List<WorkoutItemsListListItem>();

        foreach (var loopItem in Items)
        {
            var id = loopItem.DbEntry.ContentId;
            if (translatedMessage.ContentIds.Contains(id))
                existingListItemsMatchingNotification.Add(loopItem);
        }

        await ThreadSwitcher.ResumeBackgroundAsync();

        if (translatedMessage.UpdateType == DataNotificationUpdateType.Delete)
        {
            await ThreadSwitcher.ResumeForegroundAsync();
            existingListItemsMatchingNotification.ForEach(x => Items.Remove(x));
            return;
        }

        var context = await Db.Context();
        var dbItems = await context.WorkoutItems.Where(x => translatedMessage.ContentIds.Contains(x.ContentId))
            .ToListAsync();

        foreach (var loopItem in dbItems)
        {
            await ThreadSwitcher.ResumeBackgroundAsync();

            var existingItems = existingListItemsMatchingNotification
                .Where(x => x.DbEntry.ContentId == loopItem.ContentId).ToList();

            if (existingItems.Count < 1)
            {
                await ThreadSwitcher.ResumeForegroundAsync();
                Items.Add(WorkoutItemsListListItem.CreateInstance(loopItem));
                continue;
            }

            if (existingItems.Count > 1)
            {
                await ThreadSwitcher.ResumeForegroundAsync();

                foreach (var loopDelete in existingItems.Skip(1).ToList()) Items.Remove(loopDelete);
            }

            var existingItem = existingItems.First();

            existingItem.DbEntry = loopItem;
        }

        await ListContextSortHelpers.SortList(ListSort.SortDescriptions(), Items);
        await FilterList();
    }

    [NonBlockingCommand]
    public async Task Delete(WorkoutItem? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (content == null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        if (content.Id < 1)
        {
            await StatusContext.ToastError("Workout Item is not saved - Skipping?");
            return;
        }

        var context = await Db.Context();
        var toDelete = await context.WorkoutItems.FirstOrDefaultAsync(x => x.ContentId == content.ContentId);
        if (toDelete != null)
        {
            context.WorkoutItems.Remove(toDelete);
            await context.SaveChangesAsync();
            DataNotifications.PublishDataNotification("Workout Items List", DataNotificationContentType.Workout,
                DataNotificationUpdateType.Delete, [content.ContentId]);
            await StatusContext.ToastSuccess($"Deleted Workout {content.WorkoutType} on {content.WorkoutOn:d}");
        }
    }

    [NonBlockingCommand]
    public async Task DeleteSelected()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var selected = ListSelection.SelectedItems;

        if (!selected.Any())
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        var context = await Db.Context();
        var idsToDelete = selected.Select(x => x.DbEntry.ContentId).ToList();
        var toDelete = await context.WorkoutItems.Where(x => idsToDelete.Contains(x.ContentId)).ToListAsync();

        if (toDelete.Any())
        {
            context.WorkoutItems.RemoveRange(toDelete);
            await context.SaveChangesAsync();
            DataNotifications.PublishDataNotification("Workout Items List", DataNotificationContentType.Workout,
                DataNotificationUpdateType.Delete, idsToDelete);
            await StatusContext.ToastSuccess($"Deleted {toDelete.Count} Workouts");
        }
    }

    [NonBlockingCommand]
    public async Task Edit(WorkoutItem? content)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (content == null) return;

        var context = await Db.Context();

        var refreshedData = await context.WorkoutItems.SingleOrDefaultAsync(x => x.ContentId == content.ContentId);

        if (refreshedData == null)
        {
            await StatusContext.ToastError(
                $"Workout on {content.WorkoutOn:d} is no longer in the database? Cannot edit.");
            return;
        }

        var newContentWindow = await WorkoutItemEditorWindow.CreateInstance(refreshedData);

        await WindowInitialPositionHelpers.PositionWindowAndShowOnUiThread(newContentWindow);
    }

    [NonBlockingCommand]
    public async Task EditSelected()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var selected = ListSelection.Selected;

        if (selected == null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        await Edit(selected.DbEntry);
    }

    private async Task FilterList()
    {
        if (!Items.Any()) return;

        await ThreadSwitcher.ResumeForegroundAsync();

        if (string.IsNullOrWhiteSpace(UserFilterText))
        {
            ((CollectionView)CollectionViewSource.GetDefaultView(Items)).Filter = _ => true;
            return;
        }

        var cleanedFilter = UserFilterText.Trim().ToUpper();

        ((CollectionView)CollectionViewSource.GetDefaultView(Items)).Filter = o =>
        {
            if (o is not WorkoutItemsListListItem listItem) return false;

            return (listItem.DbEntry.WorkoutType?.ToUpper().Contains(cleanedFilter) ?? false) ||
                   (listItem.DbEntry.WorkoutBy?.ToUpper().Contains(cleanedFilter) ?? false) ||
                   (listItem.DbEntry.Note?.ToUpper().Contains(cleanedFilter) ?? false) ||
                   listItem.DbEntry.WorkoutOn.ToString("d").ToUpper().Contains(cleanedFilter) ||
                   listItem.DbEntry.DurationMinutes.ToString().Contains(cleanedFilter);
        };
    }

    [BlockingCommand]
    public async Task LoadData()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        DataNotifications.NewDataNotificationChannel().MessageReceived -= OnDataNotificationReceived;

        var context = await Db.Context();

        var allDbItems = await context.WorkoutItems.OrderByDescending(x => x.WorkoutOn).ToListAsync();

        var toLoad = allDbItems.Select(WorkoutItemsListListItem.CreateInstance).ToList();

        await ThreadSwitcher.ResumeForegroundAsync();

        Items.Clear();

        toLoad.ForEach(x => Items.Add(x));

        await ListContextSortHelpers.SortList(ListSort.SortDescriptions(), Items);
        await FilterList();

        DataNotifications.NewDataNotificationChannel().MessageReceived += OnDataNotificationReceived;
    }

    [NonBlockingCommand]
    public async Task NewWorkoutItem()
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        var newWindow = await WorkoutItemEditorWindow.CreateInstance(null, true);

        await WindowInitialPositionHelpers.PositionWindowAndShowOnUiThread(newWindow);
    }

    [BlockingCommand]
    public async Task NewWorkoutItemFromFiles(CancellationToken cancellationToken)
    {
        await CommonCommands.NewWorkoutContentFromFiles(cancellationToken);
    }

    [BlockingCommand]
    public async Task NewWorkoutItemFromFilesWithAutosave(CancellationToken cancellationToken)
    {
        await CommonCommands.NewWorkoutContentFromFilesWithAutosave(cancellationToken);
    }

    public WorkoutItemsListListItem? SelectedListItem()
    {
        return ListSelection.Selected;
    }

    public List<WorkoutItemsListListItem> SelectedListItems()
    {
        return ListSelection.SelectedItems.ToList();
    }

    [BlockingCommand]
    public async Task WorkoutActivityLogMonthlyStatsWindowForAllWorkoutContent()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var db = await Db.Context();

        var allWorkouts =
            await db.WorkoutItems.Select(x => x.ContentId).ToListAsync();

        var window =
            await WorkoutActivityLogMonthlySummaryWindow.CreateInstance(allWorkouts);

        await window.PositionWindowAndShowOnUiThread();
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task WorkoutActivityLogMonthlyStatsWindowForSelected()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var frozenSelected = SelectedListItems();

        var window =
            await WorkoutActivityLogMonthlySummaryWindow.CreateInstance(frozenSelected.Select(x => x.DbEntry.ContentId)
                .ToList());

        await window.PositionWindowAndShowOnUiThread();
    }

    public void DragOver(IDropInfo dropInfo)
    {
        var files = DragAndDropFilesHelper.DroppedFileNames(dropInfo, true, [".fit"]);
        dropInfo.Effects = files.Any() ? DragDropEffects.Copy : DragDropEffects.None;
    }

    public void Drop(IDropInfo dropInfo)
    {
        var files = DragAndDropFilesHelper.DroppedFiles(dropInfo, FileLocationTools.TempStorageDirectory(), true, [".fit"]);
        if (files.Any())
        {
            StatusContext.RunBlockingTask(async () => await TryOpenEditorsForDroppedFiles(files));
        }
    }

    private void OnDataNotificationReceived(object? sender, TinyMessageReceivedEventArgs e)
    {
        DataNotificationsProcessor.Enqueue(e);
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.PropertyName)) return;

        if (e.PropertyName == nameof(UserFilterText))
            StatusContext.RunFireAndForgetNonBlockingTask(FilterList);
    }

    private async Task TryOpenEditorsForDroppedFiles(List<string> files)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (files.Count > 10)
        {
            var manyFilesMessage = $"""
                                    There are {files.Count} Files to Import - do you really want to open editors for all of these files?

                                    {string.Join($"{Environment.NewLine}{Environment.NewLine}", files)}
                                    """;
            if ((await StatusContext.ShowMessageWithYesNoButton("Import Files?", manyFilesMessage)).Equals("no",
                    StringComparison.OrdinalIgnoreCase)) return;
        }

        var fileInfos = files.Select(x => new FileInfo(x)).ToList();
        await CmsCommonCommands.NewWorkoutContentFromFilesBase(fileInfos, false, CancellationToken.None, StatusContext,
            null);
    }
}
