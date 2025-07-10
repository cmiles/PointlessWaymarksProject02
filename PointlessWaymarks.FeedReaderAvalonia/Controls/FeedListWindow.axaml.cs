using Avalonia.Controls;
using Metalama.Patterns.Observability;
using PointlessWaymarks.AvaloniaCommon;
using PointlessWaymarks.AvaloniaCommon.Status;
using PointlessWaymarks.AvaloniaCommon.Utility;
using PointlessWaymarks.AvaloniaLlamaAspects;

namespace PointlessWaymarks.FeedReaderAvalonia.Controls;

[Observable]
[StaThreadConstructorGuard]
public partial class FeedListWindow : Window
{
    public FeedListWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    public FeedListContext? FeedContext { get; set; }
    public required StatusControlContext StatusContext { get; set; }

    public static async Task<FeedListWindow> CreateInstance(string dbFile)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        var factoryStatusContext = await StatusControlContext.CreateInstance();
        factoryStatusContext.BlockUi = true;

        var window = new FeedListWindow
        {
            StatusContext = factoryStatusContext
        };

        window.PositionWindowAndShow();

        await ThreadSwitcher.ResumeBackgroundAsync();

        window.StatusContext.Progress("Feed List - Creating Context");

        window.FeedContext = await FeedListContext.CreateInstance(window.StatusContext, dbFile);

        window.StatusContext.BlockUi = false;

        await ThreadSwitcher.ResumeForegroundAsync();

        return window;
    }
} 