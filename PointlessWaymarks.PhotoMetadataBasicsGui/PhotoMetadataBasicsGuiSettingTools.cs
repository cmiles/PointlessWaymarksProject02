using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using PointlessWaymarks.CommonTools;

namespace PointlessWaymarks.PhotoMetadataBasicsGui;

public static class PhotoMetadataBasicsGuiSettingTools
{
    public static async Task<string> CheckAndResolveExifTool(IProgress<string>? progressTracker)
    {
        var currentSettings = ReadSettings();
        progressTracker?.Report("Checking ExifTool configuration...");

        var existingPath = currentSettings.ExifToolPath.TrimNullToEmpty();
        var validExisting = false;

        if (!string.IsNullOrWhiteSpace(existingPath))
        {
            var dir = new DirectoryInfo(existingPath);
            if (dir.Exists)
                if (dir.GetFiles("exiftool*.exe").Any())
                    validExisting = true;
        }

        if (validExisting)
        {
            var existingExe = new DirectoryInfo(existingPath)
                .GetFiles("exiftool*.exe")
                .OrderByDescending(f => f.Name.Equals("exiftool.exe", StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault();

            if (existingExe != null)
            {
                progressTracker?.Report($"ExifTool found in {existingPath}.");
                return existingExe.FullName;
            }
        }

        // Download and unpack ExifTool into the default storage directory.
        var targetDir = FileLocationTools.DefaultExifToolStorageDirectory();

        const string versionUrl =
            "https://oliverbetz.de/cms/files/Artikel/ExifTool-for-Windows/exiftool_latest_version.txt";
        progressTracker?.Report("Fetching latest ExifTool version...");

        string version;
        using (var http = new HttpClient())
        {
            version = (await http.GetStringAsync(versionUrl).ConfigureAwait(false)).Trim();
        }

        var exifToolUrl = $"https://oliverbetz.de/cms/files/Artikel/ExifTool-for-Windows/exiftool-{version}_64.zip";
        var tempZip = UniqueFileTools.UniqueFile(FileLocationTools.DefaultExifToolStorageDirectory(),
            $"exiftool-{version}_64.zip")!;

        progressTracker?.Report($"Downloading ExifTool {version} from {exifToolUrl}...");

        using (var http = new HttpClient())
        await using (var stream = await http.GetStreamAsync(exifToolUrl).ConfigureAwait(false))
        await using (var file = File.Create(tempZip.FullName))
        {
            await stream.CopyToAsync(file).ConfigureAwait(false);
        }

        progressTracker?.Report("Extracting ExifTool...");

        await ZipFile.ExtractToDirectoryAsync(tempZip.FullName, targetDir.FullName, true);

        // The Windows zip typically contains exiftool(-k).exe; rename to exiftool.exe for convenience.
        var extractedExe = targetDir.GetFiles("exiftool*.exe").FirstOrDefault();
        if (extractedExe != null && !extractedExe.Name.Equals("exiftool.exe", StringComparison.OrdinalIgnoreCase))
        {
            var renamedPath = Path.Combine(targetDir.FullName, "exiftool.exe");
            if (File.Exists(renamedPath)) File.Delete(renamedPath);
            extractedExe.MoveTo(renamedPath);
        }
        else if (extractedExe == null)
        {
            return string.Empty;
        }

        var resolvedExe = targetDir.GetFiles("exiftool*.exe")
            .OrderByDescending(f => f.Name.Equals("exiftool.exe", StringComparison.OrdinalIgnoreCase))
            .First();

        currentSettings.ExifToolPath = targetDir.FullName;
        await WriteSettings(currentSettings).ConfigureAwait(false);

        progressTracker?.Report($"ExifTool ready at {resolvedExe.FullName}.");

        return resolvedExe.FullName;
    }

    public static PhotoMetadataBasicsGuiSettings ReadSettings()
    {
        var settingsFileName = Path.Combine(FileLocationTools.DefaultStorageDirectory().FullName,
            "PwPhotoMetadataBasicsSettings.json");
        var settingsFile = new FileInfo(settingsFileName);

        if (settingsFile.Exists)
            return JsonSerializer.Deserialize<PhotoMetadataBasicsGuiSettings>(
                       FileAndFolderTools.ReadAllText(settingsFileName)) ??
                   new PhotoMetadataBasicsGuiSettings();

        File.WriteAllText(settingsFile.FullName, JsonSerializer.Serialize(new PhotoMetadataBasicsGuiSettings()));

        return new PhotoMetadataBasicsGuiSettings();
    }

    public static async Task WriteSettings(PhotoMetadataBasicsGuiSettings settings)
    {
        var settingsFileName = Path.Combine(FileLocationTools.DefaultStorageDirectory().FullName,
            "PwPhotoMetadataBasicsSettings.json");
        var settingsFile = new FileInfo(settingsFileName);

        if (settingsFile.Exists) settingsFile.Delete();

        await using var stream = File.Create(settingsFile.FullName);
        await JsonSerializer.SerializeAsync(stream, settings);
    }
}