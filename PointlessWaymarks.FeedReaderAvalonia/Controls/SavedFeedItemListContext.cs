using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Reactive;
using System.Reactive.Subjects;
using System.Text;
using DynamicData;
using DynamicData.Binding;
using Microsoft.EntityFrameworkCore;
using Omu.ValueInjecter;
using OneOf;
using OneOf.Types;
using PointlessWaymarks.AvaloniaCommon;
using PointlessWaymarks.AvaloniaCommon.ColumnSort;
using PointlessWaymarks.AvaloniaCommon.LocalHtml;
using PointlessWaymarks.AvaloniaCommon.Status;
using PointlessWaymarks.AvaloniaCommon.Utility;
using PointlessWaymarks.AvaloniaLlamaAspects;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.FeedReaderData;
using PointlessWaymarks.FeedReaderData.Models;
using Serilog;
using TinyIpc.Messaging;

namespace PointlessWaymarks.FeedReaderAvalonia.Controls;

[GenerateStatusCommands]
[NotifyPropertyChanged]
public partial class SavedFeedItemListContext
{
    // DynamicData source and connections
    private readonly SourceList<SavedFeedItemListListItem> _sourceItems = new();
    private readonly IDisposable _dynamicDataConnection;
    private readonly BehaviorSubject<Func<SavedFeedItemListListItem, bool>> _filterPredicate = new(item => true);

    public required FeedQueries ContextDb { get; init; }
    public DataNotificationsWorkQueue? DataNotificationsProcessor { get; set; }
    public string DisplayUrl { get; set; } = string.Empty;
    public List<Guid> FeedList { get; set; } = [];
    public Func<Task<OneOf<Success<byte[]>, Error<string>>>>? ItemRssViewScreenshotFunction { get; set; }
    public Func<Task<OneOf<Success<byte[]>, Error<string>>>>? ItemWebViewScreenshotFunction { get; set; }
    public required ColumnSortControlContext ListSort { get; init; }
    public SavedFeedItemListListItem? SelectedItem { get; set; }
    public List<SavedFeedItemListListItem> SelectedItems { get; set; } = [];
    public required StatusControlContext StatusContext { get; init; }
    public string UserFilterText { get; set; } = string.Empty;
    public required AppPageServer PageServer { get; set; }
    public Guid? RssViewPreviousId { get; set; }
    public string RssViewDisplayUrl { get; set; } = string.Empty;

    // Read-only observable collection for UI binding
    public required ReadOnlyObservableCollection<SavedFeedItemListListItem> Items { get; init; }

    public SavedFeedItemListContext()
    {
        // Set up DynamicData transformations
        _dynamicDataConnection = _sourceItems.Connect()
            .Filter(_filterPredicate)
            .Sort(SortExpressionComparer<SavedFeedItemListListItem>.Descending(x => x.DbItem.PublishingDate))
            .Bind(out var items)
            .Subscribe();

        Items = items;
    }

    [NonBlockingCommand]
    public async Task ArchiveSelectedItems()
    {
        if (!SelectedItems.Any())
        {
            await StatusContext.ToastWarning("Nothing Selected?");
            return;
        }

        await ContextDb.ArchiveSavedItems(SelectedItems.Select(x => x.DbItem.PersistentId).ToList());
    }

    private async Task ShowFeedDisplayHtml(SavedFeedItemListListItem? item)
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

            RssViewDisplayUrl = await PageServer.AddPage(newPageId, feedDisplay);
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
            RssViewDisplayUrl = await PageServer.AddPage(newPageId, feedPage);
        }
        catch (Exception e)
        {
            Log.Error(e, "Error With ShowFeedDisplayHtml in FeedItemListContext");
            RssViewDisplayUrl = await PageServer.ErrorPage(newPageId, e);
        }
    }

    public static async Task<SavedFeedItemListContext> CreateInstance(StatusControlContext statusContext, string dbFile,
        List<Guid>? feedList = null, bool showUnread = false)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        await ThreadSwitcher.ResumeBackgroundAsync();

        var feedQueries = new FeedQueries() { DbFileFullName = dbFile };

        var newContext = new SavedFeedItemListContext
        {
            Items = new ReadOnlyObservableCollection<SavedFeedItemListListItem>([]),
            StatusContext = statusContext,
            FeedList = feedList ?? [],
            ContextDb = feedQueries,
            PageServer = new AppPageServer(),
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
    public async Task DeleteSelected()
    {
        if (!SelectedItems.Any())
        {
            await StatusContext.ToastWarning("Nothing To Delete?");
            return;
        }

        var db = await ContextDb.GetInstance();

        foreach (var loopSelected in SelectedItems)
        {
            var newHistoric = new HistoricSavedFeedItem()
            { FeedPersistentId = loopSelected.DbItem.FeedPersistentId, FeedTitle = loopSelected.DbItem.FeedTitle };
            newHistoric.InjectFrom(loopSelected);

            db.HistoricSavedFeedItems.Add(newHistoric);
            db.SavedFeedItems.Remove(loopSelected.DbItem);

            await db.SaveChangesAsync();
        }
    }

    [NonBlockingCommand]
    public async Task FeedEditorForFeedItem(SavedFeedItemListListItem? listItem)
    {
        if (listItem?.DbItem == null)
        {
            await StatusContext.ToastWarning("This item isn't attached to an active feed...");
            return;
        }

        await ThreadSwitcher.ResumeBackgroundAsync();

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
    public async Task FeedEditorForSelectedItem()
    {
        if (SelectedItem == null)
        {
            await StatusContext.ToastWarning("Nothing Selected?");
            return;
        }

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
            (item.DbReaderFeed?.Name.Contains(cleanedFilterText, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (item.DbReaderFeed?.Tags.Contains(cleanedFilterText, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (item.DbReaderFeed?.Note.Contains(cleanedFilterText, StringComparison.OrdinalIgnoreCase) ?? false) ||
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
        IComparer<SavedFeedItemListListItem> comparer = new SortDescriptionComparer<SavedFeedItemListListItem>(sortDescriptions);

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

    [NonBlockingCommand]
    public async Task MarkdownLinksForSelectedItems()
    {
        if (!SelectedItems.Any())
        {
            await StatusContext.ToastWarning("Nothing Selected?");
            return;
        }

        var clipboardBlock = new StringBuilder();

        foreach (var loopItems in SelectedItems)
            clipboardBlock.AppendLine($"[{loopItems.DbItem.Title ?? "No Title"}]({loopItems.DbItem.Link})");

        await ThreadSwitcher.ResumeForegroundAsync();

        await ClipboardHelper.TextToClipboardIfPossible(clipboardBlock.ToString(), StatusContext);
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

        if (e.PropertyName.Equals(nameof(SelectedItem)))
        {
            StatusContext.RunFireAndForgetNonBlockingTask(async () => await (ShowFeedDisplayHtml(SelectedItem)));

            DisplayUrl = string.IsNullOrWhiteSpace(SelectedItem?.DbItem.Link)
                ? "about:blank"
                : SelectedItem.DbItem.Link;
        }
    }

    [NonBlockingCommand]
    public async Task OpenSelectedItemInBrowser()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (SelectedItem == null)
        {
            await StatusContext.ToastWarning("Feed to Add is Blank?");
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedItem.DbItem.Link))
        {
            await StatusContext.ToastWarning("Feed Item has no Link?");
            return;
        }

        await ThreadSwitcher.ResumeForegroundAsync();

        Process.Start(new ProcessStartInfo(SelectedItem.DbItem.Link) { UseShellExecute = true });
    }

    private async Task ProcessDataUpdateNotification(InterProcessDataNotification interProcessUpdateNotification)
    {
        if (interProcessUpdateNotification.ContentType == DataNotificationContentType.SavedFeedItem)
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
    }

    [BlockingCommand]
    private async Task RssViewScreenshot()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (string.IsNullOrWhiteSpace(RssViewDisplayUrl))
        {
            await StatusContext.ToastError("Rss View Display URL is blank - unable to take screenshot");
            return;
        }

        await UrlScreenShotHelper.GetUrlScreenShot(RssViewDisplayUrl, StatusContext);
    }

    public async Task Setup()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        BuildCommands();

        var db = await ContextDb.GetInstance();

        var initialItemFilter = db.SavedFeedItems.AsQueryable();
        if (FeedList.Any()) initialItemFilter = initialItemFilter.Where(x => FeedList.Contains(x.FeedPersistentId));

        var initialItems = await initialItemFilter.OrderByDescending(x => x.PublishingDate)
            .ThenBy(x => x.Title).ToListAsync();

        var savedItems = new List<SavedFeedItemListListItem>();
        foreach (var loopItems in initialItems)
        {
            savedItems.Add(new SavedFeedItemListListItem
            {
                DbItem = loopItems,
                DbReaderFeed = db.Feeds.SingleOrDefault(x => x.PersistentId == loopItems.FeedPersistentId)
            });
        }

        await ThreadSwitcher.ResumeForegroundAsync();

        // Add all items at once for better performance
        _sourceItems.AddRange(savedItems);

        // Apply initial sorting and filtering
        await ApplySorting();
        await FilterList();

        ListSort.SortUpdated += (_, list) =>
            StatusContext.RunFireAndForgetNonBlockingTask(() => ApplySorting());

        PropertyChanged += OnPropertyChanged;
        DataNotificationsProcessor = new DataNotificationsWorkQueue { Processor = DataNotificationReceived };
        DataNotifications.NewDataNotificationChannel().MessageReceived += OnDataNotificationReceived;
    }

    [NonBlockingCommand]
    public async Task TitleAndUrlForSelectedItems()
    {
        if (!SelectedItems.Any())
        {
            await StatusContext.ToastWarning("Nothing Selected?");
            return;
        }

        var clipboardBlock = new StringBuilder();

        foreach (var loopItems in SelectedItems)
            clipboardBlock.AppendLine($"{loopItems.DbItem.Title ?? "(No Title)"} - {loopItems.DbItem.Link}");

        await ThreadSwitcher.ResumeForegroundAsync();

        await ClipboardHelper.TextToClipboardIfPossible(clipboardBlock.ToString(), StatusContext);
    }

    [NonBlockingCommand]
    public async Task TitlesForSelectedItems()
    {
        if (!SelectedItems.Any())
        {
            await StatusContext.ToastWarning("Nothing Selected?");
            return;
        }

        var clipboardBlock = new StringBuilder();

        foreach (var loopItems in SelectedItems) clipboardBlock.AppendLine($"{loopItems.DbItem.Title ?? "(No Title)"}");

        await ThreadSwitcher.ResumeForegroundAsync();

        await ClipboardHelper.TextToClipboardIfPossible(clipboardBlock.ToString(), StatusContext);
    }

    public async Task UpdateFeedItems(List<Guid> toUpdate)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var db = await ContextDb.GetInstance();

        foreach (var loopContentIds in toUpdate)
        {
            await ThreadSwitcher.ResumeBackgroundAsync();

            var listItem = _sourceItems.Items.SingleOrDefault(x => x.DbItem.PersistentId == loopContentIds);
            var dbFeedItem = db.SavedFeedItems.SingleOrDefault(x => x.PersistentId == loopContentIds);

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

            await ThreadSwitcher.ResumeForegroundAsync();

            //Update the existing list item - if there isn't one add a new one
            if (listItem != null)
            {
                _sourceItems.Edit(innerList =>
                {
                    var index = innerList.IndexOf(listItem);
                    if (index >= 0)
                    {
                        innerList.RemoveAt(index);
                        listItem.DbItem = dbFeedItem;
                        listItem.DbReaderFeed = dbFeed;
                        innerList.Insert(index, listItem);
                    }
                });
            }
            else
            {
                _sourceItems.Add(new SavedFeedItemListListItem { DbReaderFeed = dbFeed, DbItem = dbFeedItem });
            }
        }
    }

    [NonBlockingCommand]
    public async Task UrlsForSelectedItems()
    {
        if (!SelectedItems.Any())
        {
            await StatusContext.ToastWarning("Nothing Selected?");
            return;
        }

        var clipboardBlock = new StringBuilder();

        foreach (var loopItems in SelectedItems) clipboardBlock.AppendLine($"{loopItems.DbItem.Link}");

        await ThreadSwitcher.ResumeForegroundAsync();

        await ClipboardHelper.TextToClipboardIfPossible(clipboardBlock.ToString(), StatusContext);
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
    ~SavedFeedItemListContext()
    {
        _dynamicDataConnection?.Dispose();
        _filterPredicate?.Dispose();
    }
}