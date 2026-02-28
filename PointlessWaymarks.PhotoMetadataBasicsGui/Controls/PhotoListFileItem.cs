using System.IO;
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
            toReturn.Elevation = location.Elevation is double elevMeters
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
                            if (!thumb.IsFrozen) thumb.Freeze();
                            return thumb;
                        }

                        // No embedded thumbnail — re-decode at reduced width so the
                        // codec does the downscale rather than loading the full image.
                        using var ms2 = new MemoryStream(bytes);
                        var bi = new BitmapImage();
                        bi.BeginInit();
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.StreamSource = ms2;
                        bi.DecodePixelWidth = thumbnailWidth;
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
}