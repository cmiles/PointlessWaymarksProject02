using PointlessWaymarks.SiteViewerMaui.Services;
using PointlessWaymarks.SiteViewerMaui.ViewModels;

namespace PointlessWaymarks.SiteViewerMaui.Views;

public partial class ConnectionEditPage : ContentPage
{
    public ConnectionEditPage()
    {
        InitializeComponent();

        // The view model implements IQueryAttributable so Shell can pass the profileId to it.
        BindingContext = ServiceHelper.GetService<ConnectionEditViewModel>();
    }
}
