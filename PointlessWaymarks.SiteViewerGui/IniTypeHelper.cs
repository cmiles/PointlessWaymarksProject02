using System.IO;
using System.Text.Json;
using PointlessWaymarks.CmsData;

namespace PointlessWaymarks.SiteViewerGui;

public static class IniTypeHelper
{
    public enum IniTypes
    {
        Unknown = 0,
        PointlessWaymarksCms = 1,
        SecureCloudViewer = 2,
        OpenCloudViewer = 3
    }

    public static IniTypes GetIniType(FileInfo fileToCheck)
    {
        if (!fileToCheck.Exists) return IniTypes.Unknown;

        try
        {
            var content = File.ReadAllText(fileToCheck.FullName);
            if (content.TrimStart().StartsWith("{"))
            {
                using var jsonDoc = JsonDocument.Parse(content);
                var root = jsonDoc.RootElement;
                if (root.TryGetProperty("SettingsType", out var settingsTypeElement) ||
                    root.TryGetProperty("IniType", out settingsTypeElement))
                {
                    var typeString = settingsTypeElement.GetString();
                    return typeString switch
                    {
                        "SecureCloudViewer" => IniTypes.SecureCloudViewer,
                        "OpenCloudViewer" => IniTypes.OpenCloudViewer,
                        "PointlessWaymarksCms" => IniTypes.PointlessWaymarksCms,
                        _ => IniTypes.Unknown
                    };
                }
            }
        }
        catch
        {
            // Fall through to INI parser
        }

        var iniData = UserSettingsUtilities.ReadRawSettingsFromFile(fileToCheck).Result;
        if (iniData is null) return IniTypes.Unknown;
        var iniTypeExists = iniData.TryGetKey("IniType", out var iniTypeValue);
        if (!iniTypeExists) iniTypeExists = iniData.TryGetKey("SettingsType", out iniTypeValue);
        if (!iniTypeExists) return IniTypes.Unknown;
        return iniTypeValue switch
        {
            "PointlessWaymarksCms" => IniTypes.PointlessWaymarksCms,
            "SecureCloudViewer" => IniTypes.SecureCloudViewer,
            "OpenCloudViewer" => IniTypes.OpenCloudViewer,
            _ => IniTypes.Unknown
        };
    }
}