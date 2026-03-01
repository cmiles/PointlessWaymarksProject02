using System.IO;
using System.Text.Json;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.WpfCommon.Status;

namespace PointlessWaymarks.PhotoMetadataBasicsGui;

public static class PhotoMetadataBasicsGuiSettingTools
{
    public static async Task<FileInfo?> ExifTool(StatusControlContext statusContext)
    {
        var result = await FileLocationTools.DownloadAndSetupExifTool(progress: statusContext.ProgressTracker());
        if (!result.Success)
        {
            await statusContext.ShowMessageWithOkButton("ExifTool Problem", result.Message);
            return null;
        }

        return result.ExifToolExe;
    }

    public static async Task<FileInfo?> Ffprobe(StatusControlContext statusContext)
    {
        var result = await FileLocationTools.DownloadAndSetupFfmpeg(progress: statusContext.ProgressTracker());
        if (!result.Success)
        {
            await statusContext.ShowMessageWithOkButton("ExifTool Problem", result.Message);
            return null;
        }

        return result.FfprobeExe;
    }

    public static PhotoMetadataBasicsGuiSettings ReadSettings()
    {
        var settingsFileName = Path.Combine(FileLocationTools.DefaultStorageDirectory().FullName,
            "PwPhotoMetadataBasicsSettings.json");
        var settingsFile = new FileInfo(settingsFileName);

        if (settingsFile.Exists)
            return JsonSerializer.Deserialize<PhotoMetadataBasicsGuiSettings>(
                       FileAndFolderTools.ReadAllText(settingsFileName)) ??
                   new PhotoMetadataBasicsGuiSettings();

        File.WriteAllText(settingsFile.FullName, JsonSerializer.Serialize(new PhotoMetadataBasicsGuiSettings()));

        return new PhotoMetadataBasicsGuiSettings();
    }

    public static async Task WriteSettings(PhotoMetadataBasicsGuiSettings settings)
    {
        var settingsFileName = Path.Combine(FileLocationTools.DefaultStorageDirectory().FullName,
            "PwPhotoMetadataBasicsSettings.json");
        var settingsFile = new FileInfo(settingsFileName);

        if (settingsFile.Exists) settingsFile.Delete();

        await using var stream = File.Create(settingsFile.FullName);
        await JsonSerializer.SerializeAsync(stream, settings);
    }
}