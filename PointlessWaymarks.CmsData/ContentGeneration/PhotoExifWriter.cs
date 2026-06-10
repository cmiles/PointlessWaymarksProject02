using System.Diagnostics;
using System.Globalization;
using System.Text;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.CommonTools;

namespace PointlessWaymarks.CmsData.ContentGeneration;

public static class PhotoExifWriter
{
    public static List<string> BuildArguments(PhotoContent photo, FileInfo file)
    {
        var args = new List<string> { "-m", "-overwrite_original", "-charset", "iptc=utf8" };

        if (!string.IsNullOrWhiteSpace(photo.Title))
        {
            args.Add($"-Title={photo.Title}");
            args.Add($"-XMP:Title={photo.Title}");
            args.Add($"-IPTC:ObjectName={photo.Title}");
        }
        else
        {
            args.Add("-Title=");
            args.Add("-XMP:Title=");
            args.Add("-IPTC:ObjectName=");
        }

        if (!string.IsNullOrWhiteSpace(photo.Summary))
        {
            args.Add($"-Description={photo.Summary}");
            args.Add($"-XMP-dc:Description={photo.Summary}");
            args.Add($"-IPTC:Caption-Abstract={photo.Summary}");
        }
        else
        {
            args.Add("-Description=");
            args.Add("-XMP-dc:Description=");
            args.Add("-IPTC:Caption-Abstract=");
        }

        if (!string.IsNullOrWhiteSpace(photo.PhotoCreatedBy))
        {
            args.Add($"-Artist={photo.PhotoCreatedBy}");
            args.Add($"-XMP-dc:Creator={photo.PhotoCreatedBy}");
            args.Add($"-IPTC:By-line={photo.PhotoCreatedBy}");
        }
        else
        {
            args.Add("-Artist=");
            args.Add("-XMP-dc:Creator=");
            args.Add("-IPTC:By-line=");
        }

        if (!string.IsNullOrWhiteSpace(photo.License))
        {
            args.Add($"-Copyright={photo.License}");
            args.Add($"-XMP-dc:Rights={photo.License}");
            args.Add($"-IPTC:CopyrightNotice={photo.License}");
        }
        else
        {
            args.Add("-Copyright=");
            args.Add("-XMP-dc:Rights=");
            args.Add("-IPTC:CopyrightNotice=");
        }

        args.Add("-Keywords=");
        args.Add("-Subject=");
        if (!string.IsNullOrWhiteSpace(photo.Tags))
        {
            var tags = SlugTagTools.TagListParseToSpacedString(photo.Tags);
            foreach (var tag in tags)
            {
                args.Add($"-Keywords={tag}");
                args.Add($"-Subject={tag}");
            }
        }

        if (photo.Latitude.HasValue)
        {
            args.Add($"-GPSLatitude*={photo.Latitude.Value.ToString(CultureInfo.InvariantCulture)}");
        }
        else
        {
            args.Add("-GPSLatitude*=");
            args.Add("-GPSLatitudeRef=");
        }

        if (photo.Longitude.HasValue)
        {
            args.Add($"-GPSLongitude*={photo.Longitude.Value.ToString(CultureInfo.InvariantCulture)}");
        }
        else
        {
            args.Add("-GPSLongitude*=");
            args.Add("-GPSLongitudeRef=");
        }

        if (photo.Elevation.HasValue)
        {
            args.Add($"-GPSAltitude*={photo.Elevation.Value.FeetToMeters().ToString(CultureInfo.InvariantCulture)}");
        }
        else
        {
            args.Add("-GPSAltitude*=");
            args.Add("-GPSAltitudeRef=");
        }

        if (photo.PhotoDirection.HasValue)
        {
            args.Add($"-GPSImgDirection*={photo.PhotoDirection.Value.ToString(CultureInfo.InvariantCulture)}");
        }
        else
        {
            args.Add("-GPSImgDirection*=");
        }

        if (!string.IsNullOrWhiteSpace(photo.CameraMake)) args.Add($"-Make={photo.CameraMake}");
        else args.Add("-Make=");

        if (!string.IsNullOrWhiteSpace(photo.CameraModel)) args.Add($"-Model={photo.CameraModel}");
        else args.Add("-Model=");

        if (!string.IsNullOrWhiteSpace(photo.Lens)) args.Add($"-LensModel={photo.Lens}");
        else args.Add("-LensModel=");

        if (!string.IsNullOrWhiteSpace(photo.FocalLength)) args.Add($"-FocalLength={photo.FocalLength}");
        else args.Add("-FocalLength=");

        if (photo.Iso.HasValue) args.Add($"-ISO={photo.Iso.Value}");
        else args.Add("-ISO=");

        if (!string.IsNullOrWhiteSpace(photo.Aperture))
        {
            var aperture = photo.Aperture.Trim();
            if (aperture.StartsWith("f/", StringComparison.OrdinalIgnoreCase) ||
                aperture.StartsWith("ƒ/", StringComparison.OrdinalIgnoreCase))
                aperture = aperture[2..];
            else if (aperture.StartsWith("f", StringComparison.OrdinalIgnoreCase) ||
                     aperture.StartsWith("ƒ", StringComparison.OrdinalIgnoreCase))
                aperture = aperture[1..];

            args.Add($"-ApertureValue={aperture}");
            args.Add($"-FNumber={aperture}");
        }
        else
        {
            args.Add("-ApertureValue=");
            args.Add("-FNumber=");
        }

        if (!string.IsNullOrWhiteSpace(photo.ShutterSpeed))
        {
            args.Add($"-ShutterSpeedValue={photo.ShutterSpeed}");
            args.Add($"-ExposureTime={photo.ShutterSpeed}");
        }
        else
        {
            args.Add("-ShutterSpeedValue=");
            args.Add("-ExposureTime=");
        }

        if (photo.PhotoCreatedOn != default)
        {
            var dateString = photo.PhotoCreatedOn.ToString("yyyy:MM:dd HH:mm:ss");
            args.Add($"-DateTimeOriginal={dateString}");
            args.Add($"-CreateDate={dateString}");
        }
        else
        {
            args.Add("-DateTimeOriginal=");
            args.Add("-CreateDate=");
        }

        args.Add(file.FullName);

        return args;
    }

    public static string GetCommandLinePreview(FileInfo exifToolExe, PhotoContent photo, FileInfo file)
    {
        var args = BuildArguments(photo, file);
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

    public static async Task<ExifToolWriteResult> WriteToPhotoFileAsync(
        FileInfo exifToolExe,
        PhotoContent photo,
        FileInfo photoFile,
        IProgress<string>? progress = null)
    {
        var result = new ExifToolWriteResult();

        if (!photoFile.Exists)
        {
            result.Errors.Add($"File {photoFile.FullName} does not exist.");
            return result;
        }

        string? argsFilePath = null;
        try
        {
            progress?.Report($"Writing metadata to {photoFile.Name}...");

            var args = BuildArguments(photo, photoFile);

            argsFilePath = Path.Combine(Path.GetTempPath(), $"exiftool-args-{Guid.NewGuid():N}.txt");
            await File.WriteAllLinesAsync(argsFilePath, args, new UTF8Encoding(false));

            progress?.Report(GetCommandLinePreview(exifToolExe, photo, photoFile));

            var (exitCode, stdOut, stdErr) = await RunExifToolAsync(exifToolExe, argsFilePath);

            if (exitCode != 0)
                throw new InvalidOperationException($"ExifTool error ({exitCode}): {stdErr}\n{stdOut}");

            result.FilesProcessed = 1;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"{photoFile.Name}: {ex.Message}");
        }
        finally
        {
            FileLocationTools.TryDeleteFile(argsFilePath);
        }

        progress?.Report(result.Success
            ? $"Metadata written successfully to {photoFile.Name}."
            : $"Metadata writing failed for {photoFile.Name}: {string.Join("; ", result.Errors)}");

        return result;
    }
}
