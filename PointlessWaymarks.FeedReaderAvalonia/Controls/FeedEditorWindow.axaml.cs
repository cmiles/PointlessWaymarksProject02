using Avalonia;
using Avalonia.Controls;
using PointlessWaymarks.FeedReaderData.Models;
using PointlessWaymarks.AvaloniaCommon.Status;
using PointlessWaymarks.AvaloniaCommon.ChangesAndValidation;
using System.Threading.Tasks;
using PointlessWaymarks.AvaloniaCommon;

namespace PointlessWaymarks.FeedReaderAvalonia.Controls;

public partial class FeedEditorWindow : Window
{
    // Define direct properties
    public static readonly DirectProperty<FeedEditorWindow, WindowAccidentalClosureHelper?> AccidentalCloserHelperProperty =
        AvaloniaProperty.RegisterDirect<FeedEditorWindow, WindowAccidentalClosureHelper?>(
            nameof(AccidentalCloserHelper),
            o => o.AccidentalCloserHelper,
            (o, v) => o.AccidentalCloserHelper = v);

    public static readonly DirectProperty<FeedEditorWindow, FeedEditorContext?> FeedContextProperty =
        AvaloniaProperty.RegisterDirect<FeedEditorWindow, FeedEditorContext?>(
            nameof(FeedContext),
            o => o.FeedContext,
            (o, v) => o.FeedContext = v);

    public static readonly DirectProperty<FeedEditorWindow, StatusControlContext> StatusContextProperty =
        AvaloniaProperty.RegisterDirect<FeedEditorWindow, StatusControlContext>(
            nameof(StatusContext),
            o => o.StatusContext,
            (o, v) => o.StatusContext = v);

    // Backing fields
    private WindowAccidentalClosureHelper? _accidentalCloserHelper;
    private FeedEditorContext? _feedContext;
    private StatusControlContext _statusContext;

    // CLR property wrappers
    public WindowAccidentalClosureHelper? AccidentalCloserHelper
    {
        get => _accidentalCloserHelper;
        set => SetAndRaise(AccidentalCloserHelperProperty, ref _accidentalCloserHelper, value);
    }

    public FeedEditorContext? FeedContext
    {
        get => _feedContext;
        set => SetAndRaise(FeedContextProperty, ref _feedContext, value);
    }

    public StatusControlContext StatusContext
    {
        get => _statusContext;
        set => SetAndRaise(StatusContextProperty, ref _statusContext, value);
    }

    // The constructor needs to initialize the StatusContext backing field
    // since it's marked as required
    public FeedEditorWindow()
    {
        // Initialize with a temporary value that will be replaced in CreateInstance
        _statusContext = new StatusControlContext();

        InitializeComponent();
        DataContext = this;
    }

    /// <summary>
    ///     Creates a new instance - this method can be called from any thread and will
    ///     switch to the UI thread as needed. Does not show the window - consider using
    ///     PositionWindowAndShowOnUiThread() from the WindowInitialPositionHelpers.
    /// </summary>
    /// <returns></returns>
    public static async Task<FeedEditorWindow> CreateInstance(ReaderFeed toLoad, string dbFile)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        var window = new FeedEditorWindow();

        // Set the StatusContext property using the direct property
        window.StatusContext = await StatusControlContext.CreateInstance();

        await ThreadSwitcher.ResumeBackgroundAsync();

        // Set the FeedContext property using the direct property
        window.FeedContext = await FeedEditorContext.CreateInstance(window.StatusContext, toLoad, dbFile);

        window.FeedContext.RequestContentEditorWindowClose += (_, _) => { window.Close(); };

        // Set the AccidentalCloserHelper property using the direct property
        window.AccidentalCloserHelper =
            new WindowAccidentalClosureHelper(window, window.StatusContext, window.FeedContext);

        await ThreadSwitcher.ResumeForegroundAsync();

        return window;
    }
}