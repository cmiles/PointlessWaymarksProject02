using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using PointlessWaymarks.FeedReaderData.Models;
using PointlessWaymarks.AvaloniaCommon.Status;
using PointlessWaymarks.AvaloniaCommon.ChangesAndValidation;
using System.Threading.Tasks;
using PointlessWaymarks.AvaloniaCommon;

namespace PointlessWaymarks.FeedReaderAvalonia.Controls;

public partial class FeedEditorWindow : Window
{
    public FeedEditorWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    public WindowAccidentalClosureHelper? AccidentalCloserHelper { get; set; }
    public FeedEditorContext? FeedContext { get; set; }
    public required StatusControlContext StatusContext { get; set; }

    /// <summary>
    ///     Creates a new instance - this method can be called from any thread and will
    ///     switch to the UI thread as needed. Does not show the window - consider using
    ///     PositionWindowAndShowOnUiThread() from the WindowInitialPositionHelpers.
    /// </summary>
    /// <returns></returns>
    public static async Task<FeedEditorWindow> CreateInstance(ReaderFeed toLoad, string dbFile)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        var window = new FeedEditorWindow { StatusContext = await StatusControlContext.CreateInstance() };

        await ThreadSwitcher.ResumeBackgroundAsync();

        window.FeedContext = await FeedEditorContext.CreateInstance(window.StatusContext, toLoad, dbFile);

        window.FeedContext.RequestContentEditorWindowClose += (_, _) => { window.Close(); };

        window.AccidentalCloserHelper =
            new WindowAccidentalClosureHelper(window, window.StatusContext, window.FeedContext);

        await ThreadSwitcher.ResumeForegroundAsync();

        return window;
    }
}