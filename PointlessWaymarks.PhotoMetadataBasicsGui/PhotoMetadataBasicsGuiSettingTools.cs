using System.IO;
using System.Text.Json;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.FeatureIntersectionTags.Models;
using PointlessWaymarks.WpfCommon.Status;

namespace PointlessWaymarks.PhotoMetadataBasicsGui;

public static class PhotoMetadataBasicsGuiSettingTools
{
    public static async Task<IntersectSettings> FeatureIntersectSettings(StatusControlContext statusContext)
    {
        var settings = ReadSettings();

        // 1) Try the user-configured settings file
        if (!string.IsNullOrWhiteSpace(settings.FeatureIntersectSettingsFile))
        {
            var userFile = new FileInfo(settings.FeatureIntersectSettingsFile);
            if (userFile.Exists)
                try
                {
                    var json = await File.ReadAllTextAsync(userFile.FullName);
                    var deserialized = JsonSerializer.Deserialize<IntersectSettings>(json);
                    if (deserialized != null) return deserialized;
                }
                catch (Exception e)
                {
                    statusContext.Progress(
                        $"Could not deserialize Feature Intersect Settings from {userFile.FullName}: {e.Message}");
                }
        }

        // 2) Fall back to the default settings file
        var defaultFile = FileLocationTools.DefaultFeatureIntersectSettingsFile();
        if (defaultFile.Exists)
            try
            {
                var json = await File.ReadAllTextAsync(defaultFile.FullName);
                var deserialized = JsonSerializer.Deserialize<IntersectSettings>(json);
                if (deserialized != null) return deserialized;
            }
            catch (Exception e)
            {
                statusContext.Progress(
                    $"Could not deserialize Feature Intersect Settings from {defaultFile.FullName}: {e.Message}");
            }

        // 3) Return new default settings
        return new IntersectSettings();
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