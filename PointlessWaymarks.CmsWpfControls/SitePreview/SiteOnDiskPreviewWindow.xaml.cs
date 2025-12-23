using Microsoft.Web.WebView2.Core;
using PointlessWaymarks.CmsData;
using PointlessWaymarks.CmsData.S3;
using PointlessWaymarks.CmsData.Server;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.Status;
using PointlessWaymarks.WpfCommon.Utility;

namespace PointlessWaymarks.CmsWpfControls.SitePreview;

/// <summary>
///     Interaction logic for SiteOnDiskPreviewWindow.xaml
/// </summary>
[NotifyPropertyChanged]
[StaThreadConstructorGuard]
public partial class SiteOnDiskPreviewWindow
{
    private static PreviewServer? _localPreviewServer;
    private static S3PreviewServer? _remotePreviewServer;

    private SiteOnDiskPreviewWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    public SitePreviewContext? PreviewContext { get; set; }
    public string PreviewServerHost { get; set; } = string.Empty;
    public required string SiteUrl { get; set; }
    public required StatusControlContext StatusContext { get; set; }

    /// <summary>
    ///     Creates a new instance - this method can be called from any thread and will
    ///     switch to the UI thread as needed. Does not show the window - consider using
    ///     PositionWindowAndShowOnUiThread() from the WindowInitialPositionHelpers.
    /// </summary>
    /// <returns></returns>
    public static async Task<SiteOnDiskPreviewWindow> CreateInstance(string initialUrl = "")
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        var window = new SiteOnDiskPreviewWindow
        {
            StatusContext = await StatusControlContext.CreateInstance(),
            SiteUrl = UserSettingsSingleton.CurrentSettings().SiteDomainName
        };

        await ThreadSwitcher.ResumeBackgroundAsync();

        if (_localPreviewServer == null)
        {
            _localPreviewServer = new PreviewServer();

            window.StatusContext.RunFireAndForgetWithToastOnError(async () =>
            {
                await ThreadSwitcher.ResumeBackgroundAsync();
                await _localPreviewServer.StartServer(UserSettingsSingleton.CurrentSettings().SiteDomainName,
                    UserSettingsSingleton.CurrentSettings().LocalSiteRootFullDirectory().FullName);
            });
        }

        window.PreviewServerHost = $"localhost:{_localPreviewServer.ServerPort}";

        window.PreviewContext = new SitePreviewContext(UserSettingsSingleton.CurrentSettings().SiteDomainName,
            UserSettingsSingleton.CurrentSettings().LocalSiteRootFullDirectory().FullName,
            UserSettingsSingleton.CurrentSettings().SiteName, $"localhost:{_localPreviewServer.ServerPort}",
            window.StatusContext,
            initialUrl);

        window.PreviewContext.NewWindowRequestedAction += window.NewWindowRequestedAction;

        return window;
    }

    /// <summary>
    ///     Creates a new instance - this method can be called from any thread and will
    ///     switch to the UI thread as needed. Does not show the window - consider using
    ///     PositionWindowAndShowOnUiThread() from the WindowInitialPositionHelpers.
    /// </summary>
    /// <returns></returns>
    public static async Task<SiteOnDiskPreviewWindow> CreateInstanceS3(string initialUrl = "")
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        var window = new SiteOnDiskPreviewWindow
        {
            StatusContext = await StatusControlContext.CreateInstance(),
            SiteUrl = UserSettingsSingleton.CurrentSettings().SiteDomainName
        };

        await ThreadSwitcher.ResumeBackgroundAsync();

        if (_remotePreviewServer == null)
        {
            // Create with S3 account information
            var s3Info = S3CmsTools.S3AccountInformationFromSettings();
            _remotePreviewServer = new S3PreviewServer(s3Info);


            window.StatusContext.RunFireAndForgetWithToastOnError(async () =>
            {
                await ThreadSwitcher.ResumeBackgroundAsync();
                await _remotePreviewServer.StartServer(UserSettingsSingleton.CurrentSettings().SiteDomainName, "");
            });
        }

        window.PreviewServerHost = $"localhost:{_remotePreviewServer.ServerPort}";

        window.PreviewContext = new SitePreviewContext(UserSettingsSingleton.CurrentSettings().SiteDomainName,
            UserSettingsSingleton.CurrentSettings().LocalSiteRootFullDirectory().FullName,
            UserSettingsSingleton.CurrentSettings().SiteName, $"localhost:{_remotePreviewServer.ServerPort}",
            window.StatusContext,
            initialUrl);

        window.PreviewContext.NewWindowRequestedAction += window.NewWindowRequestedAction;

        return window;
    }

    private async void NewWindowRequestedAction(CoreWebView2NewWindowRequestedEventArgs navigationArgs)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        if (string.IsNullOrWhiteSpace(navigationArgs.Uri)) return;

        var uri = navigationArgs.Uri;

        if (navigationArgs.Uri.Contains(SiteUrl) || navigationArgs.Uri.Contains(PreviewServerHost))
        {
            await ThreadSwitcher.ResumeForegroundAsync();


            StatusContext.RunFireAndForgetBlockingTask(async () =>
            {
                var window = await CreateInstance(uri);
                await window.PositionWindowAndShowOnUiThread();
            });
        }
        else
        {
            ProcessHelpers.OpenUrlInExternalBrowser(uri);
        }

        navigationArgs.Handled = true;
    }
}