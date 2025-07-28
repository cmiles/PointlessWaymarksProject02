using System.Windows;
using PointlessWaymarks.FeatureIntersectionTags.Models;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.Status;
using PointlessWaymarks.WpfCommon.Utility;

namespace PointlessWaymarks.GeoToolsGui.Controls;

/// <summary>
///     Interaction logic for FeatureIntersectResultBrowserWindow.xaml
/// </summary>
[NotifyPropertyChanged]
public partial class FeatureIntersectResultBrowserWindow
{
    public FeatureIntersectResultBrowserWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    public required FeatureIntersectResultBrowserContext ResultBrowserContext { get; set; }

    public required StatusControlContext StatusContext { get; set; }

    public string WindowTitle { get; set; } = "";

    public static async Task<FeatureIntersectResultBrowserWindow> CreateInstanceAndShow(
        IntersectResult result, string windowTitlePostfix)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        var statusContext = await StatusControlContext.CreateInstance();

        var context = await FeatureIntersectResultBrowserContext.CreateInstance(
            statusContext, result);

        var windowTitle = "Feature Intersect Result Browser";

        if (!string.IsNullOrWhiteSpace(windowTitlePostfix))
        {
            windowTitle = $"{windowTitle} - {windowTitlePostfix}";
        }
        
        var window = new FeatureIntersectResultBrowserWindow
        {
            StatusContext = statusContext,
            ResultBrowserContext = context,
            WindowTitle = windowTitle
        };

        window.PositionWindowAndShow();

        return window;
    }
}