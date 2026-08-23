using System.IO;
using System.Text.Json;

namespace PointlessWaymarks.SiteViewerGui;

public static class ViewerSettingsTypeHelper
{
    public enum ViewerSettingsTypes
    {
        Unknown = 0,
        PointlessWaymarksCms = 1,
        SecureCloudViewer = 2,
        OpenCloudViewer = 3
    }

    public static ViewerSettingsTypes GetViewerSettingsType(FileInfo fileToCheck)
    {
        if (!fileToCheck.Exists) return ViewerSettingsTypes.Unknown;

        var content = File.ReadAllText(fileToCheck.FullName);
        
        if(fileToCheck.Extension == ".ini") return ViewerSettingsTypes.PointlessWaymarksCms;
        
        if (fileToCheck.Extension == ".json")
        {
            using var jsonDoc = JsonDocument.Parse(content);
            var root = jsonDoc.RootElement;
            if (root.TryGetProperty("SettingsType", out var settingsTypeElement) ||
                root.TryGetProperty("IniType", out settingsTypeElement))
            {
                var typeString = settingsTypeElement.GetString();
                return typeString switch
                {
                    "SecureCloudViewer" => ViewerSettingsTypes.SecureCloudViewer,
                    "OpenCloudViewer" => ViewerSettingsTypes.OpenCloudViewer,
                    _ => ViewerSettingsTypes.Unknown
                };
            }
        }

        return ViewerSettingsTypes.Unknown;
    }
}