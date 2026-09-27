using System.Windows.Input;
using PointlessWaymarks.SiteViewerMaui.Services;
using PointlessWaymarks.SiteViewerMaui.Storage;

namespace PointlessWaymarks.SiteViewerMaui.ViewModels;

/// <summary>
///     Implemented by the viewer page so the view model can drive the underlying WebView
///     (equivalent to the desktop <c>SitePreviewContext.WebViewGui</c>).
/// </summary>
public interface IViewerControl
{
    void GoBack();
    void GoForward();
    void Reload();
    void GoHome();
}

public class ViewerViewModel : ObservableBase, IQueryAttributable
{
    /// <summary>
    ///     The in-app virtual host. Requests to this host are intercepted and served from S3; it is a
    ///     non-resolvable name so it never leaks to the network.
    /// </summary>
    public const string VirtualHost = "pw.local";

    private readonly ISecureCredentialStore _credentialStore;
    private readonly ProfileRepository _repository;

    private string _currentAddress = "/index.html";
    private string _title = "Viewer";
    private IViewerControl? _control;
    private bool _isLoading;
    private bool _isResourceLoading;
    private double _loadProgress;

    public ViewerViewModel(ProfileRepository repository, ISecureCredentialStore credentialStore)
    {
        _repository = repository;
        _credentialStore = credentialStore;

        BackCommand = new Command(() => _control?.GoBack());
        ForwardCommand = new Command(() => _control?.GoForward());
        HomeCommand = new Command(() => _control?.GoHome());
        RefreshCommand = new Command(() => _control?.Reload());
    }

    public string VirtualHostName => VirtualHost;
    public string SiteDomain { get; private set; } = string.Empty;
    public string HomeUrl => $"https://{VirtualHost}/index.html";
    public S3ContentService? ContentService { get; private set; }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string CurrentAddress
    {
        get => _currentAddress;
        set => SetProperty(ref _currentAddress, value);
    }

    /// <summary>
    ///     True while a top-level page load is in progress. Bound to a non-blocking progress
    ///     indicator overlay in the viewer.
    /// </summary>
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    /// <summary>
    ///     True while one or more resources (images, scripts, styles, etc.) are being retrieved from storage.
    ///     Bound to a subtle activity indicator in the viewer.
    /// </summary>
    public bool IsResourceLoading
    {
        get => _isResourceLoading;
        set => SetProperty(ref _isResourceLoading, value);
    }

    /// <summary>
    ///     Current page load progress in the range 0.0 - 1.0 (driven by the Android
    ///     <c>WebChromeClient.OnProgressChanged</c> 0-100 percentage).
    /// </summary>
    public double LoadProgress
    {
        get => _loadProgress;
        set => SetProperty(ref _loadProgress, value);
    }

    public ICommand BackCommand { get; }
    public ICommand ForwardCommand { get; }
    public ICommand HomeCommand { get; }
    public ICommand RefreshCommand { get; }

    private Guid _pendingProfileId;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("profileId", out var value) && Guid.TryParse(value?.ToString(), out var parsedId))
            _pendingProfileId = parsedId;
    }

    public void AttachControl(IViewerControl control)
    {
        _control = control;
    }

    /// <summary>
    ///     Loads the profile + secrets and builds the <see cref="S3ContentService" />. Returns true when
    ///     a viewable connection was successfully prepared.
    /// </summary>
    public async Task<bool> InitializeAsync()
    {
        if (_pendingProfileId == Guid.Empty) return false;

        var profiles = await _repository.LoadAsync();
        var profile = profiles.FirstOrDefault(x => x.Id == _pendingProfileId);
        if (profile is null) return false;

        var secrets = await _credentialStore.GetAsync(profile.Id);
        var account = profile.S3AccountInformation(secrets.AccessKey, secrets.Secret, secrets.ServiceUrl);

        SiteDomain = profile.SiteDomain;
        Title = string.IsNullOrWhiteSpace(profile.Name) ? "Viewer" : profile.Name;
        OnPropertyChanged(nameof(SiteDomain));

        ContentService?.Dispose();
        ContentService = new S3ContentService(account, profile.SiteDomain, VirtualHost);

        return true;
    }
}
