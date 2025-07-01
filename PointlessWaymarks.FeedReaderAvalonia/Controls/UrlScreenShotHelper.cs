using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using PointlessWaymarks.AvaloniaCommon.Status;
using PointlessWaymarks.VisualWebWork;

namespace PointlessWaymarks.FeedReaderAvalonia.Controls;

public static class UrlScreenShotHelper
{
    public static async Task GetUrlScreenShot(string url, StatusControlContext statusContext)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            await statusContext.ToastError("Url is blank - unable to take screenshot");
            return;
        }
        
        var screenshotResult = await PlaywrightScreenShot.CaptureScreenshot(url, statusContext.ProgressTracker());

        if (!screenshotResult.Success)
        {
            await statusContext.ToastError(screenshotResult.Message);
            return;
        }

        // Get top level from the current control. Alternatively, you can use Window reference instead.
        var desktopLifetime =
            (IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;

        // Start async operation to open the dialog.
        var file = await desktopLifetime.MainWindow!.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Screenshot",
            FileTypeChoices = [FilePickerFileTypes.ImageJpg],
            SuggestedStartLocation =
                await desktopLifetime.MainWindow!.StorageProvider.TryGetFolderFromPathAsync(FeedReaderGuiSettingTools
                    .GetLastDirectory().FullName)
        });

        if (file is not null)
        {
            await using var stream = await file.OpenWriteAsync();
            await stream.WriteAsync(screenshotResult.ImageBytes!, 0, screenshotResult.ImageBytes!.Length);
        }
    }
}