using GongSolutions.Wpf.DragDrop;
using Metalama.Patterns.Observability;
using NetTopologySuite.Features;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.FeatureIntersectionTags.Models;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.Status;
using PointlessWaymarks.WpfCommon.Utility;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using PointlessWaymarks.FeatureIntersectionTags;
using Point = NetTopologySuite.Geometries.Point;

namespace PointlessWaymarks.PhotoMetadataBasicsGui.Controls;

[Observable]
[GenerateStatusCommands]
public partial class PhotoListContext : IDropTarget
{
    public required ObservableCollection<PhotoListGroupListItem> Items { get; set; }
    public PhotoListGroupListItem? SelectedItem { get; set; }
    public List<PhotoListGroupListItem> SelectedItems { get; set; } = [];
    public required StatusControlContext StatusContext { get; set; }

    public void DragOver(IDropInfo dropInfo)
    {
        var files = DragAndDropFilesHelper.DroppedFileNames(dropInfo, true);
        dropInfo.Effects = files.Count > 0 ? DragDropEffects.Copy : DragDropEffects.None;
    }

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
    public async Task AddFeatureIntersectTags(PhotoListGroupListItem toProcess, CancellationToken cancellationToken)
    {
        await AddFeatureIntersectTags([toProcess], cancellationToken);
    }

    [BlockingCommand]
    [StopAndWarnIfNoSelectedListItems]
    public async Task AddFeatureIntersectTagsForSelected(CancellationToken cancellationToken)
    {
        await AddFeatureIntersectTags(SelectedItems, cancellationToken);
    }

    public async Task AddFeatureIntersectTags(List<PhotoListGroupListItem> toProcess,
        CancellationToken cancellationToken)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        foreach (var loopGroup in toProcess)
        {
            var validLatLong = await loopGroup.HasValidLatLong();
            if (!validLatLong)
            {
                await StatusContext.ToastError("No valid Lat/Long to check?");
                return;
            }

            var featureToCheck = new Feature(
                new Point(loopGroup.LongitudeEntry.UserValue!.Value,
                    loopGroup.LatitudeEntry.UserValue!.Value),
                new AttributesTable());
            var intersectionResult = new IntersectResult(featureToCheck)
                { Description = "Photo Basic Metadata Tagging" };
            var settings = await PhotoMetadataBasicsGuiSettingTools.FeatureIntersectSettings(StatusContext);

            settings.UseOsmOverpass = true;
            settings.OsmInTagging = true;

            var possibleTags = await intersectionResult.AsList().IntersectionTags(settings,
                cancellationToken, StatusContext.ProgressTracker());

            if (!possibleTags.Any())
            {
                await StatusContext.ToastWarning("No tags found...");
                return;
            }

            var taggerTags = possibleTags.SelectMany(t => t.Tags).Distinct().ToList();
            var cleanedTaggerTags = SlugTagTools.TagListCleanupToSpacedString(taggerTags);
            var combinedTags =
                SlugTagTools.TagListCleanupToSpacedString(loopGroup.TagEntryContext.TagsList().Union(cleanedTaggerTags)
                    .ToList());
            loopGroup.TagEntryContext.Tags = SlugTagTools.TagListJoinToSpacedString(combinedTags);
        }
    }

    public static async Task<PhotoListContext> CreateInstance(StatusControlContext? statusContext)
    {
        var factoryReturn = new PhotoListContext
        {
            Items = new ObservableCollection<PhotoListGroupListItem>(),
            StatusContext = statusContext ?? await StatusControlContext.CreateInstance()
        };

        factoryReturn.BuildCommands();

        return factoryReturn;
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
        if (confirmedFiles.Count == 1)
        {
            var firstFile = confirmedFiles[0];
            var dir = firstFile.Directory;
            if (dir is { Exists: true })
            {
                var baseName = Path.GetFileNameWithoutExtension(firstFile.Name);

                // 1) Same base name, different extension.
                foreach (var f in dir.GetFiles(baseName + ".*"))
                {
                    if (confirmedFiles.Any(x => x.FullName.Equals(f.FullName, StringComparison.OrdinalIgnoreCase)))
                        continue;
                    confirmedFiles.Add(f);
                }

                // 2) Files containing _DSC[digits] token from the dropped file, if present.
                var match = Regex.Match(firstFile.Name, "_DSC\\d+", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var token = match.Value;
                    foreach (var f in dir.GetFiles("*", SearchOption.TopDirectoryOnly)
                                 .Where(x => x.Name.Contains(token, StringComparison.OrdinalIgnoreCase)))
                    {
                        if (confirmedFiles.Any(x => x.FullName.Equals(f.FullName, StringComparison.OrdinalIgnoreCase)))
                            continue;
                        confirmedFiles.Add(f);
                    }
                }

                // 3) Files whose base name starts with the dropped file's base name.
                foreach (var f in dir.GetFiles("*", SearchOption.TopDirectoryOnly)
                             .Where(x => Path.GetFileNameWithoutExtension(x.Name)
                                 .StartsWith(baseName, StringComparison.OrdinalIgnoreCase)))
                {
                    if (confirmedFiles.Any(x => x.FullName.Equals(f.FullName, StringComparison.OrdinalIgnoreCase)))
                        continue;
                    confirmedFiles.Add(f);
                }

                // 4) Numbered-suffix grouping: if the base name ends with -NN,
                //    find the root name and all its numbered variants (and the root itself).
                var suffixMatch = Regex.Match(baseName, @"^(.+)-\d+$");
                if (suffixMatch.Success)
                {
                    var rootName = suffixMatch.Groups[1].Value;
                    foreach (var f in dir.GetFiles("*", SearchOption.TopDirectoryOnly)
                                 .Where(x =>
                                 {
                                     var cb = Path.GetFileNameWithoutExtension(x.Name);
                                     return cb.Equals(rootName, StringComparison.OrdinalIgnoreCase)
                                            || Regex.IsMatch(cb, $"^{Regex.Escape(rootName)}-\\d+$",
                                                RegexOptions.IgnoreCase);
                                 }))
                    {
                        if (confirmedFiles.Any(x =>
                                x.FullName.Equals(f.FullName, StringComparison.OrdinalIgnoreCase)))
                            continue;
                        confirmedFiles.Add(f);
                    }
                }
            }
        }

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

    private async Task ProcessDroppedDirectoriesToFileGroups(List<string> directories)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        StatusContext.Progress($"Processing {directories.Count} dropped directories...");

        foreach (var dirPath in directories)
        {
            var dirInfo = new DirectoryInfo(dirPath);
            if (!dirInfo.Exists) continue;

            var files = dirInfo.GetFiles();
            StatusContext.Progress($"Scanning {files.Length} file(s) in {dirInfo.FullName}...");
            foreach (var file in files)
            {
                if (!processed.Add(file.FullName)) continue;

                var baseName = Path.GetFileNameWithoutExtension(file.Name);
                var groupSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { file.FullName };
                var group = new List<string> { file.FullName };

                // Same base name, different extension
                foreach (var f in dirInfo.GetFiles(baseName + ".*"))
                    if (processed.Add(f.FullName) && groupSet.Add(f.FullName))
                        group.Add(f.FullName);

                // _DSC[digits] token match
                var match = Regex.Match(file.Name, "_DSC\\d+", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var token = match.Value;
                    foreach (var f in dirInfo.GetFiles("*", SearchOption.TopDirectoryOnly)
                                 .Where(x => x.Name.Contains(token, StringComparison.OrdinalIgnoreCase)))
                        if (processed.Add(f.FullName) && groupSet.Add(f.FullName))
                            group.Add(f.FullName);
                }

                // Starts with base name
                foreach (var f in dirInfo.GetFiles("*", SearchOption.TopDirectoryOnly)
                             .Where(x => Path.GetFileNameWithoutExtension(x.Name)
                                 .StartsWith(baseName, StringComparison.OrdinalIgnoreCase)))
                    if (processed.Add(f.FullName) && groupSet.Add(f.FullName))
                        group.Add(f.FullName);

                // Numbered-suffix grouping: if the base name ends with -NN,
                // find the root name and all its numbered variants (and the root itself).
                var suffixMatch = Regex.Match(baseName, @"^(.+)-\d+$");
                if (suffixMatch.Success)
                {
                    var rootName = suffixMatch.Groups[1].Value;
                    foreach (var f in dirInfo.GetFiles("*", SearchOption.TopDirectoryOnly)
                                 .Where(x =>
                                 {
                                     var cb = Path.GetFileNameWithoutExtension(x.Name);
                                     return cb.Equals(rootName, StringComparison.OrdinalIgnoreCase)
                                            || Regex.IsMatch(cb, $"^{Regex.Escape(rootName)}-\\d+$",
                                                RegexOptions.IgnoreCase);
                                 }))
                        if (processed.Add(f.FullName) && groupSet.Add(f.FullName))
                            group.Add(f.FullName);
                }

                // Ensure unique list and load
                var distinctGroup = group.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (distinctGroup.Count > 0)
                {
                    StatusContext.Progress(
                        $"Found group of {distinctGroup.Count} related file(s) starting with {file.Name}...");
                    await LoadItems(distinctGroup);
                }
            }
        }
    }

    [BlockingCommand]
    [StopAndWarnIfFirstParameterIsNull]
    public async Task RemoveGroup(PhotoListGroupListItem? toRemove)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        Items.Remove(toRemove!);
    }

    public PhotoListGroupListItem? SelectedListItem()
    {
        return SelectedItem;
    }

    public List<PhotoListGroupListItem> SelectedListItems()
    {
        return SelectedItems;
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
}