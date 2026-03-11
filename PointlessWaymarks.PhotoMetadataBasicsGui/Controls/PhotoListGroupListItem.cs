using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using MathNet.Numerics;
using Metalama.Patterns.Observability;
using Microsoft.Win32;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.SpatialTools;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.ChangesAndValidation;
using PointlessWaymarks.WpfCommon.ConversionDataEntry;
using PointlessWaymarks.WpfCommon.FileMetadataDisplay;
using PointlessWaymarks.WpfCommon.LocationPicker;
using PointlessWaymarks.WpfCommon.Status;
using PointlessWaymarks.WpfCommon.StringDataEntry;
using PointlessWaymarks.WpfCommon.Utility;

namespace PointlessWaymarks.PhotoMetadataBasicsGui.Controls;

[Observable]
[GenerateStatusCommands]
public partial class PhotoListGroupListItem : IHasChanges, ICheckForChangesAndValidation,
    IHasValidationIssues
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
    public required StringDataEntryContext TitleEntryContext { get; set; }

    public void CheckForChangesAndValidationIssues()
    {
        HasChanges = PropertyScanners.ChildPropertiesHaveChanges(this);
        HasValidationIssues = PropertyScanners.ChildPropertiesHaveValidationIssues(this);
    }

    public bool HasChanges { get; set; }

    public bool HasValidationIssues { get; set; }

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

    [BlockingCommand]
    public async Task ChooseLocationOnMap()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var initialLat = LatitudeEntry.UserValue ?? 32.1092;
        var initialLong = LongitudeEntry.UserValue ?? -110.5315;
        var initialElev = ElevationEntry.UserValue;

        var locationWindow = await LocationPickerWindow.CreateInstance(
            initialLat, initialLong, initialElev,
            $"Choose Location - {TitleEntryContext.UserValue.TrimNullToEmpty()}");

        await ThreadSwitcher.ResumeForegroundAsync();

        locationWindow.Owner = Application.Current.MainWindow;
        var result = locationWindow.ShowDialog();

        if (result != true) return;

        await ThreadSwitcher.ResumeBackgroundAsync();

        var picker = locationWindow.LocationPicker!;
        LatitudeEntry.UserText = picker.LatitudeEntry!.UserValue.ToString("F6");
        LongitudeEntry.UserText = picker.LongitudeEntry!.UserValue.ToString("F6");
        if (picker.ElevationEntry?.UserValue is not null)
            ElevationEntry.UserText = picker.ElevationEntry.UserValue.Value.ToString("N0");
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

        PropertyScanners.SubscribeToChildHasChangesAndHasValidationIssues(factoryReturn,
            factoryReturn.CheckForChangesAndValidationIssues);

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


    public async Task<bool> HasValidLatLong()
    {
        if (LongitudeEntry.UserValue is null || LatitudeEntry.UserValue is null) return false;

        if (!(await SpatialValueValidations.LatitudeValidation(LatitudeEntry.UserValue.Value)).Valid) return false;
        if (!(await SpatialValueValidations.LongitudeValidation(LongitudeEntry.UserValue.Value)).Valid) return false;

        return true;
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

    [NonBlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task RemoveSelectedFiles()
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        var frozenSelected = SelectedItems.ToList();

        foreach (var item in frozenSelected) Items.Remove(item);

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

    public PhotoListFileItem? SelectedListItem()
    {
        return SelectedItem;
    }

    public List<PhotoListFileItem> SelectedListItems()
    {
        return SelectedItems;
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

        if (HasValidationIssues)
        {
            await StatusContext.ToastError("Please fix validation issues before writing metadata.");
            return;
        }

        if (!HasChanges)
        {
            var result = await StatusContext.ShowMessageWithYesNoButton("No Changes?",
                "The program is not detecting any changes to write - write anyway?");

            if (!result.Equals("yes", StringComparison.CurrentCultureIgnoreCase))
                return;
        }

        // Resolve ExifTool
        FileInfo? exifToolExe;
        try
        {
            exifToolExe =
                await PhotoMetadataBasicsGuiSettingTools.ExifTool(StatusContext);
        }
        catch (Exception ex)
        {
            await StatusContext.ToastError($"Error locating ExifTool: {ex.Message}");
            return;
        }

        if (exifToolExe is null || !exifToolExe.Exists)
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

        var request = new ExifToolWriteRequest
        {
            Title = TitleEntryContext.UserValue.TrimNullToEmpty(),
            Description = SummaryEntryContext.UserValue.TrimNullToEmpty(),
            Creator = PhotoCreatedByEntry.UserValue.TrimNullToEmpty(),
            Copyright = LicenseEntry.UserValue.TrimNullToEmpty(),
            Keywords = SlugTagTools.TagListParseToSpacedString(TagEntryContext.Tags),
            Latitude = LatitudeEntry.UserValue?.Round(6),
            Longitude = LongitudeEntry.UserValue?.Round(6),
            AltitudeInMeters = ElevationEntry.UserValue?.FeetToMeters().Round(2)
        };

        var writeResult = await ExifToolWriter.WriteMetadataAsync(
            exifToolExe, request, filesToProcess, StatusContext.ProgressTracker());

        if (!writeResult.Success)
        {
            var message = string.Join("\n", writeResult.Errors);
            await StatusContext.ShowMessageWithOkButton("Metadata Write Errors", message);
        }
        else
        {
            await StatusContext.ToastSuccess("Metadata written successfully.");
        }

        foreach (var loopItems in Items) await loopItems.RefreshMetadata();

        OverwriteAllEntriesWithCurrentMetadata();
    }
}