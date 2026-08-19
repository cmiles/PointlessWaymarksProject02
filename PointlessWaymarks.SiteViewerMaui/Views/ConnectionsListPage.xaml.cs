using PointlessWaymarks.SiteViewerMaui.Services;
using PointlessWaymarks.SiteViewerMaui.ViewModels;

namespace PointlessWaymarks.SiteViewerMaui.Views;

public partial class ConnectionsListPage : ContentPage
{
    private readonly ConnectionsListViewModel _viewModel;

    public ConnectionsListPage()
    {
        InitializeComponent();

        _viewModel = ServiceHelper.GetService<ConnectionsListViewModel>();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }
}
