using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using DynamicData;
using DynamicData.Binding;
using Microsoft.EntityFrameworkCore;
using PointlessWaymarks.AvaloniaCommon;
using PointlessWaymarks.AvaloniaCommon.ColumnSort;
using PointlessWaymarks.AvaloniaCommon.Status;
using PointlessWaymarks.AvaloniaCommon.Utility;
using PointlessWaymarks.AvaloniaLlamaAspects;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.FeedReaderData;
using Serilog;
using TinyIpc.Messaging;

namespace PointlessWaymarks.FeedReaderAvalonia.Controls;

[NotifyPropertyChanged]
[GenerateStatusCommands]
public partial class FeedListContext {

    public required FeedQueries ContextDb { get; init; }
    public DataNotificationsWorkQueue? DataNotificationsProcessor { get; set; }
    public required ColumnSortControlContext ListSort { get; init; }
    public FeedListListItem? SelectedItem { get; set; }
    public List<FeedListListItem> SelectedItems { get; set; } = [];
    public string UserAddFeedInput { get; set; } = string.Empty;
    public string UserFilterText { get; set; } = string.Empty;

    // Read-only observable collection for UI binding
    public required ReadOnlyObservableCollection<FeedListListItem> Items { get; init; }


    private readonly SourceList<FeedListListItem> _sourceItems = new();
    private readonly IDisposable _dynamicDataConnection;
    // Use BehaviorSubject to emit filter predicates as an Observable
    private readonly BehaviorSubject<Func<FeedListListItem, bool>> _filterPredicate =
        new(item => true); // Start with a filter that shows everything

    public FeedListContext()
    {
        // Set up DynamicData transformations with observable filter
        _dynamicDataConnection = _sourceItems.Connect()
            // This overload takes an Observable of filter predicates
            .Filter(_filterPredicate)
            .Sort(SortExpressionComparer<FeedListListItem>.Ascending(x => x.DbReaderFeed.Name))
            .Bind(out var items)
            .Subscribe();

        Items = items;
    }

    public FeedListListItem? SelectedListItem()
    {
        return SelectedItem;
    }

    public List<FeedListListItem> SelectedListItems()
    {
        return SelectedItems;
    }

    public required StatusControlContext StatusContext { get; set; }

    public async Task AddNewFeeds()
    {
        var existingItemIds = _sourceItems.Items.Select(x => x.DbReaderFeed.PersistentId).ToList();

        var db = await ContextDb.GetInstance();

        var newItems = (await db.Feeds.Where(x => !existingItemIds.Contains(x.PersistentId)).ToListAsync())
            .Select(x => new FeedListListItem { DbReaderFeed = x })
            .ToList();

        var feedCounts = await db.FeedItems.GroupBy(x => x.FeedPersistentId)
            .Select(x => new { FeedPersistentId = x.Key, AllFeedItemsCount = x.Count() }).ToListAsync();

        var unReadFeedCounts = await db.FeedItems.Where(x => !x.MarkedRead).GroupBy(x => x.FeedPersistentId)
            .Select(x => new { FeedPersistentId = x.Key, UnreadItemsCount = x.Count() }).ToListAsync();

        await ThreadSwitcher.ResumeForegroundAsync();

        foreach (var loopItem in newItems)
        {
            loopItem.ItemsCount = feedCounts
                .SingleOrDefault(x => x.FeedPersistentId == loopItem.DbReaderFeed.PersistentId)
                ?.AllFeedItemsCount ?? 0;
            loopItem.UnreadItemsCount = unReadFeedCounts
                .SingleOrDefault(x => x.FeedPersistentId == loopItem.DbReaderFeed.PersistentId)?.UnreadItemsCount ?? 0;
        }

        if (newItems.Any())
        {
            _sourceItems.AddRange(newItems);
            await ApplySorting();
        }
    }

    [BlockingCommand]
    public async Task ArchiveSelectedFeed()
    {
        if (SelectedItem == null)
        {
            await StatusContext.ToastWarning("Nothing Selected?");
            return;
        }

        await ContextDb.ArchiveFeed(SelectedItem.DbReaderFeed.PersistentId, StatusContext.ProgressTracker());
    }

    public static async Task<FeedListContext> CreateInstance(StatusControlContext statusContext, string dbFile)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        await ThreadSwitcher.ResumeBackgroundAsync();

        var feedQueries = new FeedQueries { DbFileFullName = dbFile };

        var newContext = new FeedListContext
        {
            StatusContext = statusContext,
            ContextDb = feedQueries,
            Items = new ReadOnlyObservableCollection<FeedListListItem>([]),
            ListSort = new ColumnSortControlContext
            {
                Items =
                [
                    new ColumnSortControlSortItem
                    {
                        DisplayName = "Feed Name",
                        ColumnName = "DbReaderFeed.Name",
                        Order = 1,
                        DefaultSortDirection = ListSortDirection.Ascending
                    },

                    new ColumnSortControlSortItem
                    {
                        DisplayName = "Unread Count",
                        ColumnName = "UnreadItemsCount",
                        DefaultSortDirection = ListSortDirection.Descending
                    },

                    new ColumnSortControlSortItem
                    {
                        DisplayName = "Last Successful Update",
                        ColumnName = "DbReaderFeed.LastSuccessfulUpdate",
                        DefaultSortDirection = ListSortDirection.Descending
                    },

                    new ColumnSortControlSortItem
                    {
                        DisplayName = "URL",
                        ColumnName = "DbReaderFeed.Url",
                        DefaultSortDirection = ListSortDirection.Ascending
                    }
                ]
            }
        };

        await newContext.Setup();

        return newContext;
    }

    private async Task DataNotificationReceived(TinyMessageReceivedEventArgs eventArgs)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var translatedMessage = DataNotifications.TranslateDataNotification(eventArgs.Message.ToString());

        var toRun = translatedMessage.Match(ProcessDataUpdateNotification,
            x =>
            {
                Log.Error("Data Notification Failure. Error Note {0}. Status Control Context Id {1}", x.ErrorMessage,
                    StatusContext.StatusControlContextId);
                return Task.CompletedTask;
            }
        );

        if (toRun is not null) await toRun;
    }

    [BlockingCommand]
    public async Task ExportSelectedUrlsToTextFile()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var feedsToExport = SelectedItems.Any() ? SelectedItems : Items.ToList();

        var urls = feedsToExport.Select(x => x.DbReaderFeed.Url).ToList();

        var urlListText = string.Join(Environment.NewLine, urls);

        await ThreadSwitcher.ResumeForegroundAsync();

        // Get top level from the current control. Alternatively, you can use Window reference instead.
        var desktopLifetime =
            (IClassicDesktopStyleApplicationLifetime)Avalonia.Application.Current!.ApplicationLifetime!;

        // Start async operation to open the dialog.
        var file = await desktopLifetime.MainWindow!.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Feed URLs",
            FileTypeChoices = [FilePickerFileTypes.TextPlain],
            SuggestedStartLocation = await desktopLifetime.MainWindow!.StorageProvider.TryGetFolderFromPathAsync(FeedReaderGuiSettingTools.GetLastDirectory().FullName),
        });

        if (file is not null)
        {
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(urlListText);
        }
    }

    [NonBlockingCommand]
    public async Task FeedEditorForFeed(FeedListListItem listItem)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        var window = await FeedEditorWindow.CreateInstance(listItem.DbReaderFeed, ContextDb.DbFileFullName);
        window.PositionWindowAndShow();
    }

    [NonBlockingCommand]
    [StopAndWarnIfNoSelectedListItem]
    public async Task FeedEditorForSelectedItem()
    {
        await FeedEditorForFeed(SelectedListItem()!);
    }

    private async Task FilterList()
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        if (string.IsNullOrWhiteSpace(UserFilterText))
        {
            // Emit a filter predicate that accepts everything
            _filterPredicate.OnNext(_ => true);
        }
        else
        {
            var cleanedFilterText = UserFilterText.Trim();

            // Emit a new filter predicate with the current search criteria
            _filterPredicate.OnNext(item =>
                item.DbReaderFeed.Name.Contains(cleanedFilterText, StringComparison.OrdinalIgnoreCase) ||
                item.DbReaderFeed.Tags.Contains(cleanedFilterText, StringComparison.OrdinalIgnoreCase) ||
                item.DbReaderFeed.Note.Contains(cleanedFilterText, StringComparison.OrdinalIgnoreCase) ||
                item.DbReaderFeed.Url.Contains(cleanedFilterText, StringComparison.OrdinalIgnoreCase));
        }
    }

    private async Task ApplySorting()
    {
        var sortDescriptions = ListSort.SortDescriptions();
        if (!sortDescriptions.Any())
            return;

        // Build a comparer based on the sort descriptions
        IComparer<FeedListListItem> comparer = new SortDescriptionComparer<FeedListListItem>(sortDescriptions);

        // Get all items to sort
        var items = _sourceItems.Items.OrderBy(x => x, comparer).ToList();

        // Re-add the sorted items
        _sourceItems.Edit(innerList =>
        {
            innerList.Clear();
            innerList.AddRange(items);
        });
    }

    [BlockingCommand]
    public async Task ImportUrlsFromTextFile()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        await ThreadSwitcher.ResumeForegroundAsync();

        // Get access to the storage provider from the application
        var desktopLifetime =
            (IClassicDesktopStyleApplicationLifetime)Avalonia.Application.Current!.ApplicationLifetime!;

        // Configure and show the file picker
        var files = await desktopLifetime.MainWindow!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Link File",
            FileTypeFilter =
            [
                new FilePickerFileType("Text Files")
                {
                    Patterns = ["*.txt"],
                    MimeTypes = ["text/plain"]
                }
            ],
            AllowMultiple = false,
            SuggestedStartLocation = await desktopLifetime.MainWindow!.StorageProvider
                .TryGetFolderFromPathAsync(FeedReaderGuiSettingTools.GetLastDirectory().FullName)
        });

        // Check if a file was selected
        if (files.Count == 0) return;

        var file = files[0];

        await ThreadSwitcher.ResumeBackgroundAsync();

        // Read the file contents
        string urlTextBlock;
        await using (var stream = await file.OpenReadAsync())
        using (var reader = new StreamReader(stream))
        {
            urlTextBlock = await reader.ReadToEndAsync();
        }

        // Process URLs - this part remains mostly unchanged
        var urls = Regex.Split(urlTextBlock, "\r\n|\r|\n").ToList();
        urls.RemoveAll(string.IsNullOrWhiteSpace);

        var db = await ContextDb.GetInstance();
        var allUrls = await db.Feeds.Select(x => x.Url).AsNoTracking().ToListAsync();

        urls.RemoveAll(x => allUrls.Contains(x));

        if (!urls.Any())
        {
            await StatusContext.ToastError("No New Links Found?");
            return;
        }

        foreach (var loopUrl in urls)
        {
            var addResult = await ContextDb.TryAddFeed(loopUrl, StatusContext.ProgressTracker());
            addResult.Switch(_ => StatusContext.ToastSuccess($"Added {loopUrl}"),
                x => StatusContext.ToastError(x.Value));
        }
    }

    private void OnDataNotificationReceived(object? sender, TinyMessageReceivedEventArgs e)
    {
        DataNotificationsProcessor?.Enqueue(e);
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.PropertyName)) return;

        if (e.PropertyName == nameof(UserFilterText))
            StatusContext.RunFireAndForgetNonBlockingTask(FilterList);
    }

    private async Task ProcessDataUpdateNotification(InterProcessDataNotification interProcessUpdateNotification)
    {
        if (interProcessUpdateNotification.ContentType == DataNotificationContentType.Feed)
        {
            if (interProcessUpdateNotification.UpdateType == DataNotificationUpdateType.Delete)
            {
                await ThreadSwitcher.ResumeForegroundAsync();

                _sourceItems.Edit(innerList =>
                {
                    var toRemove = innerList
                        .Where(x => interProcessUpdateNotification.ContentIds.Contains(x.DbReaderFeed.PersistentId))
                        .ToList();
                    foreach (var item in toRemove)
                    {
                        innerList.Remove(item);
                    }
                });

                return;
            }

            if (interProcessUpdateNotification.UpdateType is DataNotificationUpdateType.Update)
                await UpdateFeedListItems(interProcessUpdateNotification.ContentIds);
            if (interProcessUpdateNotification.UpdateType is DataNotificationUpdateType.New)
                await AddNewFeeds();
        }

        if (interProcessUpdateNotification.ContentType == DataNotificationContentType.FeedItem)
            StatusContext.RunFireAndForgetNonBlockingTask(async () =>
                await UpdateReadCount(interProcessUpdateNotification.ContentIds));
    }

    public async Task Setup()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        BuildCommands();

        await AddNewFeeds();

        ListSort.SortUpdated += (_, _) =>
            StatusContext.RunFireAndForgetNonBlockingTask(ApplySorting);

        PropertyChanged += OnPropertyChanged;

        DataNotificationsProcessor = new DataNotificationsWorkQueue { Processor = DataNotificationReceived };
        DataNotifications.NewDataNotificationChannel().MessageReceived += OnDataNotificationReceived;
    }

    private async Task UpdateFeedListItems(List<Guid> toUpdate)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var db = await ContextDb.GetInstance();

        foreach (var loopContentIds in toUpdate)
        {
            await ThreadSwitcher.ResumeBackgroundAsync();

            var dbFeedItem = db.Feeds.SingleOrDefault(x => x.PersistentId == loopContentIds);

            //If there is no database item remove it if it exists in the items and continue
            if (dbFeedItem == null)
            {
                await ThreadSwitcher.ResumeForegroundAsync();

                _sourceItems.Edit(innerList =>
                {
                    var itemToRemove = innerList.SingleOrDefault(x => x.DbReaderFeed.PersistentId == loopContentIds);
                    if (itemToRemove != null)
                    {
                        innerList.Remove(itemToRemove);
                    }
                });

                continue;
            }

            await ThreadSwitcher.ResumeForegroundAsync();

            // Find the existing item
            var existingItem = _sourceItems.Items.SingleOrDefault(x => x.DbReaderFeed.PersistentId == loopContentIds);

            if (existingItem != null)
            {
                // Update existing item
                _sourceItems.Edit(innerList =>
                {
                    var index = innerList.IndexOf(existingItem);
                    if (index >= 0)
                    {
                        innerList.RemoveAt(index);
                        existingItem.DbReaderFeed = dbFeedItem;
                        innerList.Insert(index, existingItem);
                    }
                });
            }
            else
            {
                // Add new item
                _sourceItems.Add(new FeedListListItem { DbReaderFeed = dbFeedItem });
            }
        }
    }

    public async Task UpdateReadCount(List<Guid> changedItemGuid)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var db = await ContextDb.GetInstance();

        var feedIds = await db.FeedItems.Where(x => changedItemGuid.Contains(x.PersistentId))
            .GroupBy(x => x.FeedPersistentId)
            .Select(x => x.Key).ToListAsync();

        foreach (var loopFeedId in feedIds)
        {
            var totalItems = await db.FeedItems.CountAsync(x => x.FeedPersistentId == loopFeedId);
            var unReadItems = await db.FeedItems.CountAsync(x => x.FeedPersistentId == loopFeedId && !x.MarkedRead);

            await ThreadSwitcher.ResumeForegroundAsync();

            var item = _sourceItems.Items.SingleOrDefault(x => x.DbReaderFeed.PersistentId == loopFeedId);

            if (item == null) continue;

            // Update the counts and refresh the item
            _sourceItems.Edit(innerList =>
            {
                var index = innerList.IndexOf(item);
                if (index >= 0)
                {
                    innerList.RemoveAt(index);
                    item.ItemsCount = totalItems;
                    item.UnreadItemsCount = unReadItems;
                    innerList.Insert(index, item);
                }
            });
        }
    }

    // Helper class for custom sorting
    private class SortDescriptionComparer<T> : IComparer<T>
    {
        private readonly List<SortDescription> _sortDescriptions;

        public SortDescriptionComparer(List<SortDescription> sortDescriptions)
        {
            _sortDescriptions = sortDescriptions;
        }

        public int Compare(T? x, T? y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            foreach (var sort in _sortDescriptions)
            {
                var propertyInfo = typeof(T).GetProperty(sort.PropertyName);
                if (propertyInfo == null) continue;

                var xValue = propertyInfo.GetValue(x);
                var yValue = propertyInfo.GetValue(y);

                int result;
                if (xValue == null && yValue == null)
                    result = 0;
                else if (xValue == null)
                    result = -1;
                else if (yValue == null)
                    result = 1;
                else if (xValue is IComparable comparable)
                    result = comparable.CompareTo(yValue);
                else
                    result = 0;

                if (result != 0)
                    return sort.Direction == ListSortDirection.Ascending ? result : -result;
            }

            return 0;
        }
    }

    // Update the finalizer to dispose of the BehaviorSubject
    ~FeedListContext()
    {
        _dynamicDataConnection?.Dispose();
        _filterPredicate?.Dispose();
    }
}