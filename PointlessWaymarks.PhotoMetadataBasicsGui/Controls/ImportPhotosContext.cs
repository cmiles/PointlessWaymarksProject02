using System.IO;
using System.Windows;
using GongSolutions.Wpf.DragDrop;
using Microsoft.VisualBasic.FileIO;
using Metalama.Patterns.Observability;
using MetadataExtractor;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.SpatialTools;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.Status;
using PointlessWaymarks.WpfCommon.StringDataEntry;
using PointlessWaymarks.WpfCommon.Utility;
using Directory = System.IO.Directory;
using SearchOption = System.IO.SearchOption;

namespace PointlessWaymarks.PhotoMetadataBasicsGui.Controls;

[Observable]
[GenerateStatusCommands]
public partial class ImportPhotosContext
{
    public required StatusControlContext StatusContext { get; set; }
    public required StringDataEntryContext DestinationFolderEntry { get; set; }
    public required ImportDropHandler WorkingFilesDropHandler { get; set; }
    public required ImportDropHandler FinishedPhotosDropHandler { get; set; }
    public bool OverwriteExistingFiles { get; set; }
    public string ImportLog { get; set; } = string.Empty;

    public static Task<ImportPhotosContext> CreateInstance(StatusControlContext? statusContext)
    {
        try
        {
            var settings = PhotoMetadataBasicsGuiSettingTools.ReadSettings();

            var destinationFolderEntry = StringDataEntryContext.CreateInstance();
            destinationFolderEntry.Title = "Import Destination Folder";
            destinationFolderEntry.HelpText = "The root folder where imported photos will be organized by date.";
            destinationFolderEntry.ReferenceValue = settings.ImportPhotosDestinationFolder;
            destinationFolderEntry.UserValue = settings.ImportPhotosDestinationFolder;

            var context = new ImportPhotosContext
            {
                StatusContext = statusContext ?? StatusControlContext.CreateInstance().Result,
                DestinationFolderEntry = destinationFolderEntry,
                WorkingFilesDropHandler = null!,
                FinishedPhotosDropHandler = null!
            };

            context.OverwriteExistingFiles = settings.OverwriteOnImport;

            context.WorkingFilesDropHandler = new ImportDropHandler(context, true);
            context.FinishedPhotosDropHandler = new ImportDropHandler(context, false);

            context.BuildCommands();

            context.PropertyChanged += async (_, e) =>
            {
                if (e.PropertyName == nameof(OverwriteExistingFiles))
                {
                    var current = PhotoMetadataBasicsGuiSettingTools.ReadSettings();
                    current.OverwriteOnImport = context.OverwriteExistingFiles;
                    await PhotoMetadataBasicsGuiSettingTools.WriteSettings(current);
                }
            };

            return Task.FromResult(context);
        }
        catch (Exception exception)
        {
            return Task.FromException<ImportPhotosContext>(exception);
        }
    }

    [BlockingCommand]
    public async Task BrowseForDestinationFolder()
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        var folderDialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Import Destination Folder"
        };

        if (!string.IsNullOrWhiteSpace(DestinationFolderEntry.UserValue) &&
            Directory.Exists(DestinationFolderEntry.UserValue))
            folderDialog.InitialDirectory = DestinationFolderEntry.UserValue;

        if (folderDialog.ShowDialog() == true)
            DestinationFolderEntry.UserValue = folderDialog.FolderName;
    }

    [BlockingCommand]
    public async Task SaveDestinationFolder()
    {
        var settings = PhotoMetadataBasicsGuiSettingTools.ReadSettings();
        settings.ImportPhotosDestinationFolder = DestinationFolderEntry.UserValue.TrimNullToEmpty();
        await PhotoMetadataBasicsGuiSettingTools.WriteSettings(settings);

        DestinationFolderEntry.ReferenceValue = settings.ImportPhotosDestinationFolder;

        await StatusContext.ToastSuccess("Destination folder saved.");
    }

    [NonBlockingCommand]
    public async Task ClearLog()
    {
        ImportLog = string.Empty;
        await Task.CompletedTask;
    }

    public async Task ImportFiles(List<string> files, bool isWorkingFiles)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var destinationRoot = DestinationFolderEntry.UserValue.TrimNullToEmpty();

        if (string.IsNullOrWhiteSpace(destinationRoot))
        {
            await StatusContext.ToastError("Please set a destination folder before importing.");
            return;
        }

        if (!Directory.Exists(destinationRoot))
        {
            try
            {
                Directory.CreateDirectory(destinationRoot);
            }
            catch (Exception ex)
            {
                await StatusContext.ToastError($"Could not create destination folder: {ex.Message}");
                return;
            }
        }

        var label = isWorkingFiles ? "Working" : "Finished";
        var overwrite = OverwriteExistingFiles;
        var importedCount = 0;
        var skippedCount = 0;
        var overwrittenCount = 0;
        var errorCount = 0;
        var errors = new List<string>();
        var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        StatusContext.Progress($"Importing {files.Count} file(s) as {label}...");

        foreach (var filePath in files)
        {
            if (!processed.Add(Path.GetFullPath(filePath))) continue;

            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
            {
                AppendLog($"[Skip] File not found: {filePath}");
                skippedCount++;
                continue;
            }

            var companions = FindCompanionFiles(fileInfo);
            foreach (var c in companions) processed.Add(c.FullName);

            var createdDate = await GetPhotoCreatedDate(fileInfo);

            var yearFolder = createdDate.ToString("yyyy");
            var dayFolder = createdDate.ToString("yyyy-MM-dd");

            var targetFolder = isWorkingFiles
                ? Path.Combine(destinationRoot, "Working", yearFolder, dayFolder)
                : Path.Combine(destinationRoot, yearFolder, dayFolder);

            Directory.CreateDirectory(targetFolder);

            foreach (var companion in companions)
            {
                try
                {
                    var targetPath = Path.Combine(targetFolder, companion.Name);

                    if (File.Exists(targetPath))
                    {
                        if (!overwrite)
                        {
                            AppendLog($"[Skip] Already exists: {targetPath}");
                            skippedCount++;
                            continue;
                        }

                        try
                        {
                            FileSystem.DeleteFile(targetPath, UIOption.OnlyErrorDialogs,
                                RecycleOption.SendToRecycleBin);
                        }
                        catch (Exception recycleEx)
                        {
                            var msg =
                                $"{companion.Name}: Could not recycle existing file - {recycleEx.Message}";
                            AppendLog($"[Error] {msg}");
                            errors.Add(msg);
                            errorCount++;
                            continue;
                        }

                        File.Copy(companion.FullName, targetPath);
                        AppendLog($"[Overwrite] {companion.Name} -> {targetPath}");
                        overwrittenCount++;
                    }
                    else
                    {
                        File.Copy(companion.FullName, targetPath);
                        AppendLog($"[{label}] {companion.Name} -> {targetPath}");
                        importedCount++;
                    }
                }
                catch (Exception ex)
                {
                    var msg = $"{companion.Name}: {ex.Message}";
                    AppendLog($"[Error] {msg}");
                    errors.Add(msg);
                    errorCount++;
                }
            }
        }

        var summary =
            $"Import complete: {importedCount} imported, {overwrittenCount} overwritten, {skippedCount} skipped, {errorCount} error(s).";
        AppendLog(summary);
        StatusContext.Progress(summary);

        if (errors.Count > 0)
            await StatusContext.ShowMessageWithOkButton("Import Errors",
                string.Join(Environment.NewLine, errors));
    }

    /// <summary>
    ///     Returns the anchor file plus any companion files in the same directory
    ///     that share the same base name (e.g. DSC113.arw, DSC113.jpg, DSC113.xmp).
    /// </summary>
    private static List<FileInfo> FindCompanionFiles(FileInfo anchor)
    {
        var dir = anchor.Directory;
        if (dir is not { Exists: true }) return [anchor];

        var baseName = Path.GetFileNameWithoutExtension(anchor.Name);
        var result = new List<FileInfo> { anchor };
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { anchor.FullName };

        foreach (var candidate in dir.GetFiles($"{baseName}.*", SearchOption.TopDirectoryOnly))
        {
            if (!seen.Add(candidate.FullName)) continue;

            if (Path.GetFileNameWithoutExtension(candidate.Name)
                .Equals(baseName, StringComparison.OrdinalIgnoreCase))
                result.Add(candidate);
        }

        return result;
    }

    private async Task<DateTime> GetPhotoCreatedDate(FileInfo file)
    {
        try
        {
            var directories = ImageMetadataReader.ReadMetadata(file.FullName);
            var createdOn = await FileMetadataEmbeddedTools.CreatedOnLocalAndUtc(directories);

            if (createdOn.createdOnLocal != null)
                return createdOn.createdOnLocal.Value;
        }
        catch
        {
            // Fall through to file date fallback
        }

        return file.CreationTime < file.LastWriteTime ? file.CreationTime : file.LastWriteTime;
    }

    private void AppendLog(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        ImportLog += string.IsNullOrEmpty(ImportLog)
            ? $"[{timestamp}] {message}"
            : $"{Environment.NewLine}[{timestamp}] {message}";
    }

    public class ImportDropHandler(ImportPhotosContext context, bool isWorkingFiles) : IDropTarget
    {
        public void DragOver(IDropInfo dropInfo)
        {
            var files = DragAndDropFilesHelper.DroppedFileNames(dropInfo, true);
            dropInfo.Effects = files.Count > 0 ? DragDropEffects.Copy : DragDropEffects.None;
        }

        public void Drop(IDropInfo dropInfo)
        {
            var files = DragAndDropFilesHelper.DroppedFiles(dropInfo,
                FileLocationTools.TempStorageDirectory(), true);

            if (files.Count == 0) return;

            context.StatusContext.RunBlockingTask(() => context.ImportFiles(files, isWorkingFiles));
        }
    }
}
