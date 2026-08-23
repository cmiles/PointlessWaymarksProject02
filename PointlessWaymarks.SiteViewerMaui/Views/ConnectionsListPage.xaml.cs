using PointlessWaymarks.SiteViewerMaui.Models;
using PointlessWaymarks.SiteViewerMaui.Services;
using PointlessWaymarks.SiteViewerMaui.ViewModels;

namespace PointlessWaymarks.SiteViewerMaui.Views;

public partial class ConnectionsListPage : ContentPage
{
    private readonly ConnectionsListViewModel _viewModel;

    public ConnectionsListPage() : this(ServiceHelper.GetService<ConnectionsListViewModel>())
    {
    }

    public ConnectionsListPage(ConnectionsListViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        BindingContext = _viewModel;
        _viewModel.ScrollToRequested += OnScrollToRequested;
    }

    private void OnScrollToRequested(object? sender, CloudViewerProfile profile)
    {
        Dispatcher.Dispatch(() =>
        {
            try
            {
                ProfilesCollectionView.ScrollTo(profile, position: ScrollToPosition.Start, animate: true);
            }
            catch
            {
                // Ignore scroll failure if layout is in-flight
            }
        });
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }
}
