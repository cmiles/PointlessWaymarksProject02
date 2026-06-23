using System.Diagnostics;
using System.Globalization;
using System.Text;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.CommonTools;

namespace PointlessWaymarks.CmsData.ContentGeneration;

public static class MediaLibraryPictureExifWriter
{
    public static List<string> BuildArguments(PhotoContent photo, List<FileInfo> files)
    {
        return BuildArgumentsInternal(photo.Title, photo.Summary, photo.PhotoCreatedBy, photo.License, photo.Tags,
            photo.Latitude, photo.Longitude,
            photo.Elevation, photo.PhotoDirection,
            photo.CameraMake, photo.CameraModel,
            photo.Lens, photo.FocalLength, photo.Iso, photo.Aperture, photo.ShutterSpeed, photo.PhotoCreatedOn, files);
    }

    public static List<string> BuildArguments(ImageContent image, List<FileInfo> files)
    {
        return BuildArgumentsInternal(image.Title, image.Summary, image.CreatedBy, null, image.Tags,
            image.Latitude, image.Longitude,
            image.Elevation, null, null, null, null, null, null, null, null, image.CreatedOn, files);
    }

    private static List<string> BuildArgumentsInternal(string? title, string? summary, string? createdBy,
        string? license, string? tags, double? latitude, double? longitude, double? elevation, double? direction,
        string? cameraMake, string? cameraModel, string? lens, string? focalLength, int? iso, string? aperture,
        string? shutterSpeed, DateTime createdOn, List<FileInfo> files)
    {
        var args = new List<string> { "-m", "-overwrite_original", "-charset", "iptc=utf8" };

        if (!string.IsNullOrWhiteSpace(title))
        {
            args.Add($"-Title={title}");
            args.Add($"-XMP:Title={title}");
            args.Add($"-IPTC:ObjectName={title}");
        }
        else
        {
            args.Add("-Title=");
            args.Add("-XMP:Title=");
            args.Add("-IPTC:ObjectName=");
        }

        if (!string.IsNullOrWhiteSpace(summary))
        {
            args.Add($"-Description={summary}");
            args.Add($"-XMP-dc:Description={summary}");
            args.Add($"-IPTC:Caption-Abstract={summary}");
        }
        else
        {
            args.Add("-Description=");
            args.Add("-XMP-dc:Description=");
            args.Add("-IPTC:Caption-Abstract=");
        }

        if (!string.IsNullOrWhiteSpace(createdBy))
        {
            args.Add($"-Artist={createdBy}");
            args.Add($"-XMP-dc:Creator={createdBy}");
            args.Add($"-IPTC:By-line={createdBy}");
        }
        else
        {
            args.Add("-Artist=");
            args.Add("-XMP-dc:Creator=");
            args.Add("-IPTC:By-line=");
        }

        if (!string.IsNullOrWhiteSpace(license))
        {
            args.Add($"-Copyright={license}");
            args.Add($"-XMP-dc:Rights={license}");
            args.Add($"-IPTC:CopyrightNotice={license}");
        }
        else
        {
            args.Add("-Copyright=");
            args.Add("-XMP-dc:Rights=");
            args.Add("-IPTC:CopyrightNotice=");
        }

        args.Add("-Keywords=");
        args.Add("-Subject=");
        if (!string.IsNullOrWhiteSpace(tags))
        {
            var tagList = SlugTagTools.TagListParseToSpacedString(tags);
            foreach (var tag in tagList)
            {
                args.Add($"-Keywords={tag}");
                args.Add($"-Subject={tag}");
            }
        }

        if (latitude.HasValue)
        {
            args.Add($"-GPSLatitude*={latitude.Value.ToString(CultureInfo.InvariantCulture)}");
        }
        else
        {
            args.Add("-GPSLatitude*=");
            args.Add("-GPSLatitudeRef=");
        }

        if (longitude.HasValue)
        {
            args.Add($"-GPSLongitude*={longitude.Value.ToString(CultureInfo.InvariantCulture)}");
        }
        else
        {
            args.Add("-GPSLongitude*=");
            args.Add("-GPSLongitudeRef=");
        }

        if (elevation.HasValue)
        {
            args.Add($"-GPSAltitude*={elevation.Value.FeetToMeters().ToString(CultureInfo.InvariantCulture)}");
        }
        else
        {
            args.Add("-GPSAltitude*=");
            args.Add("-GPSAltitudeRef=");
        }

        if (direction.HasValue)
            args.Add($"-GPSImgDirection*={direction.Value.ToString(CultureInfo.InvariantCulture)}");
        else
            args.Add("-GPSImgDirection*=");

        if (!string.IsNullOrWhiteSpace(cameraMake)) args.Add($"-Make={cameraMake}");
        else args.Add("-Make=");

        if (!string.IsNullOrWhiteSpace(cameraModel)) args.Add($"-Model={cameraModel}");
        else args.Add("-Model=");

        if (!string.IsNullOrWhiteSpace(lens)) args.Add($"-LensModel={lens}");
        else args.Add("-LensModel=");

        if (!string.IsNullOrWhiteSpace(focalLength)) args.Add($"-FocalLength={focalLength}");
        else args.Add("-FocalLength=");

        if (iso.HasValue) args.Add($"-ISO={iso.Value}");
        else args.Add("-ISO=");

        if (!string.IsNullOrWhiteSpace(aperture))
        {
            var cleanedAperture = aperture.Trim();
            if (cleanedAperture.StartsWith("f/", StringComparison.OrdinalIgnoreCase) ||
                cleanedAperture.StartsWith("ƒ/", StringComparison.OrdinalIgnoreCase))
                cleanedAperture = cleanedAperture[2..];
            else if (cleanedAperture.StartsWith("f", StringComparison.OrdinalIgnoreCase) ||
                     cleanedAperture.StartsWith("ƒ", StringComparison.OrdinalIgnoreCase))
                cleanedAperture = cleanedAperture[1..];

            args.Add($"-ApertureValue={cleanedAperture}");
            args.Add($"-FNumber={cleanedAperture}");
        }
        else
        {
            args.Add("-ApertureValue=");
            args.Add("-FNumber=");
        }

        if (!string.IsNullOrWhiteSpace(shutterSpeed))
        {
            args.Add($"-ShutterSpeedValue={shutterSpeed}");
            args.Add($"-ExposureTime={shutterSpeed}");
        }
        else
        {
            args.Add("-ShutterSpeedValue=");
            args.Add("-ExposureTime=");
        }

        if (createdOn != default)
        {
            var dateString = createdOn.ToString("yyyy:MM:dd HH:mm:ss");
            args.Add($"-DateTimeOriginal={dateString}");
            args.Add($"-CreateDate={dateString}");
        }
        else
        {
            args.Add("-DateTimeOriginal=");
            args.Add("-CreateDate=");
        }

        foreach (var file in files) args.Add(file.FullName);

        return args;
    }

    public static string GetCommandLinePreview(FileInfo exifToolExe, PhotoContent photo, List<FileInfo> files)
    {
        var args = BuildArguments(photo, files);
        return $"{exifToolExe.FullName} {string.Join(" ", args)}";
    }

    public static string GetCommandLinePreview(FileInfo exifToolExe, ImageContent image, List<FileInfo> files)
    {
        var args = BuildArguments(image, files);
        return $"{exifToolExe.FullName} {string.Join(" ", args)}";
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunExifToolAsync(
        FileInfo exifToolExe, string argsFilePath)
    {
        var psi = new ProcessStartInfo(exifToolExe.FullName)
        {
            Arguments = $"-@ \"{argsFilePath}\"",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi)
                         ?? throw new InvalidOperationException("Failed to start ExifTool process.");

        var stdOut = await proc.StandardOutput.ReadToEndAsync();
        var stdErr = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();

        return (proc.ExitCode, stdOut, stdErr);
    }

    private static async Task<ExifToolWriteResult> WriteToFilesInternalAsync(
        FileInfo exifToolExe,
        List<string> args,
        string commandLinePreview,
        List<FileInfo> files,
        IProgress<string>? progress = null)
    {
        await FileLocationTools.FindDownloadUpdateExifTool();

        var result = new ExifToolWriteResult();

        var existingFiles = new List<FileInfo>();
        foreach (var file in files)
            if (!file.Exists)
                result.Errors.Add($"File {file.FullName} does not exist.");
            else
                existingFiles.Add(file);

        if (existingFiles.Count == 0) return result;

        string? argsFilePath = null;
        try
        {
            progress?.Report($"Writing metadata to {existingFiles.Count} files...");

            argsFilePath = Path.Combine(Path.GetTempPath(), $"exiftool-args-{Guid.NewGuid():N}.txt");
            await File.WriteAllLinesAsync(argsFilePath, args, new UTF8Encoding(false));

            progress?.Report(commandLinePreview);

            var (exitCode, stdOut, stdErr) = await RunExifToolAsync(exifToolExe, argsFilePath);

            if (exitCode != 0)
                throw new InvalidOperationException($"ExifTool error ({exitCode}): {stdErr}\n{stdOut}");

            result.FilesProcessed = existingFiles.Count;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Error writing metadata: {ex.Message}");
        }
        finally
        {
            FileLocationTools.TryDeleteFile(argsFilePath);
        }

        progress?.Report(result.Success
            ? $"Metadata written successfully to {existingFiles.Count} files."
            : $"Metadata writing failed: {string.Join("; ", result.Errors)}");

        return result;
    }

    public static async Task<ExifToolWriteResult> WriteToImageFilesAsync(
        FileInfo exifToolExe,
        ImageContent image,
        List<FileInfo> imageFiles,
        IProgress<string>? progress = null)
    {
        var args = BuildArguments(image, imageFiles);
        var preview = GetCommandLinePreview(exifToolExe, image, imageFiles);

        return await WriteToFilesInternalAsync(exifToolExe, args, preview, imageFiles, progress);
    }

    public static async Task<ExifToolWriteResult> WriteToPhotoFilesAsync(
        FileInfo exifToolExe,
        PhotoContent photo,
        List<FileInfo> photoFiles,
        IProgress<string>? progress = null)
    {
        var args = BuildArguments(photo, photoFiles);
        var preview = GetCommandLinePreview(exifToolExe, photo, photoFiles);

        return await WriteToFilesInternalAsync(exifToolExe, args, preview, photoFiles, progress);
    }
}