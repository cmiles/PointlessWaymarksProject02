using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.Status;

namespace PointlessWaymarks.PhotoMetadataBasicsGui.Controls;

[NotifyPropertyChanged]
public partial class PhotoPreviewWindow
{
    private const double MaxZoom = 10.0;
    private const double MinZoom = 0.05;
    private const double ZoomStep = 0.25;

    public PhotoPreviewWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    public required PhotoPreviewContext PreviewContext { get; set; }

    public required StatusControlContext StatusContext { get; set; }
    public string WindowTitle { get; set; } = "Photo Preview";

    /// <summary>
    ///     Creates a new instance — can be called from any thread.
    ///     Does not show the window; use PositionWindowAndShowOnUiThread().
    /// </summary>
    public static async Task<PhotoPreviewWindow> CreateInstance()
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        var statusContext = await StatusControlContext.CreateInstance();
        var factoryContext = await PhotoPreviewContext.CreateInstance(statusContext);

        var window = new PhotoPreviewWindow
        {
            StatusContext = statusContext,
            PreviewContext = factoryContext
        };

        await ThreadSwitcher.ResumeBackgroundAsync();

        window.PreviewContext.PreviewImageLoaded += window.OnPreviewImageLoaded;

        await ThreadSwitcher.ResumeForegroundAsync();

        return window;
    }

    private void FitImageToWindow()
    {
        if (PreviewContext.PreviewImage == null) return;

        var viewportWidth = ImageScrollViewer.ViewportWidth;
        var viewportHeight = ImageScrollViewer.ViewportHeight;

        if (viewportWidth <= 0 || viewportHeight <= 0) return;

        var imageWidth = PreviewContext.PreviewImage.PixelWidth;
        var imageHeight = PreviewContext.PreviewImage.PixelHeight;

        if (imageWidth <= 0 || imageHeight <= 0) return;

        var scaleX = viewportWidth / imageWidth;
        var scaleY = viewportHeight / imageHeight;
        PreviewContext.ZoomLevel = Math.Max(Math.Min(scaleX, scaleY), MinZoom);
    }

    private void ImageScrollViewer_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;

        var factor = e.Delta > 0 ? 1.15 : 1.0 / 1.15;
        PreviewContext.ZoomLevel = Math.Clamp(PreviewContext.ZoomLevel * factor, MinZoom, MaxZoom);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        PreviewContext.PreviewImageLoaded -= OnPreviewImageLoaded;
        PreviewContext.Cleanup();
        base.OnClosing(e);
    }

    private void OnPreviewImageLoaded(object? sender, EventArgs e)
    {
        // Calculate fit-to-window zoom on the UI thread after each new image loads
        Dispatcher.InvokeAsync(FitImageToWindow);
    }

    private void ZoomActual_OnClick(object sender, RoutedEventArgs e)
    {
        PreviewContext.ZoomLevel = 1.0;
    }

    private void ZoomFit_OnClick(object sender, RoutedEventArgs e)
    {
        FitImageToWindow();
    }

    private void ZoomIn_OnClick(object sender, RoutedEventArgs e)
    {
        PreviewContext.ZoomLevel = Math.Min(PreviewContext.ZoomLevel + ZoomStep, MaxZoom);
    }

    private void ZoomOut_OnClick(object sender, RoutedEventArgs e)
    {
        PreviewContext.ZoomLevel = Math.Max(PreviewContext.ZoomLevel - ZoomStep, MinZoom);
    }
}