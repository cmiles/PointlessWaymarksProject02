using System.Windows;
using System.Windows.Input;

namespace PointedHorizons.Services;

/// <summary>
/// Provides integration and scroll isolation for Leaflet maps and interactive canvas controls
/// within OpenSilver / Photino (WebView2) / WebAssembly host containers.
/// </summary>
public static class LeafletScrollIsolationHelper
{
    public static void ProtectControl(FrameworkElement mapControl, params FrameworkElement[] childContainers)
    {
        if (mapControl != null)
        {
            mapControl.AllowScrollOnTouchMove = false;
            AttachWheelHandler(mapControl);
        }

        if (childContainers != null)
        {
            foreach (var container in childContainers)
            {
                if (container != null)
                {
                    container.AllowScrollOnTouchMove = false;
                    AttachWheelHandler(container);
                }
            }
        }
    }

    private static void AttachWheelHandler(FrameworkElement element)
    {
        if (element == null) return;

        element.MouseWheel += (sender, e) =>
        {
            e.Handled = true;
        };
    }
}
