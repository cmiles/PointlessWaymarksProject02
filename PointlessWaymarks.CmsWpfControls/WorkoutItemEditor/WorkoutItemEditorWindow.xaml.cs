using PointlessWaymarks.CmsData;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.ChangesAndValidation;
using PointlessWaymarks.WpfCommon.Status;
using PointlessWaymarks.WpfCommon.Utility;

namespace PointlessWaymarks.CmsWpfControls.WorkoutItemEditor;

/// <summary>
///     Interaction logic for WorkoutItemEditorWindow.xaml
/// </summary>
[NotifyPropertyChanged]
public partial class WorkoutItemEditorWindow
{
    private WorkoutItemEditorWindow()
    {
        InitializeComponent();
        DataContext = this;
        WindowTitle = $"Workout Editor - {UserSettingsSingleton.CurrentSettings().SiteName}";
    }

    public WindowAccidentalClosureHelper? AccidentalCloserHelper { get; set; }
    public required StatusControlContext StatusContext { get; set; }
    public string WindowTitle { get; set; }
    public WorkoutItemEditorContext? WorkoutItemEditor { get; set; }

    /// <summary>
    ///     Creates a new instance - this method can be called from any thread and will
    ///     switch to the UI thread as needed. Does not show the window - consider using
    ///     PositionWindowAndShowOnUiThread() from the WindowInitialPositionHelpers.
    /// </summary>
    public static async Task<WorkoutItemEditorWindow> CreateInstance(WorkoutItem? toLoad = null,
        bool positionAndShowWindow = false)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        var window = new WorkoutItemEditorWindow { StatusContext = await StatusControlContext.CreateInstance() };

        if (positionAndShowWindow) window.PositionWindowAndShow();

        await ThreadSwitcher.ResumeBackgroundAsync();

        window.WorkoutItemEditor = await WorkoutItemEditorContext.CreateInstance(window.StatusContext, toLoad);

        window.WorkoutItemEditor.WorkoutTypeEntry.PropertyChanged += (_, _) =>
        {
            window.WindowTitle =
                $"Workout Editor - {UserSettingsSingleton.CurrentSettings().SiteName} - {window.WorkoutItemEditor.WorkoutTypeEntry.UserValue}";
        };

        window.WorkoutItemEditor.RequestContentEditorWindowClose += (_, _) =>
        {
            window.Dispatcher?.Invoke(window.Close);
        };

        window.AccidentalCloserHelper =
            new WindowAccidentalClosureHelper(window, window.StatusContext, window.WorkoutItemEditor);

        await ThreadSwitcher.ResumeForegroundAsync();

        return window;
    }
}
