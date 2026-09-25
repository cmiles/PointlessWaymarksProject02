using System.IO;
using System.Text.Json;
using PointlessWaymarks.CommonTools;

namespace PointlessWaymarks.MetadataDisplayGui;

public static class MetadataDisplayGuiSettingTools
{
    public static MetadataDisplayGuiSettings ReadSettings()
    {
        var settingsFileName = Path.Combine(FileLocationTools.DefaultStorageDirectory().FullName,
            "PwMetadataDisplaySettings.json");
        var settingsFile = new FileInfo(settingsFileName);

        if (settingsFile.Exists)
            return JsonSerializer.Deserialize<MetadataDisplayGuiSettings>(
                       FileAndFolderTools.ReadAllText(settingsFileName)) ??
                   new MetadataDisplayGuiSettings();

        File.WriteAllText(settingsFile.FullName, JsonSerializer.Serialize(new MetadataDisplayGuiSettings()));

        return new MetadataDisplayGuiSettings();
    }

    public static async Task WriteSettings(MetadataDisplayGuiSettings settings)
    {
        var settingsFileName = Path.Combine(FileLocationTools.DefaultStorageDirectory().FullName,
            "PwMetadataDisplaySettings.json");
        var settingsFile = new FileInfo(settingsFileName);

        if (settingsFile.Exists) settingsFile.Delete();

        await using var stream = File.Create(settingsFile.FullName);
        await JsonSerializer.SerializeAsync(stream, settings);
        await stream.DisposeAsync();
    }
}