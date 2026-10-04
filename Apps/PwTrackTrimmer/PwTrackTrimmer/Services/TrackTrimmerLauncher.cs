using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace PwTrackTrimmer.Services
{
    /// <summary>
    /// Drop-in client helper for external .NET C# desktop applications to launch PwTrackTrimmer.Photino
    /// in modal dialog mode using the Process + File contract.
    /// </summary>
    public static class TrackTrimmerLauncher
    {
        public class DialogResult
        {
            public bool Confirmed { get; set; }
            public int ExitCode { get; set; }
            public string OutputFilePath { get; set; }
        }

        /// <summary>
        /// Launches PwTrackTrimmer.Photino with the specified input track, waits for the user to confirm or cancel,
        /// and returns the result with the path to the edited output track file.
        /// </summary>
        /// <param name="executablePath">Absolute path to PwTrackTrimmer.Photino.exe</param>
        /// <param name="inputFilePath">Path to the track file to edit (.fit, .tcx, or .gpx)</param>
        /// <param name="outputFilePath">Optional destination path. If null, a temp file with the same extension will be created.</param>
        public static async Task<DialogResult> EditTrackModalAsync(
            string executablePath,
            string inputFilePath,
            string outputFilePath = null)
        {
            if (!File.Exists(executablePath))
            {
                throw new FileNotFoundException($"Photino executable not found at: {executablePath}");
            }
            if (!File.Exists(inputFilePath))
            {
                throw new FileNotFoundException($"Input track file not found at: {inputFilePath}");
            }

            if (string.IsNullOrEmpty(outputFilePath))
            {
                string ext = Path.GetExtension(inputFilePath);
                if (string.IsNullOrEmpty(ext)) ext = ".gpx";
                outputFilePath = Path.Combine(Path.GetTempPath(), $"edited_track_{Guid.NewGuid():N}{ext}");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = $"--input \"{inputFilePath}\" --output \"{outputFilePath}\" --dialog",
                UseShellExecute = false,
                CreateNoWindow = false
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return new DialogResult { Confirmed = false, ExitCode = -1, OutputFilePath = null };
            }

            await process.WaitForExitAsync();

            bool confirmed = process.ExitCode == 0 && File.Exists(outputFilePath);
            return new DialogResult
            {
                Confirmed = confirmed,
                ExitCode = process.ExitCode,
                OutputFilePath = confirmed ? outputFilePath : null
            };
        }
    }
}
