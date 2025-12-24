using PointlessWaymarks.WindowsTools;

namespace PointlessWaymarks.CmsData.S3;

/// <summary>
///     Helpers for managing Cloud Storage Credentials. Credentials are stored in a Password
///     Vault.
///     If you are working with CMS User Site Settings use the CloudStorageCredentialsFromUserSettings class instead.
/// </summary>
public static class CloudStorageCredentials
{
    /// <summary>
    ///     Retrieves the S3 Credentials associated with this settings file
    /// </summary>
    /// <returns></returns>
    public static string GetS3ServiceUrl(Guid settingsId)
    {
        return PasswordVaultTools.GetCredentials(S3SiteServiceUrlResourceString(settingsId)).password;
    }

    /// <summary>
    ///     Retrieves the S3 Credentials associated with this settings file
    /// </summary>
    /// <returns></returns>
    public static (string accessKey, string secret) GetS3SiteCredentials(Guid settingsId)
    {
        return PasswordVaultTools.GetCredentials(S3SiteCredentialResourceString(settingsId));
    }

    /// <summary>
    ///     Removes all S3 Service URLs associated with this settings file
    /// </summary>
    public static void RemoveS3ServiceUrls(Guid settingsId)
    {
        PasswordVaultTools.RemoveCredentials(S3SiteServiceUrlResourceString(settingsId));
    }

    /// <summary>
    ///     Removes all S3 Credentials associated with this settings file
    /// </summary>
    public static void RemoveS3SiteCredentials(Guid settingsId)
    {
        PasswordVaultTools.RemoveCredentials(S3SiteCredentialResourceString(settingsId));
    }

    /// <summary>
    ///     Returns the Credential Manager Resource Key for the current settings file for S3 Site credentials
    /// </summary>
    /// <returns></returns>
    public static string S3SiteCredentialResourceString(Guid settingsId)
    {
        return
            $"Pointless Waymarks - S3 Credentials - {settingsId}";
    }

    /// <summary>
    ///     Returns the Credential Manager Resource Key for the current settings file for an S3 Service URL
    /// </summary>
    /// <returns></returns>
    public static string S3SiteServiceUrlResourceString(Guid settingsId)
    {
        return
            $"Pointless Waymarks - S3 Service URL - {settingsId}";
    }

    /// <summary>
    ///     Removes any existing S3 Service URLs Saves a new Service URL
    /// </summary>
    /// <param name="serviceUrl"></param>
    /// <param name="settingsId"></param>
    public static void SaveS3ServiceUrl(string serviceUrl, Guid settingsId)
    {
        PasswordVaultTools.SaveCredentials(S3SiteServiceUrlResourceString(settingsId), "Service Url", serviceUrl);
    }

    /// <summary>
    ///     Removes any existing S3 Credentials Associated with this settings file and Saves new Credentials
    /// </summary>
    /// <param name="accessKey"></param>
    /// <param name="secret"></param>
    /// <param name="settingsId"></param>
    public static void SaveS3SiteCredential(string accessKey, string secret, Guid settingsId)
    {
        PasswordVaultTools.SaveCredentials(S3SiteCredentialResourceString(settingsId), accessKey, secret);
    }
}