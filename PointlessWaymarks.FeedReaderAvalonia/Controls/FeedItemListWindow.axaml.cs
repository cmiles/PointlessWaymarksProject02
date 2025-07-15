using Avalonia;
using Avalonia.Controls;
using PointlessWaymarks.AvaloniaCommon;
using PointlessWaymarks.AvaloniaCommon.Status;
using PointlessWaymarks.AvaloniaCommon.Utility;

namespace PointlessWaymarks.FeedReaderAvalonia.Controls;

public partial class FeedItemListWindow : Window
{
    // Define direct properties
    public static readonly DirectProperty<FeedItemListWindow, FeedItemListContext?> ItemsContextProperty =
        AvaloniaProperty.RegisterDirect<FeedItemListWindow, FeedItemListContext?>(
            nameof(ItemsContext),
            o => o.ItemsContext,
            (o, v) => o.ItemsContext = v);

    public static readonly DirectProperty<FeedItemListWindow, StatusControlContext> StatusContextProperty =
        AvaloniaProperty.RegisterDirect<FeedItemListWindow, StatusControlContext>(
            nameof(StatusContext),
            o => o.StatusContext,
            (o, v) => o.StatusContext = v);

    // Backing fields
    private FeedItemListContext? _itemsContext;
    private StatusControlContext _statusContext;

    // CLR property wrappers
    public FeedItemListContext? ItemsContext
    {
        get => _itemsContext;
        set => SetAndRaise(ItemsContextProperty, ref _itemsContext, value);
    }

    public StatusControlContext StatusContext
    {
        get => _statusContext;
        set => SetAndRaise(StatusContextProperty, ref _statusContext, value);
    }

    // Initialize required properties in constructor
    public FeedItemListWindow()
    {
        // Initialize with a temporary value that will be replaced in CreateInstance
        _statusContext = new StatusControlContext();

        InitializeComponent();
        DataContext = this;
    }

    public static async Task<FeedItemListWindow> CreateInstance(string dbFile, List<Guid>? feedList = null,
        bool showUnread = false)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        var factoryStatusContext = await StatusControlContext.CreateInstance();
        factoryStatusContext.BlockUi = true;

        var window = new FeedItemListWindow();

        // Set the StatusContext using the direct property
        window.StatusContext = factoryStatusContext;

        window.PositionWindowAndShow();

        await ThreadSwitcher.ResumeBackgroundAsync();

        window.StatusContext.Progress("Feed Items List - Creating Context");

        // Set the ItemsContext using the direct property
        window.ItemsContext =
            await FeedItemListContext.CreateInstance(window.StatusContext, dbFile, feedList, showUnread);

        window.StatusContext.BlockUi = false;

        await ThreadSwitcher.ResumeForegroundAsync();

        return window;
    }
}