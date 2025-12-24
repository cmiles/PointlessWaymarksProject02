using System.IO;
using PointlessWaymarks.CmsData;

namespace PointlessWaymarks.SiteViewerGui;

public static class IniTypeHelper
{
    public enum IniTypes
    {
        Unknown = 0,
        CloudViewer = 1,
        PointlessWaymarksCms = 2
    }

    public static IniTypes GetIniType(FileInfo fileToCheck)
    {
        var iniData = UserSettingsUtilities.ReadRawSettingsFromFile(fileToCheck).Result;
        if (iniData is null) return IniTypes.Unknown;
        var iniTypeExists = iniData.TryGetKey("IniType", out var iniTypeValue);
        if (!iniTypeExists) return IniTypes.Unknown;
        return iniTypeValue switch
        {
            "CloudViewer" => IniTypes.CloudViewer,
            "PointlessWaymarksCms" => IniTypes.PointlessWaymarksCms,
            _ => IniTypes.Unknown
        };
    }
}