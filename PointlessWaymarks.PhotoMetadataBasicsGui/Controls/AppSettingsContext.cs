using Metalama.Patterns.Observability;
using PointlessWaymarks.WpfCommon.Status;

namespace PointlessWaymarks.PhotoMetadataBasicsGui.Controls;

[Observable]
public partial class AppSettingsContext
{
    public required StatusControlContext StatusContext { get; set; }

    public static async Task<AppSettingsContext> CreateInstance(StatusControlContext? statusContext)
    {
        var factoryReturn = new AppSettingsContext
        {
            StatusContext = statusContext ?? StatusControlContext.CreateInstance().Result
        };

        return factoryReturn;
    }
}