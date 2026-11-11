using System;
using System.IO;
using System.Threading.Tasks;
using OpenSilver;

namespace PointedHorizons.Services;

/// <summary>
/// Provides cross-platform folder picker dialog support across Photino (desktop), WebAssembly (browser), and MauiHybrid.
/// </summary>
public static class FolderPickerService
{
    /// <summary>
    /// Custom platform handler for folder picking (e.g. PhotinoWindow.ShowOpenFolderAsync or MAUI FolderPicker).
    /// </summary>
    public static Func<string, Task<string?>>? CustomFolderPickerHandler { get; set; }

    /// <summary>
    /// Prompts the user to choose a folder. Returns the chosen directory path, or null if canceled.
    /// </summary>
    public static async Task<string?> PickFolderAsync(string initialPath = "")
    {
        if (CustomFolderPickerHandler != null)
        {
            try
            {
                var customResult = await CustomFolderPickerHandler(initialPath);
                if (!string.IsNullOrWhiteSpace(customResult))
                {
                    return customResult;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FolderPickerService] Custom handler error: {ex.Message}");
            }
        }

        // Web / Browser fallback prompt
        try
        {
            object? jsPromptResult = Interop.ExecuteJavaScript(@"
                prompt('Enter local or server GPX folder path:', $0) || ''
            ", initialPath ?? string.Empty);

            string? str = jsPromptResult?.ToString();
            if (!string.IsNullOrWhiteSpace(str))
            {
                return str.Trim();
            }
        }
        catch { }

        return null;
    }
}
