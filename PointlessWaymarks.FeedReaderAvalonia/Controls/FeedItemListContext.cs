using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Reactive;
using System.Reactive.Subjects;
using System.Text;
using DynamicData;
using DynamicData.Binding;
using FluentScheduler;
using Microsoft.EntityFrameworkCore;
using PointlessWaymarks.AvaloniaCommon;
using PointlessWaymarks.AvaloniaCommon.ColumnSort;
using PointlessWaymarks.AvaloniaCommon.LocalHtml;
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
public partial class FeedItemListContext
{
    // DynamicData source and connections
    private readonly SourceList<FeedItemListListItem> _sourceItems = new();
    private readonly IDisposable _dynamicDataConnection;
    private readonly BehaviorSubject<Func<FeedItemListListItem, bool>> _filterPredicate = new(item => true);

    public bool AutoMarkRead { get; set; } = true;
    public required FeedQueries ContextDb { get; init; }
    public DataNotificationsWorkQueue? DataNotificationsProcessor { get; set; }
    public string DisplayBasicAuthPassword { get; set; } = string.Empty;
    public string DisplayBasicAuthUsername { get; set; } = string.Empty;
    public string DisplayUrl { get; set; } = string.Empty;
    public string RssViewDisplayUrl { get; set; } = string.Empty;
    public Guid? RssViewPreviousId { get; set; }
    public List<Guid> FeedList { get; set; } = [];
    public required AppPageServer PageServer { get; set; }
    public required ColumnSortControlContext ListSort { get; init; }
    public FeedItemListListItem? SelectedItem { get; set; }
    public List<FeedItemListListItem> SelectedItems { get; set; } = [];
    public bool ShowUnread { get; set; }
    public string UserAddFeedInput { get; set; } = string.Empty;
    public string UserFilterText { get; set; } = string.Empty;

    private readonly ReadOnlyObservableCollection<FeedItemListListItem> _data;
    // Read-only observable collection for UI binding
    public ReadOnlyObservableCollection<FeedItemListListItem> Items => _data;

    public FeedItemListContext()
    {
        // Set up DynamicData transformations
        _dynamicDataConnection = _sourceItems.Connect()
            .Filter(_filterPredicate)
            .Sort(SortExpressionComparer<FeedItemListListItem>.Descending(x => x.DbItem.PublishingDate))
            .Bind(out _data)
            .Subscribe();
    }

    public FeedItemListListItem? SelectedListItem()
    {
        return SelectedItem;
    }

    public List<FeedItemListListItem> SelectedListItems()
    {
        return SelectedItems;
    }

    public required StatusControlContext StatusContext { get; set; }

    [NonBlockingCommand]
    public async Task ClearReadItems()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var toRemove = _sourceItems.Items.Where(x => x.DbItem.MarkedRead).ToList();

        if (!toRemove.Any()) return;

        await ThreadSwitcher.ResumeForegroundAsync();

        _sourceItems.Edit(innerList =>
        {
            foreach (var item in toRemove)
            {
                innerList.Remove(item);
            }
        });
    }

    private async Task ShowFeedDisplayHtml(FeedItemListListItem? item)
    {
        var newPageId = Guid.NewGuid();

        if (RssViewPreviousId is not null)
        {
            PageServer.TryRemovePage(RssViewPreviousId.Value);
        }

        RssViewPreviousId = newPageId;

        if (item is null)
        {
            string feedDisplay =
                await """
                    <p>"No Valid Item?"</p>

                    """
                    .ToHtmlDocumentWithMinimalCss("Nothing...", "");

            await PageServer.AddPage(newPageId, feedDisplay);
            RssViewDisplayUrl = PageServer.GetPreviewUrl(newPageId);
            return;
        }

        try
        {
            var feedPage = await $"""
                                  <h3><a href="{item.DbItem.Link}">{item.DbItem.Title.HtmlEncode()}</a></h3>
                                  <h4>{item.DbReaderFeed.Name.HtmlEncode()}</h4>
                                  <hr />
                                  <p>{item.DbItem.Description}</p>
                                  <hr />
                                  {item.DbItem.Content}
                                  <hr />
                                  <ul>
                                   <li>Link: <a href="{item.DbItem.Link}">{item.DbItem.Link}</a></li>
                                   <li>Author: {item.DbItem.Author.HtmlEncode()}</li>
                                   <li>Created On: {item.DbItem.CreatedOn:F}</li>
                                   <li>Publishing Date: {item.DbItem.PublishingDate:F}</li>
                                   <li>Feed Item Id: {item.DbItem.FeedItemId.HtmlEncode()}</li>
                                   <li>Id: {item.DbItem.Id}</li>
                                   <li>Persistent Id: {item.DbItem.PersistentId}</li>
                                  </ul>
                                  """.ToHtmlDocumentWithMinimalCss($"RSS - {item.DbItem.Title.HtmlEncode()}",
                string.Empty);
            await PageServer.AddPage(newPageId, feedPage);
            RssViewDisplayUrl = PageServer.GetPreviewUrl(newPageId);
        }
        catch (Exception e)
        {
            Log.Error(e, "Error With ShowFeedDisplayHtml in FeedItemListContext");
            RssViewDisplayUrl = await PageServer.ErrorPage(newPageId, e);
        }
    }

    // This creates an instance of the context with a properly initialized ReadOnlyObservableCollection
    public static async Task<FeedItemListContext> CreateInstance(StatusControlContext statusContext, string dbFile,
        List<Guid>? feedList = null, bool showUnread = false)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var feedQueries = new FeedQueries { DbFileFullName = dbFile };

        var appPageServer = await AppPageServer.GetInstance();

        var context = new FeedItemListContext
        {
            StatusContext = statusContext,
            FeedList = feedList ?? [],
            ShowUnread = showUnread,
            ContextDb = feedQueries,
            PageServer = appPageServer,
            ListSort = new ColumnSortControlContext
            {
                Items =
                [
                    new ColumnSortControlSortItem
                    {
                        DisplayName = "Posted",
                        ColumnName = "DbItem.PublishingDate",
                        Order = 1,
                        DefaultSortDirection = ListSortDirection.Descending
                    },

                    new ColumnSortControlSortItem
                    {
                        DisplayName = "Item Name",
                        ColumnName = "DbItem.Title",
                        DefaultSortDirection = ListSortDirection.Descending
                    },

                    new ColumnSortControlSortItem
                    {
                        DisplayName = "Feed Name",
                        ColumnName = "DbReaderFeed.Name",
                        DefaultSortDirection = ListSortDirection.Ascending
                    },

                    new ColumnSortControlSortItem
                    {
                        DisplayName = "Item Author",
                        ColumnName = "DbItem.Author",
                        DefaultSortDirection = ListSortDirection.Ascending
                    }
                ]
            }
        };

        await context.Setup();

        return context;
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

    [NonBlockingCommand]
    public async Task FeedEditorForFeedItem(FeedItemListListItem? listItem)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (listItem == null)
        {
            await StatusContext.ToastError("Nothing Selected?");
            return;
        }

        var db = await ContextDb.GetInstance();
        var currentFeed =
            await db.Feeds.SingleOrDefaultAsync(x => x.PersistentId == listItem.DbReaderFeed.PersistentId);

        if (currentFeed == null)
        {
            await StatusContext.ToastError("Feed Not Found?!?");
            return;
        }

        await ThreadSwitcher.ResumeForegroundAsync();

        var window = await FeedEditorWindow.CreateInstance(currentFeed, ContextDb.DbFileFullName);
        window.PositionWindowAndShow();
    }

    [NonBlockingCommand]
    [StopAndWarnIfNoSelectedListItem]
    public async Task FeedEditorForSelectedItem()
    {
        await FeedEditorForFeedItem(SelectedItem);
    }

    private async Task FilterList()
    {
        if (!_sourceItems.Items.Any()) return;

        await ThreadSwitcher.ResumeForegroundAsync();

        if (string.IsNullOrWhiteSpace(UserFilterText))
        {
            _filterPredicate.OnNext(_ => true);
            return;
        }

        var cleanedFilterText = UserFilterText.Trim();

        _filterPredicate.OnNext(item =>
            item.DbReaderFeed.Name.Contains(cleanedFilterText, StringComparison.OrdinalIgnoreCase) ||
            item.DbReaderFeed.Tags.Contains(cleanedFilterText, StringComparison.OrdinalIgnoreCase) ||
            item.DbReaderFeed.Note.Contains(cleanedFilterText, StringComparison.OrdinalIgnoreCase) ||
            (item.DbItem.Title?.Contains(cleanedFilterText, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (item.DbItem.Author?.Contains(cleanedFilterText, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (item.DbItem.Link?.Contains(cleanedFilterText, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (item.DbItem.Description?.Contains(cleanedFilterText, StringComparison.OrdinalIgnoreCase) ?? false)
        );
    }

    private async Task ApplySorting()
    {
        var sortDescriptions = ListSort.SortDescriptions();
        if (!sortDescriptions.Any())
            return;

        // Build a comparer based on the sort descriptions
        IComparer<FeedItemListListItem> comparer = new SortDescriptionComparer<FeedItemListListItem>(sortDescriptions);

        // Sort the items
        var items = _sourceItems.Items.OrderBy(x => x, comparer).ToList();

        // Re-add the sorted items
        _sourceItems.Edit(innerList =>
        {
            innerList.Clear();
            innerList.AddRange(items);
        });
    }

    [BlockingCommand]
    private async Task ItemWebViewScreenshot()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        await UrlScreenShotHelper.GetUrlScreenShot(DisplayUrl, StatusContext);
    }

    [BlockingCommand]
    private async Task ItemRssViewScreenshot()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (string.IsNullOrWhiteSpace(RssViewDisplayUrl))
        {
            await StatusContext.ToastError("Rss View Display URL is blank - unable to take screenshot");
            return;
        }

        await UrlScreenShotHelper.GetUrlScreenShot(RssViewDisplayUrl, StatusContext);
    }

    [NonBlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task MarkdownLinksForSelectedItems()
    {
        var clipboardBlock = new StringBuilder();

        foreach (var loopItems in SelectedListItems())
            clipboardBlock.AppendLine($"[{loopItems.DbItem.Title ?? "No Title"}]({loopItems.DbItem.Link})");

        await ThreadSwitcher.ResumeForegroundAsync();

        await ClipboardHelper.TextToClipboardIfPossible(clipboardBlock.ToString(), StatusContext);
    }

    [NonBlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task MarkSelectedRead()
    {
        await ContextDb.ItemRead(SelectedItems.Select(x => x.DbItem.PersistentId).ToList(), true);
    }

    [NonBlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task MarkSelectedUnRead()
    {
        await ContextDb.ItemRead(SelectedItems.Select(x => x.DbItem.PersistentId).ToList(), false);
    }

    [BlockingCommand]
    public async Task NewFeedEditorFromUrl()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (string.IsNullOrEmpty(UserAddFeedInput))
        {
            await StatusContext.ToastWarning("Feed to Add is Blank?");
            return;
        }

        var feedItem = await ContextDb.TryGetFeed(UserAddFeedInput, StatusContext.ProgressTracker());

        await ThreadSwitcher.ResumeForegroundAsync();

        var window = await FeedEditorWindow.CreateInstance(feedItem, ContextDb.DbFileFullName);

        window.PositionWindowAndShow();

        UserAddFeedInput = string.Empty;
    }

    private void OnDataNotificationReceived(object? sender, TinyMessageReceivedEventArgs e)
    {
        DataNotificationsProcessor?.Enqueue(e);
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.PropertyName)) return;

        if (e.PropertyName == nameof(UserFilterText))
            StatusContext.RunNonBlockingTask(FilterList);

        if (e.PropertyName == nameof(AutoMarkRead))
            StatusContext.RunNonBlockingTask(async () =>
            {
                try
                {
                    var settings = FeedReaderGuiSettingTools.ReadSettings();
                    settings.AutoMarkReadDefault = AutoMarkRead;
                    await FeedReaderGuiSettingTools.WriteSettings(settings);
                }
                catch (Exception)
                {
                    //Ignored
                }
            });

        if (e.PropertyName.Equals(nameof(SelectedItem)))
        {
            Debug.WriteLine($"FeedItemListContext SelectedItem {SelectedItem?.DbItem.Title}");

            if (SelectedItem is { DbItem: { MarkedRead: false, KeepUnread: false } } && AutoMarkRead)
                StatusContext.RunNonBlockingTask(async () =>
                {
                    await ContextDb.ItemRead(SelectedItem.DbItem.PersistentId.AsList(), true);
                });

            StatusContext.RunNonBlockingTask(async () =>
            {
                try
                {
                    if (SelectedItem is not null && SelectedItem.DbReaderFeed.UseBasicAuth)
                    {
                        var credentials = await FeedReaderEncryption.DecryptBasicAuthCredentials(
                            SelectedItem.DbReaderFeed.BasicAuthUsername,
                            SelectedItem.DbReaderFeed.BasicAuthPassword,
                            ContextDb.DbFileFullName);
                        DisplayBasicAuthUsername = credentials.username;
                        DisplayBasicAuthPassword = credentials.password;
                    }
                    else
                    {
                        DisplayBasicAuthUsername = string.Empty;
                        DisplayBasicAuthPassword = string.Empty;
                    }

                    StatusContext.RunNonBlockingTask(async () =>
                        await ShowFeedDisplayHtml(SelectedItem));
                    DisplayUrl = string.IsNullOrWhiteSpace(SelectedItem?.DbItem.Link)
                        ? "about:blank"
                        : SelectedItem.DbItem.Link;
                }
                catch (Exception exception)
                {
                    Log.Error(exception, "Error With Display URL in the FeedItemListContext");
                    DisplayUrl = await PageServer.ErrorPage(exception);
                }
            });
        }
    }

    [NonBlockingCommand]
    [StopAndWarnIfNoSelectedListItem]
    public async Task OpenSelectedItemInBrowser()
    {
        if (string.IsNullOrWhiteSpace(SelectedItem?.DbItem.Link))
        {
            await StatusContext.ToastWarning("Feed Item has no Link?");
            return;
        }

        await ThreadSwitcher.ResumeForegroundAsync();

        Process.Start(new ProcessStartInfo(SelectedItem.DbItem.Link) { UseShellExecute = true });
    }

    private async Task ProcessDataUpdateNotification(InterProcessDataNotification interProcessUpdateNotification)
    {
        if (interProcessUpdateNotification.ContentType == DataNotificationContentType.FeedItem)
        {
            if (interProcessUpdateNotification.UpdateType == DataNotificationUpdateType.Delete)
            {
                await ThreadSwitcher.ResumeForegroundAsync();

                _sourceItems.Edit(innerList =>
                {
                    var toRemove = innerList
                        .Where(x => interProcessUpdateNotification.ContentIds.Contains(x.DbItem.PersistentId))
                        .ToList();
                    foreach (var item in toRemove)
                    {
                        innerList.Remove(item);
                    }
                });
                return;
            }

            if (interProcessUpdateNotification.UpdateType is DataNotificationUpdateType.Update
                or DataNotificationUpdateType.New)
                await UpdateFeedItems(interProcessUpdateNotification.ContentIds);
        }

        if (interProcessUpdateNotification.ContentType == DataNotificationContentType.Feed)
        {
            var feedsToUpdate = FeedList.Any()
                ? FeedList.Intersect(interProcessUpdateNotification.ContentIds).ToList()
                : _sourceItems.Items.Select(x => x.DbReaderFeed.PersistentId).ToList();
            if (!feedsToUpdate.Any()) return;

            var toUpdate = _sourceItems.Items.Where(x => feedsToUpdate.Contains(x.DbItem.FeedPersistentId))
                .Select(x => x.DbItem.PersistentId).ToList();
            await UpdateFeedItems(toUpdate);
        }
    }

    [BlockingCommand]
    public async Task RefreshFeedItems()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var errors = FeedList is { Count: > 0 }
            ? await ContextDb.UpdateFeeds(FeedList, StatusContext.ProgressTracker())
            : await ContextDb.UpdateFeeds(StatusContext.ProgressTracker());
        foreach (var loopError in errors) await StatusContext.ToastError(loopError);
    }

    [NonBlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task SaveSelectedItems()
    {
        await ContextDb.SaveFeedItems(SelectedItems.Select(x => x.DbItem.PersistentId).ToList());
    }

    public async Task Setup()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        BuildCommands();

        var db = await ContextDb.GetInstance();

        var initialItemFilter = db.FeedItems.Where(x => x.MarkedRead == ShowUnread);
        if (FeedList.Any()) initialItemFilter = initialItemFilter.Where(x => FeedList.Contains(x.FeedPersistentId));

        var initialItems = await initialItemFilter.OrderByDescending(x => x.PublishingDate)
            .ThenBy(x => x.Title).ToListAsync();

        var feedItems = new List<FeedItemListListItem>();
        foreach (var loopItems in initialItems)
        {
            feedItems.Add(new FeedItemListListItem
            {
                DbItem = loopItems,
                DbReaderFeed = db.Feeds.Single(x => x.PersistentId == loopItems.FeedPersistentId)
            });
        }

        await ThreadSwitcher.ResumeForegroundAsync();

        // Add all items at once for better performance
        _sourceItems.AddRange(feedItems);

        // Apply initial sorting and filtering
        await ApplySorting();
        await FilterList();

        ListSort.SortUpdated += (_, _) =>
            StatusContext.RunNonBlockingTask(ApplySorting);

        PropertyChanged += OnPropertyChanged;
        DataNotificationsProcessor = new DataNotificationsWorkQueue { Processor = DataNotificationReceived };
        DataNotifications.NewDataNotificationChannel().MessageReceived += OnDataNotificationReceived;

        StatusContext.RunNonBlockingTask(async () => { await RefreshFeedItems(); });

        JobManager.Initialize();

        JobManager.AddJob(
            async () =>
            {
                try
                {
                    await RefreshFeedItems();
                }
                catch (Exception e)
                {
                    Log.ForContext("ignored exception", e.ToString())
                        .Verbose("Error in Feed Item List Background Refresh (Ignored)");
                }
            },
            s => s.ToRunEvery(2).Hours()
        );
    }

    [NonBlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task TitleAndUrlForSelectedItems()
    {
        var clipboardBlock = new StringBuilder();

        foreach (var loopItems in SelectedListItems())
            clipboardBlock.AppendLine($"{loopItems.DbItem.Title ?? "(No Title)"} - {loopItems.DbItem.Link}");

        await ThreadSwitcher.ResumeForegroundAsync();

        await ClipboardHelper.TextToClipboardIfPossible(clipboardBlock.ToString(), StatusContext);
    }

    [NonBlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task TitlesForSelectedItems()
    {
        var clipboardBlock = new StringBuilder();

        foreach (var loopItems in SelectedListItems())
            clipboardBlock.AppendLine($"{loopItems.DbItem.Title ?? "(No Title)"}");

        await ThreadSwitcher.ResumeForegroundAsync();

        await ClipboardHelper.TextToClipboardIfPossible(clipboardBlock.ToString(), StatusContext);
    }

    [NonBlockingCommand]
    [StopAndWarnIfNoSelectedListItem]
    public async Task ToggleKeepUnread(FeedItemListListItem? listItem)
    {
        await ContextDb.ItemKeepUnreadToggle(listItem!.DbItem.PersistentId.AsList(), StatusContext.ProgressTracker());
    }

    [NonBlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task ToggleSelectedKeepUnRead()
    {
        await ContextDb.ItemKeepUnreadToggle(SelectedItems.Select(x => x.DbItem.PersistentId).ToList(),
            StatusContext.ProgressTracker());
    }

    [BlockingCommand]
    public async Task TryAddFeed()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (string.IsNullOrEmpty(UserAddFeedInput))
        {
            await StatusContext.ToastWarning("Feed to Add is Blank?");
            return;
        }

        var result = await ContextDb.TryAddFeed(UserAddFeedInput, StatusContext.ProgressTracker());

        result.Switch(_ => StatusContext.ToastSuccess($"Added Feed for {UserAddFeedInput}"),
            error => StatusContext.ToastError(error.Value));

        UserAddFeedInput = string.Empty;
    }

    public async Task UpdateFeedItems(List<Guid> toUpdate)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var db = await ContextDb.GetInstance();

        foreach (var loopContentIds in toUpdate)
        {
            await ThreadSwitcher.ResumeBackgroundAsync();

            var listItem = _sourceItems.Items.SingleOrDefault(x => x.DbItem.PersistentId == loopContentIds);
            var dbFeedItem = db.FeedItems.SingleOrDefault(x => x.PersistentId == loopContentIds);

            //If there is no database item remove it if it exists in the items and continue
            if (dbFeedItem == null)
            {
                if (listItem != null)
                {
                    await ThreadSwitcher.ResumeForegroundAsync();
                    _sourceItems.Remove(listItem);
                }

                continue;
            }

            var dbFeed = db.Feeds.SingleOrDefault(x => x.PersistentId == dbFeedItem.FeedPersistentId);

            //If the Feed is not in the Db remove the item from the collection
            if (dbFeed == null)
            {
                if (listItem != null)
                {
                    await ThreadSwitcher.ResumeForegroundAsync();
                    _sourceItems.Remove(listItem);
                }

                continue;
            }

            await ThreadSwitcher.ResumeForegroundAsync();

            //Update the existing list item - if there isn't one add a new one
            if (listItem != null)
            {
                _sourceItems.Edit(innerList =>
                {
                    var index = innerList.IndexOf(listItem);
                    if (index >= 0)
                    {
                        listItem.DbItem = dbFeedItem;
                        listItem.DbReaderFeed = dbFeed;
                    }
                });
            }
            else
            {
                _sourceItems.Add(new FeedItemListListItem { DbReaderFeed = dbFeed, DbItem = dbFeedItem });
            }
        }

        if (SelectedItem != null && toUpdate.Contains(SelectedItem.DbItem.PersistentId))
            if (SelectedItem.DbItem is { KeepUnread: false, MarkedRead: false } && AutoMarkRead)
                StatusContext.RunNonBlockingTask(async () =>
                    await ContextDb.ItemRead(SelectedItem.DbItem.PersistentId.AsList(), true));
    }

    [NonBlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task UrlsForSelectedItems()
    {
        var clipboardBlock = new StringBuilder();

        foreach (var loopItems in SelectedListItems()) clipboardBlock.AppendLine($"{loopItems.DbItem.Link}");

        await ThreadSwitcher.ResumeForegroundAsync();

        await ClipboardHelper.TextToClipboardIfPossible(clipboardBlock.ToString(), StatusContext);
    }

    // Helper class for custom sorting
    private class SortDescriptionComparer<T>(List<SortDescription> sortDescriptions) : IComparer<T>
    {
        public int Compare(T? x, T? y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            foreach (var sort in sortDescriptions)
            {
                // Handle nested property paths like "DbItem.PublishingDate"
                var value1 = GetPropertyValue(x, sort.PropertyName);
                var value2 = GetPropertyValue(y, sort.PropertyName);

                int result;
                if (value1 == null && value2 == null)
                    result = 0;
                else if (value1 == null)
                    result = -1;
                else if (value2 == null)
                    result = 1;
                else if (value1 is IComparable comparable)
                    result = comparable.CompareTo(value2);
                else
                    result = 0;

                if (result != 0)
                    return sort.Direction == ListSortDirection.Ascending ? result : -result;
            }

            return 0;
        }

        private static object? GetPropertyValue(object obj, string propertyPath)
        {
            var properties = propertyPath.Split('.');
            var value = obj;

            foreach (var property in properties)
            {
                var propInfo = value.GetType().GetProperty(property);
                if (propInfo == null)
                    return null;

                value = propInfo.GetValue(value);
                if (value == null)
                    return null;
            }

            return value;
        }
    }

    // Remember to dispose the connection when the context is no longer needed
    ~FeedItemListContext()
    {
        _dynamicDataConnection?.Dispose();
        _filterPredicate?.Dispose();
    }
}