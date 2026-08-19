using PointlessWaymarks.SiteViewerMaui.Services;
using PointlessWaymarks.SiteViewerMaui.ViewModels;

namespace PointlessWaymarks.SiteViewerMaui.Views;

public partial class ViewerPage : ContentPage, IViewerControl
{
    private readonly ViewerViewModel _viewModel;
    private bool _platformReady;

#if ANDROID
    private Android.Webkit.WebView? _platformWebView;
#endif

    public ViewerPage()
    {
        InitializeComponent();

        _viewModel = ServiceHelper.GetService<ViewerViewModel>();
        _viewModel.AttachControl(this);
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        var ready = await _viewModel.InitializeAsync();
        if (!ready)
        {
            await Shell.Current.DisplayAlertAsync("Cannot Open", "The selected connection could not be loaded.", "OK");
            await Shell.Current.GoToAsync("..");
            return;
        }

        SetupPlatformWebView();
    }

    private void SetupPlatformWebView()
    {
        if (_platformReady) return;

#if ANDROID
        if (Web.Handler?.PlatformView is not Android.Webkit.WebView platformView) return;
        if (_viewModel.ContentService is null) return;

        _platformWebView = platformView;

        var settings = platformView.Settings;
        settings.JavaScriptEnabled = true;
        settings.DomStorageEnabled = true;

        platformView.SetWebViewClient(new S3WebViewClient(
            _viewModel.ContentService,
            _viewModel.SiteDomain,
            _viewModel.VirtualHostName,
            address => Dispatcher.Dispatch(() => _viewModel.CurrentAddress = address),
            isLoading => Dispatcher.Dispatch(() => _viewModel.IsResourceLoading = isLoading)));

        platformView.SetWebChromeClient(new S3WebChromeClient(progress =>
            Dispatcher.Dispatch(() =>
            {
                _viewModel.LoadProgress = progress / 100.0;
                _viewModel.IsLoading = progress is > 0 and < 100;
            })));

        platformView.LoadUrl(_viewModel.HomeUrl);
        _platformReady = true;
#endif
    }

    public void GoBack()
    {
#if ANDROID
        if (_platformWebView is { } view && view.CanGoBack()) view.GoBack();
#endif
    }

    public void GoForward()
    {
#if ANDROID
        if (_platformWebView is { } view && view.CanGoForward()) view.GoForward();
#endif
    }

    public void Reload()
    {
#if ANDROID
        _platformWebView?.Reload();
#endif
    }

    public void GoHome()
    {
#if ANDROID
        _platformWebView?.LoadUrl(_viewModel.HomeUrl);
#endif
    }
}
