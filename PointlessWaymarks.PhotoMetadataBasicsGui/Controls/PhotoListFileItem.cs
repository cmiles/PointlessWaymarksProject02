using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Iptc;
using MetadataExtractor.Formats.Xmp;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.SpatialTools;
using XmpCore;

namespace PointlessWaymarks.PhotoMetadataBasicsGui.Controls;

[NotifyPropertyChanged]
public partial class PhotoListFileItem
{
    public bool IsPrimaryPhoto { get; set; }
    public required PhotoListFileMetadata Metadata { get; set; }
    public required FileInfo PhotoFile { get; set; }
    public BitmapSource? ThumbnailImage { get; set; }

    private static BitmapSource ApplyRotation(BitmapSource source, Rotation rotation)
    {
        if (rotation == Rotation.Rotate0) return source;

        var angle = rotation switch
        {
            Rotation.Rotate90 => 90.0,
            Rotation.Rotate180 => 180.0,
            Rotation.Rotate270 => 270.0,
            _ => 0.0
        };

        var transformed = new TransformedBitmap(source, new RotateTransform(angle));
        transformed.Freeze();
        return transformed;
    }

    public static async Task<PhotoListFileItem> CreateInstance(FileInfo photoFile)
    {
        var item = new PhotoListFileItem
        {
            PhotoFile = photoFile,
            Metadata = await GetMetadataAsync(photoFile),
            ThumbnailImage = await GetThumbnailAsync(photoFile)
        };

        return item;
    }

    private static FileInfo? ExtractLargestPreviewAsJpeg(FileInfo file, IProgress<string>? progress = null)
    {
        try
        {
            progress?.Report($"Reading {file.Name} ({file.Length / 1024.0 / 1024.0:N1} MB)...");
            var bytes = File.ReadAllBytes(file.FullName);
            var exifRotation = GetExifRotation(bytes);

            // --- Strategy 1: WPF/WIC full-frame decode ---
            try
            {
                progress?.Report("Attempting WIC decode of full image...");
                using var ms = new MemoryStream(bytes);
                var decoder = BitmapDecoder.Create(ms,
                    BitmapCreateOptions.None, BitmapCacheOption.OnLoad);

                if (decoder.Frames.Count > 0)
                {
                    var frame = decoder.Frames[0];
                    progress?.Report($"WIC decoded {frame.PixelWidth}x{frame.PixelHeight} image, saving preview...");
                    var source = ApplyRotation(frame, exifRotation);
                    return SavePreviewJpeg(source, file.Name);
                }
            }
            catch
            {
                progress?.Report("WIC could not decode this format, scanning for embedded JPEG previews...");
            }

            // --- Strategy 2: Scan for the largest embedded JPEG ---
            BitmapSource? bestSource = null;
            var bestPixelCount = 0L;
            var jpegCount = 0;

            for (var i = 0; i < bytes.Length - 2; i++)
            {
                if (bytes[i] != 0xFF || bytes[i + 1] != 0xD8 || bytes[i + 2] != 0xFF)
                    continue;

                try
                {
                    using var ms = new MemoryStream(bytes, i, bytes.Length - i);
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.StreamSource = ms;
                    bi.EndInit();
                    bi.Freeze();

                    jpegCount++;
                    var pixelCount = (long)bi.PixelWidth * bi.PixelHeight;
                    progress?.Report($"Found embedded JPEG #{jpegCount}: {bi.PixelWidth}x{bi.PixelHeight}");
                    if (pixelCount > bestPixelCount)
                    {
                        bestPixelCount = pixelCount;
                        bestSource = bi;
                    }
                }
                catch
                {
                    // Invalid JPEG at this offset; continue scanning.
                }
            }

            if (bestSource != null)
            {
                progress?.Report(
                    $"Using largest embedded JPEG ({bestSource.PixelWidth}x{bestSource.PixelHeight}), saving preview...");
                var rotated = ApplyRotation(bestSource, exifRotation);
                return SavePreviewJpeg(rotated, file.Name);
            }

            progress?.Report("No viewable preview could be extracted.");
            return null;
        }
        catch (Exception ex)
        {
            progress?.Report($"Error extracting preview: {ex.Message}");
            return null;
        }
    }

    private static Rotation GetExifRotation(byte[] imageBytes)
    {
        try
        {
            using var ms = new MemoryStream(imageBytes);
            var directories = ImageMetadataReader.ReadMetadata(ms);
            var ifd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
            if (ifd0 != null && ifd0.TryGetUInt16(ExifDirectoryBase.TagOrientation, out var orientation))
                return orientation switch
                {
                    3 => Rotation.Rotate180,
                    6 => Rotation.Rotate90,
                    8 => Rotation.Rotate270,
                    _ => Rotation.Rotate0
                };
        }
        catch
        {
            // If orientation can't be read, assume normal
        }

        return Rotation.Rotate0;
    }

    /// <summary>
    ///     Reads photo metadata from the file using MetadataExtractor. Never throws —
    ///     returns a default-valued instance if reading fails.
    ///     Follows the same field priority as PhotoGenerator.PhotoMetadataFromFile:
    ///     - PhotoCreatedBy: EXIF Artist → XMP creator → IPTC ByLine
    ///     - Dates: via FileMetadataEmbeddedTools.CreatedOnLocalAndUtc
    ///     - Location: via FileMetadataEmbeddedTools.LocationFromExif (elevation converted to feet)
    ///     - License: EXIF Copyright → XMP rights → IPTC CopyrightNotice
    ///     - Title: XMP dc:title → IPTC ObjectName → filename without extension
    ///     - Summary: EXIF ImageDescription → IPTC ObjectName → Title
    ///     - Tags: combined XMP/IPTC keywords, comma-separated
    /// </summary>
    public static async Task<PhotoListFileMetadata> GetMetadataAsync(FileInfo file)
    {
        var toReturn = new PhotoListFileMetadata();

        if (!file.Exists) return toReturn;

        try
        {
            var directories = ImageMetadataReader.ReadMetadata(file.FullName);

            var exifIfd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
            var iptc = directories.OfType<IptcDirectory>().FirstOrDefault();
            var xmp = directories.OfType<XmpDirectory>().FirstOrDefault();

            // PhotoCreatedBy
            toReturn.PhotoCreatedBy = exifIfd0?.GetDescription(ExifDirectoryBase.TagArtist) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(toReturn.PhotoCreatedBy))
                toReturn.PhotoCreatedBy =
                    xmp?.XmpMeta?.GetArrayItem(XmpConstants.NsDC, "creator", 1)?.Value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(toReturn.PhotoCreatedBy))
                toReturn.PhotoCreatedBy = iptc?.GetDescription(IptcDirectory.TagByLine) ?? string.Empty;

            // Dates
            var createdOn = await FileMetadataEmbeddedTools.CreatedOnLocalAndUtc(directories)
                .ConfigureAwait(false);
            toReturn.PhotoCreatedOn = createdOn.createdOnLocal ?? DateTime.Now;
            toReturn.PhotoCreatedOnUtc = createdOn.createdOnUtc;

            // Location (elevation stored in feet to match CMS convention)
            var location = await FileMetadataEmbeddedTools.LocationFromExif(
                directories, true,
                createdOn.createdOnUtc ?? createdOn.createdOnLocal,
                null).ConfigureAwait(false);
            toReturn.Latitude = location.Latitude;
            toReturn.Longitude = location.Longitude;
            toReturn.Elevation = location.Elevation is { } elevMeters
                ? elevMeters.MetersToFeet()
                : null;
            toReturn.PhotoDirection = location.PhotoDirection;

            // License
            toReturn.License = exifIfd0?.GetDescription(ExifDirectoryBase.TagCopyright) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(toReturn.License))
                toReturn.License =
                    xmp?.XmpMeta?.GetArrayItem(XmpConstants.NsDC, "rights", 1)?.Value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(toReturn.License))
                toReturn.License = iptc?.GetDescription(IptcDirectory.TagCopyrightNotice) ?? string.Empty;

            // Title
            toReturn.Title = xmp?.XmpMeta?.GetArrayItem(XmpConstants.NsDC, "title", 1)?.Value;
            if (string.IsNullOrWhiteSpace(toReturn.Title))
                toReturn.Title = iptc?.GetDescription(IptcDirectory.TagObjectName) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(toReturn.Title))
                toReturn.Title = Path.GetFileNameWithoutExtension(file.Name);

            // Summary: prefer EXIF ImageDescription, then IPTC ObjectName, then fall back to title
            toReturn.Summary =
                exifIfd0?.GetDescription(ExifDirectoryBase.TagImageDescription) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(toReturn.Summary))
                toReturn.Summary = iptc?.GetDescription(IptcDirectory.TagCaption) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(toReturn.Summary))
                toReturn.Summary = xmp?.XmpMeta?.GetArrayItem(XmpConstants.NsDC, "description", 1)?.Value ??
                                   string.Empty;

            // Rating: XMP xmp:Rating (0-5) → EXIF MicrosoftRating (0-99 scale)
            var rating = 0;
            try
            {
                var xmpRating = xmp?.XmpMeta?.GetPropertyInteger("http://ns.adobe.com/xap/1.0/", "Rating");
                if (xmpRating is > 0 and <= 5) rating = xmpRating.Value;
            }
            catch { /* property missing or not an integer */ }

            if (rating == 0 && exifIfd0 != null &&
                exifIfd0.TryGetUInt16(ExifDirectoryBase.TagRating, out var msRating))
            {
                rating = msRating switch
                {
                    >= 99 => 5,
                    >= 75 => 4,
                    >= 50 => 3,
                    >= 25 => 2,
                    >= 1 => 1,
                    _ => 0
                };
            }

            toReturn.Rating = rating;

            // Tags: combined XMP subject + IPTC keywords, de-duplicated
            var tags = FileMetadataEmbeddedTools.KeywordsFromExif(directories, true);
            toReturn.Tags = tags.Count > 0 ? string.Join(", ", tags) : string.Empty;
        }
        catch
        {
            // Return whatever we managed to fill before the failure.
        }

        return toReturn;
    }


    /// <summary>
    ///     Loads a thumbnail BitmapSource entirely from memory — no file handles are held
    ///     after this method returns. Returns null if no image can be decoded.
    ///     Strategy:
    ///     1. WPF BitmapDecoder with OnLoad cache (handles JPEG, PNG, TIFF, BMP, GIF,
    ///     HEIC, and RAW formats via installed WIC codecs). BitmapFrame.Thumbnail
    ///     surfaces the embedded JPEG preview that cameras embed in RAW files.
    ///     2. If the full frame has no embedded thumbnail, re-decode at reduced size
    ///     using BitmapImage.DecodePixelWidth so the decoder does the downscale.
    ///     3. Fallback: scan the raw bytes for the first JPEG SOI marker (FF D8 FF)
    ///     and decode that — covers RAW formats whose WIC codec is not installed
    ///     but that embed a full-size JPEG preview in the file body.
    ///     All returned BitmapSources are Frozen so they can be used from any thread.
    /// </summary>
    private static async Task<BitmapSource?> GetThumbnailAsync(FileInfo file,
        int thumbnailWidth = 200)
    {
        return await Task.Run(() =>
        {
            try
            {
                // Read the entire file into memory first so no handle is held beyond here.
                var bytes = File.ReadAllBytes(file.FullName);

                // Read EXIF orientation so portrait images are displayed correctly.
                var exifRotation = GetExifRotation(bytes);

                // --- Pass 1: WPF BitmapDecoder ---
                try
                {
                    using var ms = new MemoryStream(bytes);
                    var decoder = BitmapDecoder.Create(ms,
                        BitmapCreateOptions.None,
                        BitmapCacheOption.OnLoad);

                    if (decoder.Frames.Count > 0)
                    {
                        var frame = decoder.Frames[0];

                        // Prefer the embedded thumbnail; cameras and many JPEG files
                        // include one, and it is already a small, ready-to-use image.
                        if (frame.Thumbnail is { } thumb)
                        {
                            var rotatedThumb = ApplyRotation(thumb, exifRotation);
                            if (!rotatedThumb.IsFrozen) rotatedThumb.Freeze();
                            return rotatedThumb;
                        }

                        // No embedded thumbnail — re-decode at reduced width so the
                        // codec does the downscale rather than loading the full image.
                        using var ms2 = new MemoryStream(bytes);
                        var bi = new BitmapImage();
                        bi.BeginInit();
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.StreamSource = ms2;
                        bi.DecodePixelWidth = thumbnailWidth;
                        bi.Rotation = exifRotation;
                        bi.EndInit();
                        bi.Freeze();
                        return bi;
                    }
                }
                catch
                {
                    // WPF / WIC cannot decode this format; fall through to byte scan.
                }

                // --- Pass 2: embedded JPEG byte scan ---
                // Many RAW formats embed a full-size JPEG preview in the file body.
                // Scan for the JPEG SOI marker (FF D8 FF) and try to decode from there.
                for (var i = 0; i < bytes.Length - 2; i++)
                {
                    if (bytes[i] != 0xFF || bytes[i + 1] != 0xD8 || bytes[i + 2] != 0xFF)
                        continue;

                    try
                    {
                        using var ms = new MemoryStream(bytes, i, bytes.Length - i);
                        var bi = new BitmapImage();
                        bi.BeginInit();
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.StreamSource = ms;
                        bi.DecodePixelWidth = thumbnailWidth;
                        bi.Rotation = exifRotation;
                        bi.EndInit();
                        bi.Freeze();
                        return bi;
                    }
                    catch
                    {
                        // Not a valid JPEG at this offset; keep scanning.
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }).ConfigureAwait(false);
    }

    public async Task RefreshMetadata()
    {
        Metadata = await GetMetadataAsync(PhotoFile);
    }

    private static FileInfo SavePreviewJpeg(BitmapSource source, string originalFileName)
    {
        var tempDir = FileLocationTools.TempStorageDirectory();
        var safeName = Path.GetFileNameWithoutExtension(originalFileName);
        var datePart = DateTime.Now.ToString("yyyy-MM-dd");
        var tempPath = Path.Combine(tempDir.FullName,
            $"Preview-{datePart}-{safeName}-{Guid.NewGuid():N}.jpg");

        var encoder = new JpegBitmapEncoder { QualityLevel = 95 };
        encoder.Frames.Add(BitmapFrame.Create(source));

        using var fs = File.Create(tempPath);
        encoder.Save(fs);

        return new FileInfo(tempPath);
    }

    /// <summary>
    ///     Opens a preview of the image in the OS default viewer. For formats the OS
    ///     can display natively (JPEG, PNG, etc.) the file is opened directly. For RAW
    ///     and other formats the largest embedded preview is extracted as a JPEG temp
    ///     file and opened.
    /// </summary>
    public static async Task ShowPreviewInOperatingSystem(FileInfo file, IProgress<string>? progress = null)
    {
        if (!file.Exists)
        {
            progress?.Report($"File not found: {file.FullName}");
            return;
        }

        HashSet<string> nativeViewableExtensions =
            [".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff", ".webp"];

        if (nativeViewableExtensions.Contains(file.Extension.ToLowerInvariant()))
        {
            progress?.Report($"Opening {file.Name} in default viewer...");
            Process.Start(new ProcessStartInfo(file.FullName) { UseShellExecute = true });
            return;
        }

        progress?.Report($"Extracting preview from {file.Name}...");
        var previewFile = await Task.Run(() => ExtractLargestPreviewAsJpeg(file, progress)).ConfigureAwait(false);

        if (previewFile is { Exists: true })
        {
            progress?.Report($"Opening extracted preview: {previewFile.Name}");
            Process.Start(new ProcessStartInfo(previewFile.FullName) { UseShellExecute = true });
        }
        else
        {
            progress?.Report($"Could not generate a preview for {file.Name}.");
        }
    }
}