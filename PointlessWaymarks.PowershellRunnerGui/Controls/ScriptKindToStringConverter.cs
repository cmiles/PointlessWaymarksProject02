using System.Globalization;
using System.Windows.Data;
using PointlessWaymarks.PowerShellRunnerData;

namespace PointlessWaymarks.PowerShellRunnerGui.Controls;

public class ScriptKindToStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string kind)
        {
            if (!string.IsNullOrWhiteSpace(kind))
            {
                if (kind == nameof(ScriptKind.DotNetSingleFile)) return "C#";
                if (kind == nameof(ScriptKind.PowerShell)) return "PowerShell";
            }
        }

        return "?Unknown";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}