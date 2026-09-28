using Microsoft.Maui.Storage;

namespace PointlessWaymarks.SiteViewerMaui.Storage;

/// <summary>
///     <see cref="ISecureCredentialStore" /> implementation backed by MAUI <see cref="SecureStorage" />
///     (Android KeyStore-backed). Each secret is stored under a per-profile, per-field key so the
///     access key, secret and (non-Amazon) service URL can be saved and removed independently.
/// </summary>
public class SecureCredentialStore : ISecureCredentialStore
{
    private static string AccessKeyKey(Guid id) => $"pw-siteviewer-accesskey-{id:N}";
    private static string SecretKey(Guid id) => $"pw-siteviewer-secret-{id:N}";
    private static string ServiceUrlKey(Guid id) => $"pw-siteviewer-serviceurl-{id:N}";

    public async Task SaveAsync(Guid id, string accessKey, string secret, string? serviceUrl)
    {
        await SecureStorage.Default.SetAsync(AccessKeyKey(id), accessKey ?? string.Empty);
        await SecureStorage.Default.SetAsync(SecretKey(id), secret ?? string.Empty);

        if (string.IsNullOrWhiteSpace(serviceUrl))
            SecureStorage.Default.Remove(ServiceUrlKey(id));
        else
            await SecureStorage.Default.SetAsync(ServiceUrlKey(id), serviceUrl);
    }

    public async Task<CloudViewerCredentialSet> GetAsync(Guid id)
    {
        var accessKey = await SecureStorage.Default.GetAsync(AccessKeyKey(id)) ?? string.Empty;
        var secret = await SecureStorage.Default.GetAsync(SecretKey(id)) ?? string.Empty;
        var serviceUrl = await SecureStorage.Default.GetAsync(ServiceUrlKey(id));

        return new CloudViewerCredentialSet(accessKey, secret, serviceUrl);
    }

    public Task RemoveAsync(Guid id)
    {
        SecureStorage.Default.Remove(AccessKeyKey(id));
        SecureStorage.Default.Remove(SecretKey(id));
        SecureStorage.Default.Remove(ServiceUrlKey(id));

        return Task.CompletedTask;
    }
}
