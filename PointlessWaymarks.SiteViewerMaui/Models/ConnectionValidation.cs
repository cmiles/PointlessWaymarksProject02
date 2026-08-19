using PointlessWaymarks.SiteViewerMaui.S3;

namespace PointlessWaymarks.SiteViewerMaui.Models;

/// <summary>The result of validating a connection profile before saving.</summary>
public readonly record struct ValidationResult(bool IsValid, string? Error)
{
    public static ValidationResult Valid() => new(true, null);
    public static ValidationResult Invalid(string error) => new(false, error);
}

/// <summary>
///     Pure (MAUI-free) validation for a connection profile, mirroring the desktop
///     <c>SecureCloudViewerSettingsEditorContext.SaveSettingsValidation</c>: Name, Site Domain and
///     Provider are required; Access Key and Secret are required; for non-Amazon providers a Service
///     URL is required, while for Amazon no Service URL is needed (it is derived from the region).
/// </summary>
public static class ConnectionValidation
{
    public static ValidationResult Validate(CloudViewerProfile profile, string accessKey, string secret,
        string? serviceUrl)
    {
        if (string.IsNullOrWhiteSpace(profile.Name))
            return ValidationResult.Invalid("Site Name can not be blank");

        if (string.IsNullOrWhiteSpace(profile.SiteDomain))
            return ValidationResult.Invalid("Site Domain can not be blank");

        if (string.IsNullOrWhiteSpace(profile.Provider))
            return ValidationResult.Invalid("Provider can not be blank");

        if (string.IsNullOrWhiteSpace(accessKey) || string.IsNullOrWhiteSpace(secret))
            return ValidationResult.Invalid(
                "Full Valid Cloud Credentials not found - please enter both an Access Key and a Secret");

        if (profile.Provider != nameof(S3Providers.Amazon) && string.IsNullOrWhiteSpace(serviceUrl))
            return ValidationResult.Invalid("Service URL is missing or invalid");

        return ValidationResult.Valid();
    }
}
