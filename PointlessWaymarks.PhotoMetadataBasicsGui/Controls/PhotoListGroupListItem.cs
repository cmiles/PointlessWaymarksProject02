using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using MathNet.Numerics;
using Metalama.Patterns.Observability;
using Microsoft.Win32;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.SpatialTools;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.ConversionDataEntry;
using PointlessWaymarks.WpfCommon.FileMetadataDisplay;
using PointlessWaymarks.WpfCommon.Status;
using PointlessWaymarks.WpfCommon.StringDataEntry;
using PointlessWaymarks.WpfCommon.Utility;

namespace PointlessWaymarks.PhotoMetadataBasicsGui.Controls;

[Observable]
[GenerateStatusCommands]
public partial class PhotoListGroupListItem
{
    public required ConversionDataEntryContext<double?> ElevationEntry { get; set; }
    public required ObservableCollection<PhotoListFileItem> Items { get; set; }
    public required ConversionDataEntryContext<double?> LatitudeEntry { get; set; }
    public required StringDataEntryContext LicenseEntry { get; set; }
    public required ConversionDataEntryContext<double?> LongitudeEntry { get; set; }
    public required StringDataEntryContext PhotoCreatedByEntry { get; set; }
    public PhotoListFileItem? SelectedItem { get; set; }
    public List<PhotoListFileItem> SelectedItems { get; set; } = [];
    public required StatusControlContext StatusContext { get; set; }
    public required StringDataEntryContext SummaryEntryContext { get; set; }
    public required TagsEditorContext TagEntryContext { get; set; }

    // Entry contexts exposed as required properties so callers / bindings can use them
    public required StringDataEntryContext TitleEntryContext { get; set; }

    [NonBlockingCommand]
    public async Task AddCopyWriteSymbolToLicense()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();
        var current = LicenseEntry.UserValue.TrimNullToEmpty();
        if (current.Contains("©"))
        {
            await StatusContext.ToastWarning("License already contains a copyright symbol.");
            return;
        }

        LicenseEntry.UserValue = $"© {current}".Trim();
    }

    public async Task AddFiles(List<FileInfo> inputFiles)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var existingPaths = Items
            .Select(x => x.PhotoFile.FullName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var newFiles = inputFiles
            .Where(f => !existingPaths.Contains(f.FullName))
            .ToList();

        if (newFiles.Count == 0) return;

        var itemTasks = newFiles.Select(PhotoListFileItem.CreateInstance);
        var items = await Task.WhenAll(itemTasks).ConfigureAwait(false);

        await ThreadSwitcher.ResumeForegroundAsync();

        foreach (var item in items) Items.Add(item);
    }

    [BlockingCommand]
    public async Task ChooseAndAddFiles()
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        var filePicker = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "Image Files|*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.bmp;*.gif|All Files|*.*"
        };
        if (filePicker.ShowDialog() == true)
        {
            var selectedFiles = filePicker.FileNames.Select(f => new FileInfo(f)).ToList();
            await AddFiles(selectedFiles);
        }
    }

    public static async Task<PhotoListGroupListItem> CreateInstance(
        StatusControlContext? statusContext, List<FileInfo> inputFiles)
    {
        var factoryStatusContext = statusContext ?? await StatusControlContext.CreateInstance(statusContext);

        var itemTasks = inputFiles.Where(x => x.Exists).Select(PhotoListFileItem.CreateInstance);
        var items = await Task.WhenAll(itemTasks);

        var composite = GetCompositeMetadata(items.ToList());

        var titleEntryContext = StringDataEntryContext.CreateInstance();
        titleEntryContext.Title = "Title";
        titleEntryContext.HelpText =
            "Photograph Title";

        var summaryEntryContext = StringDataEntryContext.CreateInstance();
        summaryEntryContext.Title = "Summary";
        summaryEntryContext.HelpText =
            "A summary for the photograph - will sometimes be used as a caption.";

        var tagEntryContext = TagsEditorContext.CreateInstance();
        tagEntryContext.TagsReference = SlugTagTools.TagListParseToSpacedString(composite.Tags);

        var photoCreatedByEntry = StringDataEntryContext.CreateInstance();
        photoCreatedByEntry.Title = "Photo Created By";
        photoCreatedByEntry.HelpText = "Who created the photo";

        var licenseEntry = StringDataEntryContext.CreateInstance();
        licenseEntry.Title = "License";
        licenseEntry.HelpText = "The Photo's License";

        var latitudeEntry =
            await ConversionDataEntryContext<double?>.CreateInstance(
                ConversionDataEntryHelpers.DoubleNullableConversion);
        latitudeEntry.ValidationFunctions = [SpatialValueValidations.LatitudeValidationWithNullOk];
        latitudeEntry.ComparisonFunction = (o, u) => o.IsApproximatelyEqualTo(u?.Round(6), .0000001);
        latitudeEntry.Title = "Latitude";
        latitudeEntry.HelpText = "In DDD.DDDDDD°";

        var longitudeEntry =
            await ConversionDataEntryContext<double?>.CreateInstance(
                ConversionDataEntryHelpers.DoubleNullableConversion);
        longitudeEntry.ValidationFunctions = [SpatialValueValidations.LongitudeValidationWithNullOk];
        longitudeEntry.ComparisonFunction = (o, u) => o.IsApproximatelyEqualTo(u?.Round(6), .0000001);
        longitudeEntry.Title = "Longitude";
        longitudeEntry.HelpText = "In DDD.DDDDDD°";

        var elevationEntry =
            await ConversionDataEntryContext<double?>.CreateInstance(
                ConversionDataEntryHelpers.DoubleNullableConversion);
        elevationEntry.ValidationFunctions = [SpatialValueValidations.ElevationValidation];
        elevationEntry.ComparisonFunction = (o, u) => o.IsApproximatelyEqualTo(u?.Round(0), .1);
        elevationEntry.Title = "Elevation (feet)";
        elevationEntry.HelpText = "Elevation in Feet";

        var factoryReturn = new PhotoListGroupListItem
        {
            Items = new ObservableCollection<PhotoListFileItem>(items),
            StatusContext = factoryStatusContext,
            TitleEntryContext = titleEntryContext,
            SummaryEntryContext = summaryEntryContext,
            TagEntryContext = tagEntryContext,
            PhotoCreatedByEntry = photoCreatedByEntry,
            LicenseEntry = licenseEntry,
            LatitudeEntry = latitudeEntry,
            LongitudeEntry = longitudeEntry,
            ElevationEntry = elevationEntry
        };

        factoryReturn.BuildCommands();

        factoryReturn.DesignateBestGuessPrimary();

        factoryReturn.OverwriteAllEntriesWithCurrentMetadata();

        return factoryReturn;
    }

    private void DesignateBestGuessPrimary()
    {
        // Pick a primary photo: prefer DxO_DeepPRIME processed files with thumbnails, then most recent.
        var primaryCandidate = Items
            .Where(i => i.ThumbnailImage != null)
            .OrderByDescending(i => i.PhotoFile.Name.Contains("DxO_DeepPRIME", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(i => i.PhotoFile.LastWriteTimeUtc)
            .FirstOrDefault();

        if (primaryCandidate != null) primaryCandidate.IsPrimaryPhoto = true;
    }

    [NonBlockingCommand]
    [StopAndWarnIfFirstParameterIsNull]
    public async Task FileMetadataReport(PhotoListFileItem? fileItem)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();
        var window = await FileMetadataDisplayWindow.CreateInstance(fileItem!.PhotoFile.FullName, null);
        await window.PositionWindowAndShowOnUiThread();
    }

    private static PhotoGroupMetadata GetCompositeMetadata(List<PhotoListFileItem> items)
    {
        // Local helpers: pick the most frequent non-blank/null value; ties are broken by
        // first-encountered order because GroupBy preserves insertion order in .NET.
        static string? MostFrequentString(IEnumerable<PhotoListFileItem> source,
            Func<PhotoListFileItem, string?> select)
        {
            return source.Select(select)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .GroupBy(v => v!, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault()?.Key;
        }

        static T? MostFrequentValue<T>(IEnumerable<PhotoListFileItem> source,
            Func<PhotoListFileItem, T?> select) where T : struct
        {
            return source.Select(select)
                .Where(v => v.HasValue)
                .GroupBy(v => v)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault()?.Key;
        }

        // At this point Items are empty; composite metadata will be populated once files are added.
        var composite = new PhotoGroupMetadata
        {
            Tags = SlugTagTools.TagListCleanupToSpacedString(string.Join(",",
                items.Select(x => x.Metadata.Tags))),
            License = MostFrequentString(items, x => x.Metadata.License),
            PhotoCreatedBy = MostFrequentString(items, x => x.Metadata.PhotoCreatedBy),
            Summary = MostFrequentString(items, x => x.Metadata.Summary),
            Title = MostFrequentString(items, x => x.Metadata.Title),
            Elevation = MostFrequentValue(items, x => x.Metadata.Elevation),
            Latitude = MostFrequentValue(items, x => x.Metadata.Latitude),
            Longitude = MostFrequentValue(items, x => x.Metadata.Longitude)
        };
        return composite;
    }

    [NonBlockingCommand]
    [StopAndWarnIfFirstParameterIsNull]
    public async Task MakePrimaryPhoto(PhotoListFileItem? item)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        foreach (var loopItems in Items) loopItems.IsPrimaryPhoto = loopItems == item;
    }

    public void OverwriteAllEntriesWithCurrentMetadata()
    {
        var composite = GetCompositeMetadata(Items.ToList());
        TitleEntryContext.ReferenceValue = composite.Title.TrimNullToEmpty();
        TitleEntryContext.UserValue = composite.Title.TrimNullToEmpty();
        SummaryEntryContext.ReferenceValue = composite.Summary.TrimNullToEmpty();
        SummaryEntryContext.UserValue = composite.Summary.TrimNullToEmpty();
        TagEntryContext.TagsReference = SlugTagTools.TagListParseToSpacedString(composite.Tags);
        TagEntryContext.Tags = composite.Tags ?? string.Empty;
        PhotoCreatedByEntry.ReferenceValue = composite.PhotoCreatedBy.TrimNullToEmpty();
        PhotoCreatedByEntry.UserValue = composite.PhotoCreatedBy.TrimNullToEmpty();
        LicenseEntry.ReferenceValue = composite.License.TrimNullToEmpty();
        LicenseEntry.UserValue = composite.License.TrimNullToEmpty();
        LatitudeEntry.ReferenceValue = composite.Latitude;
        LatitudeEntry.UserText = composite.Latitude?.ToString("F6") ?? string.Empty;
        LongitudeEntry.ReferenceValue = composite.Longitude;
        LongitudeEntry.UserText = composite.Longitude?.ToString("F6") ?? string.Empty;
        ElevationEntry.ReferenceValue = composite.Elevation;
        ElevationEntry.UserText = composite.Elevation?.ToString("N0") ?? string.Empty;

        SetDefaultCreatedByAndLicenseIfPossible();
    }

    public void OverwriteUnChangedEntriesWithCurrentMetadata()
    {
        var composite = GetCompositeMetadata(Items.ToList());
        ResetStringEntry(TitleEntryContext, composite.Title);
        ResetStringEntry(SummaryEntryContext, composite.Summary);
        ResetStringEntry(PhotoCreatedByEntry, composite.PhotoCreatedBy);
        ResetStringEntry(LicenseEntry, composite.License);
        ResetTagsEntry(TagEntryContext, composite.Tags);

        ResetConversionEntry(LatitudeEntry, composite.Latitude, "F6");
        ResetConversionEntry(LongitudeEntry, composite.Longitude, "F6");
        ResetConversionEntry(ElevationEntry, composite.Elevation, "N0");

        SetDefaultCreatedByAndLicenseIfPossible();

        void ResetStringEntry(StringDataEntryContext entry, string? value)
        {
            var newValue = value.TrimNullToEmpty();
            var hadChanges = entry.HasChanges;
            entry.ReferenceValue = newValue;
            if (!hadChanges) entry.UserValue = newValue;
        }

        void ResetTagsEntry(TagsEditorContext entry, string? tags)
        {
            var tagString = tags ?? string.Empty;
            var tagList = SlugTagTools.TagListParseToSpacedString(tagString);
            var hadChanges = entry.HasChanges;
            entry.TagsReference = tagList;
            if (!hadChanges) entry.Tags = tagString;
        }

        void ResetConversionEntry(ConversionDataEntryContext<double?> entry, double? value, string format)
        {
            var hadChanges = entry.HasChanges;
            entry.ReferenceValue = value;
            if (!hadChanges)
                entry.UserText = value?.ToString(format, CultureInfo.InvariantCulture) ?? string.Empty;
        }
    }

    [NonBlockingCommand]
    [StopAndWarnIfFirstParameterIsNull]
    public async Task RemoveFile(PhotoListFileItem? file)
    {
        await ThreadSwitcher.ResumeForegroundAsync();
        Items.Remove(file!);

        if (!Items.Any(x => x.IsPrimaryPhoto)) DesignateBestGuessPrimary();
    }

    [BlockingCommand]
    public async Task RenameFiles()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var primaryPhoto = Items.FirstOrDefault(i => i.IsPrimaryPhoto);

        if (primaryPhoto is null)
        {
            await StatusContext.ToastError("Please designate a primary file before renaming.");
            return;
        }

        var title = TitleEntryContext.UserValue.TrimNullToEmpty();

        if (string.IsNullOrWhiteSpace(title))
        {
            await StatusContext.ToastError("Title is required to rename files.");
            return;
        }

        var baseDirectory = primaryPhoto.PhotoFile.DirectoryName;

        var baseName = SlugTagTools.CreateSlug(false, title, 240);
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            await StatusContext.ToastError("Unable to determine base directory.");
            return;
        }

        var baseDirectoryInfo = new DirectoryInfo(baseDirectory);

        StatusContext.Progress($"Renaming files in {baseDirectoryInfo.FullName}...");

        // 1) Group files by filename without extension.
        var grouped = Items
            .GroupBy(i => Path.GetFileNameWithoutExtension(i.PhotoFile.Name), StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        StatusContext.Progress("Calculated filename groups.");

        // 2) Assign new base names: primary group gets baseName; others get baseName-01, -02, ...
        var primaryGroupKey = Path.GetFileNameWithoutExtension(primaryPhoto.PhotoFile.Name);
        var newBaseNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [primaryGroupKey] = baseName
        };

        var suffix = 1;
        foreach (var group in grouped.Where(g => !g.Key.Equals(primaryGroupKey, StringComparison.OrdinalIgnoreCase)))
        {
            newBaseNames[group.Key] = $"{baseName}-{suffix:00}";
            suffix++;
        }

        StatusContext.Progress("Assigned new base names.");

        // 3) Build target file paths and check for conflicts in the directory.
        var sourcePaths = Items.Select(i => i.PhotoFile.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var targetPaths = Items
            .Select(i =>
            {
                var oldBase = Path.GetFileNameWithoutExtension(i.PhotoFile.Name);
                var newBase = newBaseNames[oldBase];
                return Path.Combine(baseDirectoryInfo.FullName, newBase + i.PhotoFile.Extension);
            })
            .ToList();

        var targetPathSet = targetPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var conflicting = baseDirectoryInfo
            .EnumerateFiles()
            .Where(f => targetPathSet.Contains(f.FullName) && !sourcePaths.Contains(f.FullName))
            .ToList();

        if (conflicting.Any())
        {
            await StatusContext.ToastError("Rename would overwrite existing files. Aborting.");
            return;
        }

        StatusContext.Progress("No conflicts detected; performing renames...");

        // 4) Perform the renames.
        foreach (var item in Items)
        {
            var oldPath = item.PhotoFile.FullName;
            var oldBase = Path.GetFileNameWithoutExtension(item.PhotoFile.Name);
            var newBase = newBaseNames[oldBase];
            var newPath = Path.Combine(baseDirectoryInfo.FullName, newBase + item.PhotoFile.Extension);

            if (oldPath.Equals(newPath, StringComparison.OrdinalIgnoreCase)) continue;

            try
            {
                File.Move(oldPath, newPath);
                item.PhotoFile = new FileInfo(newPath);
            }
            catch (Exception e)
            {
                await StatusContext.ToastError($"Rename failed for {item.PhotoFile.Name}: {e.Message}");
                return;
            }
        }

        StatusContext.Progress("Rename complete.");
    }


    [NonBlockingCommand]
    public async Task RescanMetadataAndOverwrite()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        OverwriteAllEntriesWithCurrentMetadata();

        await StatusContext.ToastSuccess($"Updated Metadata for {TitleEntryContext.UserValue}");
    }

    [NonBlockingCommand]
    public async Task RescanMetadataAndUpdate()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        OverwriteUnChangedEntriesWithCurrentMetadata();

        await StatusContext.ToastSuccess($"Updated Metadata for {TitleEntryContext.UserValue}");
    }

    public void SetDefaultCreatedByAndLicenseIfPossible()
    {
        var currentSettings = PhotoMetadataBasicsGuiSettingTools.ReadSettings();

        if (string.IsNullOrWhiteSpace(LicenseEntry.UserValue) &&
            !string.IsNullOrWhiteSpace(currentSettings.DefaultLicense))
            LicenseEntry.UserValue =
                currentSettings.DefaultLicense.Replace("[CurrentYear]", DateTime.Now.Year.ToString()).Replace(
                    "[PrimaryYear]",
                    Items.FirstOrDefault(x => x.IsPrimaryPhoto)?.Metadata.PhotoCreatedOn?.Year.ToString() ??
                    string.Empty);
        if (string.IsNullOrWhiteSpace(PhotoCreatedByEntry.UserValue) &&
            !string.IsNullOrWhiteSpace(currentSettings.DefaultCreatedBy))
            PhotoCreatedByEntry.UserValue = currentSettings.DefaultCreatedBy;
    }

    [BlockingCommand]
    public async Task WriteMetadata()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        // Resolve ExifTool
        string exifToolExe;
        try
        {
            exifToolExe =
                await PhotoMetadataBasicsGuiSettingTools.CheckAndResolveExifTool(StatusContext.ProgressTracker());
        }
        catch (Exception ex)
        {
            await StatusContext.ToastError($"Error locating ExifTool: {ex.Message}");
            return;
        }

        if (string.IsNullOrWhiteSpace(exifToolExe) || !File.Exists(exifToolExe))
        {
            await StatusContext.ToastError("Unable to locate ExifTool executable.");
            return;
        }

        var supportedExt = FileMetadataTools.ExifToolWriteSupportedExtensions;
        var filesToProcess = Items
            .Select(i => i.PhotoFile)
            .Where(f => supportedExt.Contains(f.Extension.ToUpperInvariant()))
            .ToList();

        if (filesToProcess.Count == 0)
        {
            await StatusContext.ToastError("No files with ExifTool-supported extensions to write.");
            return;
        }

        var errors = new List<string>();

        var commandlineString = string.Empty;

        foreach (var file in filesToProcess)
            try
            {
                StatusContext.Progress($"Writing metadata to {file.Name}...");

                var args = new List<string> { "-overwrite_original" };

                // Title
                var title = TitleEntryContext.UserValue.TrimNullToEmpty();
                args.AddRange([
                    $"-Title={Escape(title)}",
                    $"-XMP:Title={Escape(title)}",
                    $"-IPTC:ObjectName={Escape(title)}"
                ]);

                // Summary / description
                var desc = SummaryEntryContext.UserValue.TrimNullToEmpty();
                args.AddRange([
                    $"-Description={Escape(desc)}",
                    $"-XMP-dc:Description={Escape(desc)}",
                    $"-IPTC:Caption-Abstract={Escape(desc)}"
                ]);

                // Creator / artist
                var creator = PhotoCreatedByEntry.UserValue.TrimNullToEmpty();
                args.AddRange([
                    $"-Artist={Escape(creator)}",
                    $"-XMP-dc:Creator={Escape(creator)}",
                    $"-IPTC:By-line={Escape(creator)}"
                ]);

                var rights = LicenseEntry.UserValue.TrimNullToEmpty();
                args.AddRange([
                    $"-Copyright={Escape(rights)}",
                    $"-XMP-dc:Rights={Escape(rights)}",
                    $"-IPTC:CopyrightNotice={Escape(rights)}"
                ]);

                var tags = SlugTagTools.TagListParseToSpacedString(TagEntryContext.Tags);

                args.Add("-Keywords="); // clear existing
                args.AddRange(tags.Select(t => $"-Keywords={Escape(t)}"));

                // GPS
                args.Add($"-GPSLatitude={LatitudeEntry.UserValue?.Round(6).ToString(CultureInfo.InvariantCulture)}");
                args.Add(
                    $"-GPSLongitude={LongitudeEntry.UserValue?.Round(6).ToString(CultureInfo.InvariantCulture)}");
                args.Add(
                    $"-GPSAltitude={ElevationEntry.UserValue?.FeetToMeters().Round(2).ToString(CultureInfo.InvariantCulture)}");

                args.Add($"\"{file.FullName}\"");

                var argsString = string.Join(" ", args);
                commandlineString = $"{exifToolExe} {argsString}";

                StatusContext.Progress(commandlineString);

                var psi = new ProcessStartInfo(exifToolExe)
                {
                    Arguments = argsString,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                if (proc == null)
                    throw new InvalidOperationException("Failed to start ExifTool process.");

                var stdOut = await proc.StandardOutput.ReadToEndAsync();
                var stdErr = await proc.StandardError.ReadToEndAsync();
                await proc.WaitForExitAsync();

                if (proc.ExitCode != 0)
                    throw new InvalidOperationException($"ExifTool error ({proc.ExitCode}): {stdErr}\n{stdOut}");
            }
            catch (Exception ex)
            {
                errors.Add($"{file.Name}: {ex.Message}{Environment.NewLine}{Environment.NewLine}{commandlineString}");
            }

        if (errors.Any())
        {
            var message = string.Join("\n", errors);
            await StatusContext.ShowMessageWithOkButton("Metadata Write Errors", message);
        }
        else
        {
            await StatusContext.ToastSuccess("Metadata written successfully.");
        }

        foreach (var loopItems in Items) await loopItems.RefreshMetadata();

        OverwriteAllEntriesWithCurrentMetadata();

        static string Escape(string value)
        {
            return $"\"{value.Replace("\"", "\\\"")}\"";
        }
    }
}