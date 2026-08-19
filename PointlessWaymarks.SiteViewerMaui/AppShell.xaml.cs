using PointlessWaymarks.SiteViewerMaui.Services;
using PointlessWaymarks.SiteViewerMaui.Views;

namespace PointlessWaymarks.SiteViewerMaui;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute(Routes.ConnectionEdit, typeof(ConnectionEditPage));
        Routing.RegisterRoute(Routes.Viewer, typeof(ViewerPage));
    }
}
