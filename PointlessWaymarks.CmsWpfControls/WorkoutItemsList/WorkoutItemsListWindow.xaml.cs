using PointlessWaymarks.CmsData;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.WpfCommon;

namespace PointlessWaymarks.CmsWpfControls.WorkoutItemsList;

/// <summary>
///     Interaction logic for WorkoutItemsListWindow.xaml
/// </summary>
[NotifyPropertyChanged]
public partial class WorkoutItemsListWindow
{
    private WorkoutItemsListWindow(WorkoutItemsListContext toLoad)
    {
        InitializeComponent();
        ListContext = toLoad;
        DataContext = this;
        WindowTitle = $"Workout List - {UserSettingsSingleton.CurrentSettings().SiteName}";
    }

    public WorkoutItemsListContext ListContext { get; set; }
    public string WindowTitle { get; set; }

    /// <summary>
    ///     Creates a new instance - this method can be called from any thread and will
    ///     switch to the UI thread as needed.
    /// </summary>
    /// <returns></returns>
    public static async Task<WorkoutItemsListWindow> CreateInstance(WorkoutItemsListContext? toLoad)
    {
        await ThreadSwitcher.ResumeForegroundAsync();
        var window = new WorkoutItemsListWindow(toLoad ?? await WorkoutItemsListContext.CreateInstance(null));

        return window;
    }
}
