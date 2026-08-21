using System.IO;
using System.Text.Json;
using PointlessWaymarks.CmsData;
using PointlessWaymarks.CmsData.S3;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.CommonTools.S3;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.WindowsTools;
using PointlessWaymarks.WpfCommon.Status;

namespace PointlessWaymarks.SiteViewerGui.Controls;

[NotifyPropertyChanged]
public partial class SecureCloudViewerSettings
{
    public string CloudServiceUrl { get; set; } = string.Empty;
    public string CloudViewerAccessKey { get; set; } = string.Empty;
    public string CloudViewerBucket { get; set; } = string.Empty;
    public string CloudViewerProvider { get; set; } = string.Empty;
    public string CloudViewerRegion { get; set; } = string.Empty;
    public string CloudViewerSecret { get; set; } = string.Empty;
    public Guid CloudViewerSettingsId { get; set; } = Guid.NewGuid();
    public string CloudViewerSettingsName { get; set; } = string.Empty;
    public string CloudViewerSiteDomain { get; set; } = string.Empty;
    public string SettingsType { get; set; } = "SecureCloudViewer";

    public static string GetObfuscationKey(FileInfo settingsFile)
    {
        return GetObfuscationKey(settingsFile.FullName);
    }

    public static string GetObfuscationKey(string settingsFileName)
    {
        return PasswordVaultTools.GetCredentials(ObfuscationKeyResourceIdentifier(settingsFileName)).password;
    }

    public static async Task<string> GetOrPromptObfuscationKey(FileInfo settingsFile,
        StatusControlContext? statusContext = null)
    {
        return await GetOrPromptObfuscationKey(settingsFile.FullName, statusContext);
    }

    public static async Task<string> GetOrPromptObfuscationKey(string settingsFileName,
        StatusControlContext? statusContext = null)
    {
        var existingKey = GetObfuscationKey(settingsFileName);
        if (!string.IsNullOrWhiteSpace(existingKey)) return existingKey;

        if (statusContext != null)
        {
            var promptResult = await statusContext.ShowStringEntry("Site Viewer Obfuscation Key",
                $"Please enter an obfuscation/encryption key for your Secure Cloud Viewer settings ({settingsFileName}). This key will be stored in your Windows Password Vault.",
                string.Empty);
            if (promptResult.Item1 && !string.IsNullOrWhiteSpace(promptResult.Item2))
            {
                var cleanedKey = promptResult.Item2.Trim();
                SaveObfuscationKey(settingsFileName, cleanedKey);
                return cleanedKey;
            }
        }

        throw new InvalidOperationException(
            "An obfuscation key is required to encrypt or decrypt Secure Cloud Viewer settings.");
    }

    public static string ObfuscationKeyResourceIdentifier(FileInfo settingsFile)
    {
        return ObfuscationKeyResourceIdentifier(settingsFile.FullName);
    }

    public static string ObfuscationKeyResourceIdentifier(string settingsFileName)
    {
        return $"PointlessWaymarks-SiteViewer-ObfuscationKey-{settingsFileName}";
    }

    public static async Task<SecureCloudViewerSettings> ReadFromSettingsFile(FileInfo fileToRead,
        IProgress<string>? progress = null, StatusControlContext? statusContext = null)
    {
        progress?.Report($"Reading Secure Cloud Settings from {fileToRead.FullName}");

        var fileText = await File.ReadAllTextAsync(fileToRead.FullName);

        var dto = JsonSerializer.Deserialize<EncryptedSecureCloudViewerSettingsDto>(fileText);
        if (dto is null)
            throw new NullReferenceException($"Trying to read Settings from {fileToRead.FullName} returned null");

        var key = await GetOrPromptObfuscationKey(fileToRead, statusContext);

        var result = new SecureCloudViewerSettings
        {
            SettingsType = dto.SettingsType,
            CloudViewerSettingsName = dto.CloudViewerSettingsName.Decrypt(key),
            CloudViewerSiteDomain = dto.CloudViewerSiteDomain.Decrypt(key),
            CloudViewerProvider = dto.CloudViewerProvider.Decrypt(key),
            CloudViewerRegion = dto.CloudViewerRegion.Decrypt(key),
            CloudViewerBucket = dto.CloudViewerBucket.Decrypt(key),
            CloudViewerAccessKey = dto.CloudViewerAccessKey.Decrypt(key),
            CloudViewerSecret = dto.CloudViewerSecret.Decrypt(key),
            CloudServiceUrl = dto.CloudServiceUrl.Decrypt(key)
        };

        var decryptedId = dto.CloudViewerSettingsId.Decrypt(key);
        if (Guid.TryParse(decryptedId, out var id))
        {
            result.CloudViewerSettingsId = id;
        }

        return result;
    }

    public IS3AccountInformation S3AccountInformation()
    {
        Enum.TryParse(CloudViewerProvider, out S3Providers cloudProvider);

        return new S3AccountInformation
        {
            ServiceUrl = cloudProvider == S3Providers.Amazon
                ? () => S3Tools.AmazonServiceUrlFromBucketRegion(CloudViewerRegion)
                : () => CloudServiceUrl,
            AccessKey = () => CloudViewerAccessKey,
            Secret = () => CloudViewerSecret,
            BucketName = () => CloudViewerBucket,
            FullFileNameForJsonUploadInformation = () =>
                Path.Combine(FileLocationTools.TempStorageDirectory().FullName,
                    $"{DateTime.Now:yyyy-MM-dd--HH-mm-ss}---File-Upload-Data.json"),
            FullFileNameForToExcel = () => Path.Combine(FileLocationTools.TempStorageDirectory().FullName,
                $"{DateTime.Now:yyyy-MM-dd--HH-mm-ss}---{FileAndFolderTools.TryMakeFilenameValid("S3UploadItems")}.xlsx"),
            S3Provider = () => cloudProvider
        };
    }

    public static void SaveObfuscationKey(FileInfo settingsFile, string key)
    {
        SaveObfuscationKey(settingsFile.FullName, key);
    }

    public static void SaveObfuscationKey(string settingsFileName, string key)
    {
        PasswordVaultTools.SaveCredentials(ObfuscationKeyResourceIdentifier(settingsFileName), "SiteViewerObfuscationKey", key);
    }

    public static async Task WriteSettings(SecureCloudViewerSettings toWrite, string saveAsFullFilename,
        StatusControlContext? statusContext = null)
    {
        var key = await GetOrPromptObfuscationKey(saveAsFullFilename, statusContext);

        var dto = new EncryptedSecureCloudViewerSettingsDto
        {
            SettingsType = string.IsNullOrWhiteSpace(toWrite.SettingsType) ? "SecureCloudViewer" : toWrite.SettingsType,
            CloudViewerSettingsId = toWrite.CloudViewerSettingsId.ToString().Encrypt(key),
            CloudViewerSettingsName = toWrite.CloudViewerSettingsName.Encrypt(key),
            CloudViewerSiteDomain = toWrite.CloudViewerSiteDomain.Encrypt(key),
            CloudViewerProvider = toWrite.CloudViewerProvider.Encrypt(key),
            CloudViewerRegion = toWrite.CloudViewerRegion.Encrypt(key),
            CloudViewerBucket = toWrite.CloudViewerBucket.Encrypt(key),
            CloudViewerAccessKey = toWrite.CloudViewerAccessKey.Encrypt(key),
            CloudViewerSecret = toWrite.CloudViewerSecret.Encrypt(key),
            CloudServiceUrl = toWrite.CloudServiceUrl.Encrypt(key)
        };

        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(saveAsFullFilename, json);
    }
}

public class EncryptedSecureCloudViewerSettingsDto
{
    public string CloudServiceUrl { get; set; } = string.Empty;
    public string CloudViewerAccessKey { get; set; } = string.Empty;
    public string CloudViewerBucket { get; set; } = string.Empty;
    public string CloudViewerProvider { get; set; } = string.Empty;
    public string CloudViewerRegion { get; set; } = string.Empty;
    public string CloudViewerSecret { get; set; } = string.Empty;
    public string CloudViewerSettingsId { get; set; } = string.Empty;
    public string CloudViewerSettingsName { get; set; } = string.Empty;
    public string CloudViewerSiteDomain { get; set; } = string.Empty;
    public string SettingsType { get; set; } = "SecureCloudViewer";
}