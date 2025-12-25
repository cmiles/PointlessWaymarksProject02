using System.IO;
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
        var iniData = UserSettingsUtilities.ReadRawSettingsFromFile(fileToCheck).Result;
        if (iniData is null) return IniTypes.Unknown;
        var iniTypeExists = iniData.TryGetKey("IniType", out var iniTypeValue);
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