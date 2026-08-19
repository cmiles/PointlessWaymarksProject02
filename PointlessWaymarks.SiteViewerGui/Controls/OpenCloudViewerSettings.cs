using System.IO;
using System.Text.Json;
using PointlessWaymarks.CmsData;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.CommonTools.S3;
using PointlessWaymarks.LlamaAspects;

namespace PointlessWaymarks.SiteViewerGui.Controls;

[NotifyPropertyChanged]
public partial class OpenCloudViewerSettings
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
    public string SettingsType { get; set; } = "OpenCloudViewer";

    public static async Task<OpenCloudViewerSettings> ReadFromSettingsFile(FileInfo fileToRead,
        IProgress<string>? progress = null)
    {
        progress?.Report($"Reading Open Cloud Settings from {fileToRead.FullName}");

        var fileText = await File.ReadAllTextAsync(fileToRead.FullName);

        var settings = JsonSerializer.Deserialize<OpenCloudViewerSettings>(fileText);

        if (settings is null)
            throw new NullReferenceException($"Trying to read Settings from {fileToRead.FullName} returned null");

        return settings;
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

    public static async Task WriteSettings(OpenCloudViewerSettings toWrite, string saveAsFullFilename)
    {
        var json = JsonSerializer.Serialize(toWrite, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(saveAsFullFilename, json);
    }
}