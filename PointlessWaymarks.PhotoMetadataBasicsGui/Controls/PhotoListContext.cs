using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.Messaging;
using GongSolutions.Wpf.DragDrop;
using Metalama.Patterns.Observability;
using Microsoft.VisualBasic.FileIO;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.FeatureIntersectionTags;
using PointlessWaymarks.FeatureIntersectionTags.Models;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.SpatialTools;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.AppMessages;
using PointlessWaymarks.WpfCommon.FileBasedGeoTagger;
using PointlessWaymarks.WpfCommon.PhotoPreview;
using PointlessWaymarks.WpfCommon.StarRating;
using PointlessWaymarks.WpfCommon.Status;
using PointlessWaymarks.WpfCommon.Utility;
using Serilog;
using Point = NetTopologySuite.Geometries.Point;
using SearchOption = System.IO.SearchOption;

namespace PointlessWaymarks.PhotoMetadataBasicsGui.Controls;

[Observable]
[GenerateStatusCommands]
public partial class PhotoListContext : IDropTarget
{
    private bool _previewFilterUnratedOnly;
    private PhotoPreviewWindow? _previewWindow;
    private PhotoListGroupListItem? _previouslySelectedItem;
    public ICollectionView? FilteredItems { get; set; }
    public StarRatingContext FilterMinimumRatingEntry { get; set; } = StarRatingContext.CreateInstance();
    public bool FilterNoRatingOnly { get; set; }
    public required ObservableCollection<PhotoListGroupListItem> Items { get; set; }
    public PhotoListGroupListItem? SelectedItem { get; set; }
    public List<PhotoListGroupListItem> SelectedItems { get; set; } = [];
    public required StatusControlContext StatusContext { get; set; }

    public void DragOver(IDropInfo dropInfo)
    {
        var files = DragAndDropFilesHelper.DroppedFileNames(dropInfo, true);
        dropInfo.Effects = files.Count > 0 ? DragDropEffects.Copy : DragDropEffects.None;
    }

    public List<PhotoListGroupListItem> CurrentFilteredListItems => FilteredItems?.Cast<PhotoListGroupListItem>().ToList()
                                                                    ?? [];

    public void Drop(IDropInfo dropInfo)
    {
        var directories = DragAndDropFilesHelper.DroppedDirectories(dropInfo);
        if (directories.Count > 0)
        {
            StatusContext.RunBlockingTask(() => ProcessDroppedDirectoriesToFileGroups(directories));
            return;
        }

        var files = DragAndDropFilesHelper.DroppedFiles(dropInfo, FileLocationTools.TempStorageDirectory(),
            true);
        if (files.Count == 0) return;
        StatusContext.RunBlockingTask(() => LoadItems(files));
    }

    [BlockingCommand]
    [StopAndWarnIfFirstParameterIsNull]
    public async Task AddFeatureIntersectTags(PhotoListGroupListItem? toProcess, CancellationToken cancellationToken)
    {
        await AddFeatureIntersectTags([toProcess!], cancellationToken);
    }

    public async Task AddFeatureIntersectTags(List<PhotoListGroupListItem> toProcess,
        CancellationToken cancellationToken)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var errorList = new List<string>();
        var successList = new List<string>();
        var noTagsList = new List<string>();

        var processedCount = 0;

        cancellationToken.ThrowIfCancellationRequested();

        var intersectResults = new List<IntersectResult>();

        var settings = await PhotoMetadataBasicsGuiSettingTools.FeatureIntersectSettings(StatusContext);
        var bufferInFeet = Math.Max((double)(settings.BufferPointsAndLinesByFeet ?? 0M), 50);

        foreach (var loopGroup in toProcess)
        {
            var validLatLong = await loopGroup.HasValidLatLong();
            if (!validLatLong)
            {
                await StatusContext.ToastError("No valid Lat/Long to check?");
                return;
            }

            var point = new Point(loopGroup.LongitudeEntry.UserValue!.Value,
                loopGroup.LatitudeEntry.UserValue!.Value);

            var feature = bufferInFeet <= 0
                ? new Feature(point, new AttributesTable())
                : new Feature(PointTools.CreateCircle(loopGroup.LongitudeEntry.UserValue!.Value,
                    loopGroup.LatitudeEntry.UserValue!.Value, bufferInFeet), new AttributesTable());

            var intersectionResult = new IntersectResult(feature)
            {
                ContentId = loopGroup.ContentId,
                Description = "Photo Basic Metadata Tagging",
                OsmIsInPoints =
                {
                    new Coordinate(loopGroup.LongitudeEntry.UserValue!.Value,
                        loopGroup.LatitudeEntry.UserValue!.Value)
                }
            };

            settings.UseOsmOverpass = true;
            settings.OsmInTagging = true;

            intersectResults.Add(intersectionResult);
        }

        await intersectResults.IntersectionTags(settings,
            cancellationToken,
            StatusContext.ProgressTracker());

        foreach (var loopSelected in toProcess)
        {
            processedCount++;

            var title = string.IsNullOrWhiteSpace(loopSelected.TitleEntryContext.UserValue)
                ? "[Blank Title]"
                : loopSelected.TitleEntryContext.UserValue;

            try
            {
                var taggerResult = intersectResults.Single(x => x.ContentId == loopSelected.ContentId);


                if (!taggerResult.Tags.Any())
                {
                    noTagsList.Add($"{title} - no tags found");
                    StatusContext.Progress(
                        $"Processed - {title} - no tags found - Photo {processedCount} of {toProcess.Count}");
                    continue;
                }

                var taggerTags = taggerResult.Tags.Distinct().ToList();
                var cleanedTaggerTags = SlugTagTools.TagListCleanupToSpacedString(taggerTags);
                var combinedTags =
                    SlugTagTools.TagListCleanupToSpacedString(loopSelected.TagEntryContext.TagsList()
                        .Union(cleanedTaggerTags)
                        .ToList());
                loopSelected.TagEntryContext.Tags = SlugTagTools.TagListJoinToSpacedString(combinedTags);

                successList.Add(
                    $"{title} - found Tags {string.Join(", ", taggerResult.Tags)}");
                StatusContext.Progress(
                    $"Processed - {title} - found Tags {string.Join(", ", taggerResult.Tags)} - Photo {processedCount} of {toProcess.Count}");
            }
            catch (Exception e)
            {
                Log.Error(e,
                    $"Photo Save Error during Selected Photo Feature Intersection Tagging {title}, {loopSelected.ContentId}");
                errorList.Add(
                    $"Save Failed! Photo: {title}, {e.Message}");
            }

            if (cancellationToken.IsCancellationRequested) break;
        }

        if (errorList.Any())
        {
            var bodyBuilder = new StringBuilder();
            bodyBuilder.AppendLine(
                $"There were errors getting Feature Intersection Tags and saving items - Errors: {errorList.Count}, Success: {successList.Count}, No Tags: {noTagsList.Count}.");
            bodyBuilder.AppendLine();
            bodyBuilder.AppendFormat("Errors:");
            bodyBuilder.AppendLine(string.Join(Environment.NewLine, errorList));
            bodyBuilder.AppendLine();
            bodyBuilder.AppendFormat("Successes:");
            bodyBuilder.AppendLine(string.Join(Environment.NewLine, successList));
            bodyBuilder.AppendLine();
            bodyBuilder.AppendFormat("No Tags Found:");
            bodyBuilder.AppendLine(string.Join(Environment.NewLine, noTagsList));

            await StatusContext.ShowMessageWithOkButton("Feature Intersection Errors", bodyBuilder.ToString());
        }
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task AddFeatureIntersectTagsForSelected(CancellationToken cancellationToken)
    {
        await AddFeatureIntersectTags(SelectedItems, cancellationToken);
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task AddFeatureIntersectTagsForAll(CancellationToken cancellationToken)
    {
        await AddFeatureIntersectTags(CurrentFilteredListItems, cancellationToken);
    }

    public static async Task<PhotoListContext> CreateInstance(StatusControlContext? statusContext)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        var factoryReturn = new PhotoListContext
        {
            Items = new ObservableCollection<PhotoListGroupListItem>(),
            StatusContext = statusContext ?? await StatusControlContext.CreateInstance()
        };

        factoryReturn.BuildCommands();

        factoryReturn.FilteredItems = CollectionViewSource.GetDefaultView(factoryReturn.Items);
        factoryReturn.FilteredItems.Filter = o =>
            o is PhotoListGroupListItem item &&
            item.RatingEntry.UserValue >= factoryReturn.FilterMinimumRatingEntry.UserValue &&
            (!factoryReturn.FilterNoRatingOnly || item.RatingEntry.UserValue == 0);

        await ThreadSwitcher.ResumeBackgroundAsync();

        factoryReturn.FilterMinimumRatingEntry.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(StarRatingContext.UserValue))
            {
                factoryReturn.FilteredItems?.Refresh();
                factoryReturn.EnsureSelectedItemVisible();
            }
        };

        factoryReturn.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(FilterNoRatingOnly))
            {
                factoryReturn.FilteredItems?.Refresh();
                factoryReturn.EnsureSelectedItemVisible();
            }
        };

        factoryReturn.Items.CollectionChanged += (_, args) =>
        {
            if (args.NewItems != null)
                foreach (PhotoListGroupListItem newItem in args.NewItems)
                    newItem.RatingEntry.PropertyChanged += factoryReturn.OnItemRatingChanged;

            if (args.OldItems != null)
                foreach (PhotoListGroupListItem oldItem in args.OldItems)
                    oldItem.RatingEntry.PropertyChanged -= factoryReturn.OnItemRatingChanged;

            factoryReturn.FilteredItems?.Refresh();
            factoryReturn.EnsureSelectedItemVisible();
        };

        WeakReferenceMessenger.Default.Register<PhotoPreviewNextItemMessage>(factoryReturn,
            (r, _) => ((PhotoListContext)r).StatusContext.RunFireAndForgetNonBlockingTask(
                ((PhotoListContext)r).OnPreviewNextItem));
        WeakReferenceMessenger.Default.Register<PhotoPreviewPreviousItemMessage>(factoryReturn,
            (r, _) => ((PhotoListContext)r).StatusContext.RunFireAndForgetNonBlockingTask(
                ((PhotoListContext)r).OnPreviewPreviousItem));
        WeakReferenceMessenger.Default.Register<PhotoItemRatingChangedMessage>(factoryReturn,
            (r, m) => ((PhotoListContext)r).StatusContext.RunFireAndForgetNonBlockingTask(() =>
                ((PhotoListContext)r).OnRatingChangedFromPreview(m.Value)));
        WeakReferenceMessenger.Default.Register<PhotoPreviewFilterUnratedMessage>(factoryReturn,
            (r, m) => ((PhotoListContext)r)._previewFilterUnratedOnly = m.Value);

        factoryReturn.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(SelectedItem)) return;

            if (factoryReturn.SelectedItem != null)
                factoryReturn._previouslySelectedItem = factoryReturn.SelectedItem;

            if (factoryReturn._previewWindow != null)
                factoryReturn.SendPreviewRequest();
        };

        return factoryReturn;
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task DeleteAllFilesInSelectedGroups()
    {
        var frozenGroups = SelectedItems.ToList();
        var totalFiles = frozenGroups.Sum(g => g.Items.Count);

        var errors = new List<string>();

        foreach (var group in frozenGroups)
        {
            foreach (var fileItem in group.Items.ToList())
                try
                {
                    FileSystem.DeleteFile(fileItem.PhotoFile.FullName, UIOption.OnlyErrorDialogs,
                        RecycleOption.SendToRecycleBin);
                }
                catch (Exception ex)
                {
                    errors.Add($"{fileItem.PhotoFile.Name}: {ex.Message}");
                }

            await ThreadSwitcher.ResumeForegroundAsync();
            Items.Remove(group);
            await ThreadSwitcher.ResumeBackgroundAsync();
        }

        if (errors.Count > 0)
            await StatusContext.ShowMessageWithOkButton("Delete Errors",
                string.Join(Environment.NewLine, errors));
        else
            await StatusContext.ToastSuccess($"Deleted {totalFiles} file(s) from {frozenGroups.Count} group(s).");
    }

    [BlockingCommand]
    public async Task DeleteGroupsWithNoRating()
    {
        var unratedGroups = CurrentFilteredListItems.Where(x => x.RatingEntry.UserValue < 1).ToList();
        var errors = new List<string>();

        var fileDeleteCount = 0;
        
        foreach (var group in unratedGroups)
        {
            foreach (var fileItem in group.Items.ToList())
                try
                {
                    FileSystem.DeleteFile(fileItem.PhotoFile.FullName, UIOption.OnlyErrorDialogs,
                        RecycleOption.SendToRecycleBin);
                    fileDeleteCount++;
                }
                catch (Exception ex)
                {
                    errors.Add($"{fileItem.PhotoFile.Name}: {ex.Message}");
                }

            await ThreadSwitcher.ResumeForegroundAsync();
            Items.Remove(group);
            await ThreadSwitcher.ResumeBackgroundAsync();
        }

        if (errors.Count > 0)
            await StatusContext.ShowMessageWithOkButton("Delete Errors",
                string.Join(Environment.NewLine, errors));
        else
            await StatusContext.ToastSuccess(
                $"Deleted {fileDeleteCount} file(s) from {unratedGroups.Count} filtered-out group(s).");
    }

    [BlockingCommand]
    public async Task DeleteFilteredOutGroups()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (FilteredItems == null) return;

        var visibleSet = FilteredItems.Cast<PhotoListGroupListItem>().ToHashSet();
        var hiddenGroups = Items.Where(x => !visibleSet.Contains(x)).ToList();

        if (hiddenGroups.Count == 0)
        {
            await StatusContext.ToastWarning("No filtered-out groups to delete.");
            return;
        }

        var fileDeleteCount = 0;
        var errors = new List<string>();

        foreach (var group in hiddenGroups)
        {
            foreach (var fileItem in group.Items.ToList())
                try
                {
                    FileSystem.DeleteFile(fileItem.PhotoFile.FullName, UIOption.OnlyErrorDialogs,
                        RecycleOption.SendToRecycleBin);
                    fileDeleteCount++;
                }
                catch (Exception ex)
                {
                    errors.Add($"{fileItem.PhotoFile.Name}: {ex.Message}");
                }

            await ThreadSwitcher.ResumeForegroundAsync();
            Items.Remove(group);
            await ThreadSwitcher.ResumeBackgroundAsync();
        }

        if (errors.Count > 0)
            await StatusContext.ShowMessageWithOkButton("Delete Errors",
                string.Join(Environment.NewLine, errors));
        else
            await StatusContext.ToastSuccess(
                $"Deleted {fileDeleteCount} file(s) from {hiddenGroups.Count} filtered-out group(s).");
    }

    [BlockingCommand]
    [StopAndWarnIfFirstParameterIsNull]
    public async Task DeleteSelectedFiles(PhotoListGroupListItem? listItem)
    {
        var selectedFiles = listItem!.SelectedItems.ToList();

        if (selectedFiles.Count < 1)
        {
            await StatusContext.ToastWarning("Select at least one file to delete.");
            return;
        }

        var errors = new List<string>();
        var deleted = new List<PhotoListFileItem>();

        foreach (var loopFile in selectedFiles)
            try
            {
                FileSystem.DeleteFile(loopFile.PhotoFile.FullName, UIOption.OnlyErrorDialogs,
                    RecycleOption.SendToRecycleBin);
                deleted.Add(loopFile);
            }
            catch (Exception ex)
            {
                errors.Add($"{loopFile.PhotoFile.Name}: {ex.Message}");
            }

        await ThreadSwitcher.ResumeForegroundAsync();

        foreach (var loopDeleted in deleted)
            listItem.Items.Remove(loopDeleted);

        if (listItem.Items.Count == 0)
            Items.Remove(listItem);

        if (errors.Count > 0)
            await StatusContext.ShowMessageWithOkButton("Delete Errors",
                string.Join(Environment.NewLine, errors));
    }

    /// <summary>
    ///     If the current SelectedItem is no longer visible after a filter refresh,
    ///     selects the next visible item (scanning forward from the previous position).
    ///     Must be called on the UI thread.
    /// </summary>
    private void EnsureSelectedItemVisible()
    {
        if (FilteredItems == null) return;

        var visible = FilteredItems.Cast<PhotoListGroupListItem>().ToHashSet();

        if (SelectedItem != null && visible.Contains(SelectedItem)) return;

        if (visible.Count == 0)
        {
            SelectedItem = null;
            return;
        }

        var reference = SelectedItem ?? _previouslySelectedItem;
        var previousIndex = reference != null ? Items.IndexOf(reference) : 0;
        if (previousIndex < 0) previousIndex = 0;

        for (var i = 0; i < Items.Count; i++)
        {
            var candidate = Items[(previousIndex + i) % Items.Count];
            if (visible.Contains(candidate))
            {
                SelectedItem = candidate;
                return;
            }
        }

        SelectedItem = visible.First();
    }

    /// <summary>
    ///     Extracts a camera sequence identifier token from a filename.
    ///     Matches patterns like _DSC1234, _1234567, _IMG5678, _DSCF0001, etc.
    /// </summary>
    private static string? ExtractCameraIdentifier(string fileName)
    {
        var match = Regex.Match(fileName, @"_[A-Za-z]*\d{3,}", RegexOptions.None);
        return match.Success ? match.Value : null;
    }

    /// <summary>
    ///     Given an anchor file, finds all related files in the same directory by matching
    ///     base name prefixes (bidirectional with separator boundary), camera identifier
    ///     tokens, and numbered suffix variants.
    /// </summary>
    private static List<FileInfo> FindRelatedFiles(FileInfo anchor)
    {
        var dir = anchor.Directory;
        if (dir is not { Exists: true }) return [anchor];

        var baseName = Path.GetFileNameWithoutExtension(anchor.Name);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { anchor.FullName };
        var result = new List<FileInfo> { anchor };

        // Pre-compute the camera identifier token (e.g. _DSC1234, _1234, _IMG5678)
        var cameraToken = ExtractCameraIdentifier(anchor.Name);

        // Pre-compute the numbered suffix root (e.g. "photo" from "photo-1" or "photo_2")
        string? numberRoot = null;
        var suffixMatch = Regex.Match(baseName, @"^(.+)[_-]\d+$");
        if (suffixMatch.Success)
            numberRoot = suffixMatch.Groups[1].Value;

        foreach (var f in dir.GetFiles("*", SearchOption.TopDirectoryOnly))
        {
            if (!seen.Add(f.FullName)) continue;

            var candidateBase = Path.GetFileNameWithoutExtension(f.Name);

            // Same base name, different extension
            if (candidateBase.Equals(baseName, StringComparison.OrdinalIgnoreCase))
            {
                result.Add(f);
                continue;
            }

            // Forward prefix: candidate starts with anchor's base name + separator
            if (candidateBase.Length > baseName.Length
                && candidateBase.StartsWith(baseName, StringComparison.OrdinalIgnoreCase)
                && IsGroupingSeparator(candidateBase[baseName.Length]))
            {
                result.Add(f);
                continue;
            }

            // Reverse prefix: anchor starts with candidate's base name + separator
            // (e.g. anchor "2026_Base-Deep Prime 3" finds "2026_Base")
            if (baseName.Length > candidateBase.Length
                && baseName.StartsWith(candidateBase, StringComparison.OrdinalIgnoreCase)
                && IsGroupingSeparator(baseName[candidateBase.Length]))
            {
                result.Add(f);
                continue;
            }

            // Camera identifier token shared between anchor and candidate
            if (!string.IsNullOrEmpty(cameraToken)
                && f.Name.Contains(cameraToken, StringComparison.OrdinalIgnoreCase))
            {
                result.Add(f);
                continue;
            }

            // Numbered suffix grouping: if anchor is "root-N" or "root_N",
            // also match "root" and "root-M" / "root_M"
            if (numberRoot is not null
                && (candidateBase.Equals(numberRoot, StringComparison.OrdinalIgnoreCase)
                    || Regex.IsMatch(candidateBase, $@"^{Regex.Escape(numberRoot)}[_-]\d+$",
                        RegexOptions.IgnoreCase)))
                result.Add(f);
        }

        return result;
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task GeoTagAllItemsItems(CancellationToken cancellationToken)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        await GeoTagItems(CurrentFilteredListItems.SelectMany(g => g.Items).ToList(), cancellationToken);
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task GeoTagSelectedItems(CancellationToken cancellationToken)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();
        var filesFromSelected = SelectedItems.SelectMany(g => g.Items).ToList();

        await GeoTagItems(filesFromSelected, cancellationToken);
    }

    public async Task GeoTagItems(List<PhotoListFileItem> toTag, CancellationToken cancellationToken)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        List<string>? gpxFiles = null;
        var settings = PhotoMetadataBasicsGuiSettingTools.ReadSettings();
        if (!string.IsNullOrWhiteSpace(settings.DefaultGpxDirectory) && Directory.Exists(settings.DefaultGpxDirectory))
            gpxFiles = Directory.GetFiles(settings.DefaultGpxDirectory, "*.gpx").ToList();

        var window =
            await FileBasedGeoTaggerWindow.CreateInstance(toTag.Select(x => x.PhotoFile.FullName).ToList(),
                initialGpxFiles: gpxFiles);

        window.CloseAfterWrite = true;

        await window.PositionWindowAndShowDialogOnUiThread();
    }

    private FileInfo? GetCurrentPrimaryFile()
    {
        var group = SelectedItem;
        if (group == null) return null;

        var primary = group.Items.FirstOrDefault(x => x.IsPrimaryPhoto) ?? group.Items.FirstOrDefault();
        return primary?.PhotoFile;
    }

    private static FileInfo? GetPrimaryFileForGroup(PhotoListGroupListItem group)
    {
        var primary = group.Items.FirstOrDefault(x => x.IsPrimaryPhoto) ?? group.Items.FirstOrDefault();
        return primary?.PhotoFile;
    }

    private static bool IsGroupingSeparator(char c)
    {
        return c is '-' or ' ';
    }

    [NonBlockingCommand]
    public async Task LaunchPreviewWindow()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var primaryFile = GetCurrentPrimaryFile();
        if (primaryFile == null)
        {
            await StatusContext.ToastWarning("No primary photo selected to preview.");
            return;
        }

        _previewWindow = await PhotoPreviewWindow.CreateInstance(false);
        await _previewWindow.PositionWindowAndShowOnUiThread();

        SendPreviewRequest();
    }

    public async Task LoadItems(List<string> files)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (files.Count < 1)
        {
            await StatusContext.ToastError("No files to load.");
            return;
        }

        StatusContext.Progress($"Loading {files.Count} file(s)...");

        List<FileInfo> confirmedFiles = [];

        foreach (var file in files)
        {
            if (!File.Exists(file)) continue;
            var fullPath = Path.GetFullPath(file);
            if (confirmedFiles.Any(x => x.FullName.Equals(fullPath, StringComparison.OrdinalIgnoreCase))) continue;
            confirmedFiles.Add(new FileInfo(fullPath));
        }

        // If a single file was dropped, expand the set with likely related files in the same folder.
        if (confirmedFiles.Count == 1) confirmedFiles = FindRelatedFiles(confirmedFiles[0]);

        var group = await PhotoListGroupListItem.CreateInstance(StatusContext, confirmedFiles);

        await ThreadSwitcher.ResumeForegroundAsync();
        Items.Add(group);
        StatusContext.Progress($"Added group with {group.Items.Count} item(s).");
    }

    [BlockingCommand]
    public async Task MergeSelectedGroups()
    {
        if (SelectedItems.Count < 2)
        {
            await StatusContext.ToastWarning("Select at least two groups to merge.");
            return;
        }

        var frozenSelectedItems = SelectedItems.ToList();
        var firstItem = frozenSelectedItems[0];

        for (var i = 1; i < frozenSelectedItems.Count; i++)
        {
            await firstItem.AddFiles(frozenSelectedItems[i].Items.Select(x => x.PhotoFile).ToList());

            await RemoveGroup(frozenSelectedItems[i]);
        }
    }

    private void OnItemRatingChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StarRatingContext.UserValue))
            StatusContext.RunNonBlockingTask(async () =>
            {
                await ThreadSwitcher.ResumeForegroundAsync();
                FilteredItems?.Refresh();
                EnsureSelectedItemVisible();
            });
    }

    private async Task OnPreviewNextItem()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (SelectedItem == null || FilteredItems == null) return;

        var visible = FilteredItems.Cast<PhotoListGroupListItem>().ToList();
        if (visible.Count == 0) return;

        var currentIndex = visible.IndexOf(SelectedItem);
        if (currentIndex < 0) return;

        for (var i = 1; i <= visible.Count; i++)
        {
            var candidate = visible[(currentIndex + i) % visible.Count];

            if (_previewFilterUnratedOnly && candidate.RatingEntry.UserValue > 0) continue;

            await ThreadSwitcher.ResumeForegroundAsync();
            SelectedItem = candidate;
            return;
        }
    }

    private async Task OnPreviewPreviousItem()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (SelectedItem == null || FilteredItems == null) return;

        var visible = FilteredItems.Cast<PhotoListGroupListItem>().ToList();
        if (visible.Count == 0) return;

        var currentIndex = visible.IndexOf(SelectedItem);
        if (currentIndex < 0) return;

        for (var i = 1; i <= visible.Count; i++)
        {
            var candidate = visible[(currentIndex - i + visible.Count) % visible.Count];

            if (_previewFilterUnratedOnly && candidate.RatingEntry.UserValue > 0) continue;

            await ThreadSwitcher.ResumeForegroundAsync();
            SelectedItem = candidate;
            return;
        }
    }

    private async Task OnRatingChangedFromPreview(PhotoItemRatingChangedData data)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (SelectedItem == null) return;

        var primaryFile = GetCurrentPrimaryFile();
        if (primaryFile == null) return;

        if (string.Equals(primaryFile.FullName, data.FullFilePath, StringComparison.OrdinalIgnoreCase))
        {
            SelectedItem.RatingEntry.UserValue = data.Rating;
            await ThreadSwitcher.ResumeForegroundAsync();
            FilteredItems?.Refresh();
            EnsureSelectedItemVisible();
        }
    }

    public async Task ProcessDroppedDirectoriesToFileGroups(List<string> directories)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        StatusContext.Progress($"Processing {directories.Count} dropped directories...");

        var fileGroups = new List<List<string>>();

        foreach (var dirPath in directories)
        {
            var dirInfo = new DirectoryInfo(dirPath);
            if (!dirInfo.Exists) continue;

            var files = dirInfo.GetFiles();
            StatusContext.Progress($"Scanning {files.Length} file(s) in {dirInfo.FullName}...");

            foreach (var file in files)
            {
                if (!processed.Add(file.FullName)) continue;

                var related = FindRelatedFiles(file);
                foreach (var f in related) processed.Add(f.FullName);

                var fileGroup = related.Select(f => f.FullName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                if (fileGroup.Count > 0)
                {
                    StatusContext.Progress(
                        $"Found group of {fileGroup.Count} related file(s) starting with {file.Name}...");
                    fileGroups.Add(fileGroup);
                }
            }
        }

        var groupListItems = new ConcurrentBag<PhotoListGroupListItem>();

        Parallel.ForEach(fileGroups, fileGroup =>
        {
            var groupItem = PhotoListGroupListItem
                .CreateInstance(StatusContext, fileGroup.Select(f => new FileInfo(f)).ToList()).Result;
            StatusContext.Progress(
                $"Created group for {fileGroup.Count} file(s) starting with {Path.GetFileName(fileGroup[0])}...");
            groupListItems.Add(groupItem);
        });

        var toAdd = groupListItems.OrderBy(g => g.Items.FirstOrDefault(x => x.IsPrimaryPhoto)?.PhotoFile.Name).ToList();

        StatusContext.Progress($"Adding {toAdd.Count} groups to the list...");

        await ThreadSwitcher.ResumeForegroundAsync();

        foreach (var photoListGroupListItem in toAdd) Items.Add(photoListGroupListItem);
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task RemoveAllGroups()
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        var frozenItems = CurrentFilteredListItems.ToList();

        foreach (var loopSelected in frozenItems) Items.Remove(loopSelected);
    }

    [BlockingCommand]
    [StopAndWarnIfFirstParameterIsNull]
    public async Task RemoveGroup(PhotoListGroupListItem? toRemove)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        Items.Remove(toRemove!);
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task RemoveSelectedGroups()
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        var frozenSelected = SelectedItems.ToList();

        foreach (var loopSelected in frozenSelected) Items.Remove(loopSelected);
    }

    [BlockingCommand]
    public async Task RenameFilesAllItems(CancellationToken cancellationToken)
    {
        var toRename = CurrentFilteredListItems.ToList();

        foreach (var loopSelected in toRename) await loopSelected.RenameFiles();
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task RenameFilesSelectedItems(CancellationToken cancellationToken)
    {
        var frozenSelected = SelectedItems;

        foreach (var loopSelected in frozenSelected) await loopSelected.RenameFiles();
    }

    public PhotoListGroupListItem? SelectedListItem()
    {
        return SelectedItem;
    }

    public List<PhotoListGroupListItem> SelectedListItems()
    {
        return SelectedItems;
    }

    public void SendPreviewRequest()
    {
        var primaryFile = GetCurrentPrimaryFile();
        if (primaryFile == null)
        {
            WeakReferenceMessenger.Default.Send(new PhotoPreviewClearMessage());
            return;
        }

        var title = SelectedItem?.TitleEntryContext.UserValue.TrimNullToEmpty() ?? primaryFile.Name;
        var rating = SelectedItem?.RatingEntry.UserValue ?? 0;

        var upcomingPaths = new List<string>();

        var visible = FilteredItems?.Cast<PhotoListGroupListItem>().ToList();
        var currentIndex = visible != null && SelectedItem != null ? visible.IndexOf(SelectedItem) : -1;

        if (currentIndex >= 0 && visible != null)
        {
            // Collect forward paths for prefetch
            var collected = 0;
            for (var i = 1; i <= visible.Count && collected < 5; i++)
            {
                var candidate = visible[(currentIndex + i) % visible.Count];

                if (_previewFilterUnratedOnly && candidate.RatingEntry.UserValue > 0) continue;

                var candidateFile = GetPrimaryFileForGroup(candidate);
                if (candidateFile != null)
                {
                    upcomingPaths.Add(candidateFile.FullName);
                    collected++;
                }
            }

            // Collect backward paths so back-navigation is also cached
            collected = 0;
            for (var i = 1; i <= visible.Count && collected < 3; i++)
            {
                var candidate = visible[(currentIndex - i + visible.Count) % visible.Count];

                if (_previewFilterUnratedOnly && candidate.RatingEntry.UserValue > 0) continue;

                var candidateFile = GetPrimaryFileForGroup(candidate);
                if (candidateFile != null)
                {
                    upcomingPaths.Add(candidateFile.FullName);
                    collected++;
                }
            }
        }

        WeakReferenceMessenger.Default.Send(
            new PhotoPreviewRequestMessage(new PhotoPreviewRequestData(primaryFile.FullName, title, rating,
                upcomingPaths.Count > 0 ? upcomingPaths : null)));
    }

    [NonBlockingCommand]
    [StopAndWarnIfFirstParameterIsNull]
    public async Task ShowFileInExplorer(PhotoListGroupListItem? item)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var file = item!.SelectedItem?.PhotoFile
                   ?? item.Items.FirstOrDefault(x => x.IsPrimaryPhoto)?.PhotoFile
                   ?? item.Items.FirstOrDefault()?.PhotoFile;

        if (file == null)
        {
            await StatusContext.ToastWarning("No file found to show in Explorer.");
            return;
        }

        await ProcessHelpers.OpenExplorerWindowForFile(file.FullName);
    }

    [BlockingCommand]
    [StopAndWarnIfFirstParameterIsNull]
    public async Task SplitSelectedFiles(PhotoListGroupListItem item)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var selectedFileItems = item.SelectedItems.ToList();

        if (selectedFileItems.Count == 0)
        {
            await StatusContext.ToastWarning("Select at least one file to split into a new group.");
            return;
        }

        var newGroup = await PhotoListGroupListItem.CreateInstance(StatusContext,
            selectedFileItems.Select(x => x.PhotoFile).ToList());

        await ThreadSwitcher.ResumeForegroundAsync();

        Items.Add(newGroup);

        foreach (var x in selectedFileItems) await item.RemoveFile(x);
    }

    [BlockingCommand]
    public async Task WriteMetadataAllItems(CancellationToken cancellationToken)
    {
        var toWrite = CurrentFilteredListItems.ToList();

        foreach (var loopSelected in toWrite) await loopSelected.WriteMetadata();
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task WriteMetadataSelectedItems(CancellationToken cancellationToken)
    {
        var frozenSelected = SelectedItems;

        foreach (var loopSelected in frozenSelected) await loopSelected.WriteMetadata();
    }
}
