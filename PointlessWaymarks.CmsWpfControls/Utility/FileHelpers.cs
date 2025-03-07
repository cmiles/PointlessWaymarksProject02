using System.IO;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.Status;
using Serilog;

namespace PointlessWaymarks.CmsWpfControls.Utility;

public static class FileHelpers
{
    public static async Task RenameSelectedFile(FileInfo? selectedFile, StatusControlContext statusContext,
        Action<FileInfo> setSelectedFile)
    {
        if (selectedFile is not { Exists: true })
        {
            await statusContext.ToastWarning("No file to rename?");
            return;
        }

        var newName = await statusContext.ShowStringEntry("Rename File",
            $"Rename {Path.GetFileNameWithoutExtension(selectedFile.Name)} - " +
            "File Names must be limited to A-Z a-z 0-9 - . _  :",
            Path.GetFileNameWithoutExtension(selectedFile.Name.Replace(" ", "-")));

        if (!newName.Item1) return;

        var cleanedName = newName.Item2.TrimNullToEmpty();

        if (string.IsNullOrWhiteSpace(cleanedName))
        {
            await statusContext.ToastError("Can't rename the file to an empty string...");
            return;
        }

        var noExtensionCleaned = Path.GetFileNameWithoutExtension(cleanedName);

        if (string.IsNullOrWhiteSpace(noExtensionCleaned))
        {
            await statusContext.ToastError("Not a valid filename...");
            return;
        }

        if (!FileAndFolderTools.IsNoUrlEncodingNeeded(noExtensionCleaned))
        {
            await statusContext.ToastError("File Names must be limited to A - Z a - z 0 - 9 - . _");
            return;
        }

        var moveToName = Path.Combine(selectedFile.Directory?.FullName ?? string.Empty,
            $"{noExtensionCleaned}{Path.GetExtension(selectedFile.Name)}");

        try
        {
            File.Copy(selectedFile.FullName, moveToName);
        }
        catch (Exception e)
        {
            Log.Error(e, "Exception while trying to rename file. Status Control Context Id {0}",
                statusContext.StatusControlContextId);
            await statusContext.ToastError($"Error Copying File: {e.Message}");
            return;
        }

        var finalFile = new FileInfo(moveToName);

        if (!finalFile.Exists)
        {
            await statusContext.ToastError("Unknown error renaming file - original file still selected.");
            return;
        }

        try
        {
            setSelectedFile(finalFile);
        }
        catch (Exception e)
        {
            await statusContext.ToastError($"Error setting selected file - {e.Message}");
            return;
        }

        await statusContext.ToastSuccess($"Selected file now {selectedFile.FullName}");
    }

    /// <summary>
    ///     Attempts to rename a file to a name that conforms to the constrained filename rules of this program.
    /// </summary>
    /// <param name="selectedFile"></param>
    /// <param name="statusContext"></param>
    /// <param name="setSelectedFile"></param>
    /// <returns></returns>
    public static async Task TryAutoCleanRenameSelectedFile(FileInfo? selectedFile,
        StatusControlContext statusContext,
        Action<FileInfo> setSelectedFile)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (selectedFile is not { Exists: true })
        {
            await statusContext.ToastWarning("No file to rename?");
            return;
        }

        var cleanedFileNamePath = Path.GetFileNameWithoutExtension(selectedFile.Name).TrimNullToEmpty();

        await TryAutoRenameSelectedFile(selectedFile, cleanedFileNamePath, statusContext, setSelectedFile);
    }

    /// <summary>
    ///     Tries to rename a file to an automatically cleaned version of the input suggested name.
    /// </summary>
    /// <param name="selectedFile"></param>
    /// <param name="suggestedName"></param>
    /// <param name="statusContext"></param>
    /// <param name="setSelectedFile"></param>
    /// <returns></returns>
    public static async Task TryAutoRenameSelectedFile(FileInfo? selectedFile, string suggestedName,
        StatusControlContext statusContext,
        Action<FileInfo> setSelectedFile)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (selectedFile is not { Exists: true })
        {
            await statusContext.ToastWarning("No file to rename?");
            return;
        }

        var cleanedName = SlugTools.CreateSlug(false, suggestedName.TrimNullToEmpty());

        if (string.IsNullOrWhiteSpace(cleanedName))
        {
            await statusContext.ToastError("Can't rename the file to an empty string...");
            return;
        }

        if (!FileAndFolderTools.IsNoUrlEncodingNeeded(cleanedName))
        {
            await statusContext.ToastError("File Names must be limited to A - Z a - z 0 - 9 - . _");
            return;
        }

        var moveToName = Path.Combine(selectedFile.Directory?.FullName ?? string.Empty,
            $"{cleanedName}{Path.GetExtension(selectedFile.Name)}");

        try
        {
            File.Copy(selectedFile.FullName, moveToName);
        }
        catch (Exception e)
        {
            Log.Error(e, "Exception while trying to rename file. Status Control Context Id {0}",
                statusContext.StatusControlContextId);
            await statusContext.ToastError($"Error Copying File: {e.Message}");
            return;
        }

        var finalFile = new FileInfo(moveToName);

        if (!finalFile.Exists)
        {
            await statusContext.ToastError("Unknown error renaming file - original file still selected.");
            return;
        }

        try
        {
            setSelectedFile(finalFile);
        }
        catch (Exception e)
        {
            await statusContext.ToastError($"Error setting selected file - {e.Message}");
            return;
        }

        await statusContext.ToastSuccess($"Selected file now {selectedFile.FullName}");
    }
}