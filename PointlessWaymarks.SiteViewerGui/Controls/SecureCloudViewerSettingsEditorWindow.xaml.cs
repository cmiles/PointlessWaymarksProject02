using System.ComponentModel;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.Status;

namespace PointlessWaymarks.SiteViewerGui.Controls;

/// <summary>
///     Interaction logic for SecureCloudViewerSettingsEditorWindow.xaml
/// </summary>
[NotifyPropertyChanged]
public partial class SecureCloudViewerSettingsEditorWindow
{
    public EventHandler<SecureCloudViewerSettingsEditorContext>? CloudSettingsSaved;
    private bool _saveSuccessful;

    public SecureCloudViewerSettingsEditorWindow(SecureCloudViewerSettingsEditorContext context,
        StatusControlContext statusContext)
    {
        InitializeComponent();

        StatusContext = statusContext;
        Context = context;

        DataContext = this;
        Closing += CloudViewerSettingsEditorWindow_OnClosing;

        Context.EditFinished += (_, success) =>
        {
            _saveSuccessful = success;
            Dispatcher.BeginInvoke(Close);
        };
    }

    public SecureCloudViewerSettingsEditorContext Context { get; set; }

    public StatusControlContext StatusContext { get; set; }

    private void CloudViewerSettingsEditorWindow_OnClosing(object? sender, CancelEventArgs e)
    {
        try
        {
            // Set DialogResult based on whether save was successful
            if (_saveSuccessful)
            {
                DialogResult = true;
                CloudSettingsSaved?.Invoke(this, Context);
            }
            // If DialogResult hasn't been set and window is closing, it's a cancel
            else if (DialogResult == null)
            {
                DialogResult = false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    public static async Task<SecureCloudViewerSettingsEditorWindow> CreateInstance(SecureCloudViewerSettings settings,
        string settingsFilename)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var factoryStatus = await StatusControlContext.CreateInstance();

        var factoryContext =
            await SecureCloudViewerSettingsEditorContext.CreateInstance(factoryStatus, settings, settingsFilename);

        await ThreadSwitcher.ResumeForegroundAsync();

        var window = new SecureCloudViewerSettingsEditorWindow(factoryContext, factoryStatus);

        return window;
    }

    /// <summary>
    ///     Shows the window as a dialog and returns both the dialog result and the context
    /// </summary>
    /// <returns>Tuple of (DialogResult, Context) where DialogResult is true if saved, false/null if cancelled</returns>
    public (bool? dialogResult, SecureCloudViewerSettingsEditorContext context) ShowDialogAndGetContext()
    {
        var result = ShowDialog();
        return (result, Context);
    }

    /// <summary>
    ///     Shows the window as a dialog asynchronously and returns both the dialog result and the context
    /// </summary>
    /// <returns>Tuple of (DialogResult, Context) where DialogResult is true if saved, false/null if cancelled</returns>
    public async Task<(bool? dialogResult, SecureCloudViewerSettingsEditorContext context)>
        ShowDialogAndGetContextAsync()
    {
        await ThreadSwitcher.ResumeForegroundAsync();
        var result = ShowDialog();
        return (result, Context);
    }
}