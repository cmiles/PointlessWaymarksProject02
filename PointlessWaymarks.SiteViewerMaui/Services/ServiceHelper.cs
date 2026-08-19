using Microsoft.Extensions.DependencyInjection;

namespace PointlessWaymarks.SiteViewerMaui.Services;

/// <summary>
///     Small service locator used so that pages created by Shell (via <c>DataTemplate</c> or route
///     navigation) can resolve their view models and dependencies from the MAUI DI container.
/// </summary>
public static class ServiceHelper
{
    public static T GetService<T>() where T : notnull
    {
        var services = IPlatformApplication.Current?.Services
                       ?? throw new InvalidOperationException("The MAUI service provider is not available yet.");
        return services.GetRequiredService<T>();
    }
}
