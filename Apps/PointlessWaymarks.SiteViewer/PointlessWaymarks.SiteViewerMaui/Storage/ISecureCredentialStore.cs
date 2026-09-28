namespace PointlessWaymarks.SiteViewerMaui.Storage;

/// <summary>
///     Secure storage for a profile's secrets (access key, secret and - for non-Amazon providers -
///     the service URL). This is the mobile, self-contained replacement for the desktop
///     <c>CloudStorageCredentials</c>/<c>PasswordVaultTools</c> (which use the Windows Password Vault).
///     Secrets are keyed by the profile's <see cref="System.Guid" />.
/// </summary>
public interface ISecureCredentialStore
{
    Task SaveAsync(Guid id, string accessKey, string secret, string? serviceUrl);

    Task<CloudViewerCredentialSet> GetAsync(Guid id);

    Task RemoveAsync(Guid id);
}

/// <summary>The secrets associated with a single profile.</summary>
public readonly record struct CloudViewerCredentialSet(string AccessKey, string Secret, string? ServiceUrl);
