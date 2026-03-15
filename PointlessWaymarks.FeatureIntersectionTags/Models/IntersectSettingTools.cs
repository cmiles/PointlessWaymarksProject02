using System.Text.Json;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.CommonTools.S3;

namespace PointlessWaymarks.FeatureIntersectionTags.Models;

public static class IntersectSettingTools
{
    private static readonly TaskQueue SettingsWriteQueue = new();

    public static async Task<FileInfo> SettingsFile(string? settingsFileFullName)
    {
        var settingsFile = string.IsNullOrWhiteSpace(settingsFileFullName) ? FileLocationTools.DefaultFeatureIntersectSettingsFile() : new FileInfo(settingsFileFullName);

        if (!settingsFile.Exists)
        {
            var blankSettings = new IntersectSettings();
            var serializedSettings =
                JsonSerializer.Serialize(blankSettings, JsonTools.WriteIndentedOptions);
            await File.WriteAllTextAsync(settingsFile.FullName, serializedSettings);
            settingsFile.Refresh();
        }

        return settingsFile;
    }

    public static async Task<IntersectSettings> ReadSettings(string? settingsFileFullName)
    {
        var settingsFile = await SettingsFile(settingsFileFullName);
        var json = FileAndFolderTools.ReadAllText(settingsFile.FullName);
        return JsonSerializer.Deserialize<IntersectSettings>(json) ??
               new IntersectSettings();
    }

    public static async Task WriteSettings(IntersectSettings setting, string? settingsFileFullName)
    {
        var settingsFile = await SettingsFile(settingsFileFullName);
        var serializedSettings = JsonSerializer.Serialize(setting, JsonTools.WriteIndentedOptions);
        SettingsWriteQueue.Enqueue(async () => await File.WriteAllTextAsync(settingsFile.FullName, serializedSettings));
    }

    public static async Task WriteCalTopoApi(string calTopoApiKey, string? settingsFileFullName)
    {
        var settings = await ReadSettings(settingsFileFullName);
        settings.CalTopoApiKey = calTopoApiKey;
        await WriteSettings(settings, settingsFileFullName);
    }


}
