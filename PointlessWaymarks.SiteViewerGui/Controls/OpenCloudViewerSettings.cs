using System.IO;
using IniParser;
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

    //2025/12/23 - The verbose names are a small hedge against collisions if an unexpected or incorrect .ini
    //file is saved to - sections or other mechanisms could help and perhaps should be added, this is enough
    //for now.
    public string CloudViewerProvider { get; set; } = string.Empty;
    public string CloudViewerRegion { get; set; } = string.Empty;
    public string CloudViewerSecret { get; set; } = string.Empty;
    public Guid CloudViewerSettingsId { get; set; } = Guid.NewGuid();
    public string CloudViewerSettingsName { get; set; } = string.Empty;
    public string CloudViewerSiteDomain { get; set; } = string.Empty;
    public string IniType { get; set; } = "OpenCloudViewer";

    public static async Task<OpenCloudViewerSettings> ReadFromSettingsFile(FileInfo fileToRead,
        IProgress<string>? progress = null)
    {
        var iniResult = await UserSettingsUtilities.ReadRawSettingsFromFile(fileToRead, progress);

        if (iniResult is null)
            throw new NullReferenceException($"Trying to read Settings from {fileToRead.FullName} returned null");

        var currentProperties = typeof(OpenCloudViewerSettings).GetProperties().ToList();

        var readResult = new OpenCloudViewerSettings();

        foreach (var loopProperties in currentProperties)
        {
            var propertyExists = iniResult.TryGetKey(loopProperties.Name, out var existingValue);

            if (!propertyExists) continue;

            if (loopProperties.PropertyType == typeof(string))
            {
                loopProperties.SetValue(readResult, existingValue.TrimNullToEmpty());
                continue;
            }

            if (loopProperties.PropertyType == typeof(bool))
            {
                var valueTranslated = bool.TryParse(existingValue, out var translated);

                if (valueTranslated)
                    loopProperties.SetValue(readResult, translated);

                continue;
            }

            if (loopProperties.PropertyType == typeof(double))
            {
                var valueTranslated = double.TryParse(existingValue, out var translated);

                if (valueTranslated)
                    loopProperties.SetValue(readResult, translated);

                continue;
            }

            if (loopProperties.PropertyType == typeof(int))
            {
                var valueTranslated = int.TryParse(existingValue, out var translated);

                if (valueTranslated)
                    loopProperties.SetValue(readResult, translated);

                continue;
            }

            if (loopProperties.PropertyType == typeof(Guid))
            {
                var valueTranslated = Guid.TryParse(existingValue, out var translated);

                if (valueTranslated)
                    loopProperties.SetValue(readResult, translated);

                continue;
            }

            throw new NotSupportedException(
                $"The use of the type {loopProperties.PropertyType} in User Settings is not supported...");
        }

        return readResult;
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
        var currentFile = new FileInfo(saveAsFullFilename);

        if (!currentFile.Exists)
        {
            var fileStream = currentFile.Create();
            fileStream.Close();
        }

        var iniResult = (await UserSettingsUtilities.ReadRawSettingsFromFile(currentFile))!;

        var currentProperties = typeof(OpenCloudViewerSettings).GetProperties().ToList();

        foreach (var loopProperties in currentProperties)
        {
            var propertyExists = iniResult.TryGetKey(loopProperties.Name, out _);

            if (propertyExists)
                iniResult.Global[loopProperties.Name] = loopProperties.GetValue(toWrite)?.ToString();
            else
                iniResult.Global.AddKey(loopProperties.Name, loopProperties.GetValue(toWrite)?.ToString());
        }

        var writer = new FileIniDataParser();

        writer.WriteFile(currentFile.FullName, iniResult);
    }
}