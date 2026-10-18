using System;
using System.IO;
using OpenSilver.Photino;
using Photino.NET;

namespace PwImageMetadataViewer.Photino
{
    internal class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            string? fileToLoad = null;

            if (args != null && args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
            {
                string candidate = args[0].Trim('"', '\'');
                if (File.Exists(candidate))
                {
                    fileToLoad = Path.GetFullPath(candidate);
                }
            }

            // Fallback to sample folder image if no command line arg was specified
            if (string.IsNullOrEmpty(fileToLoad))
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string sampleInBase = Path.Combine(baseDir, "Samples", "1510-Rama-Shrine-from-the-Grandview-Trail.jpg");
                if (File.Exists(sampleInBase))
                {
                    fileToLoad = sampleInBase;
                }
                else
                {
                    string currentDir = Directory.GetCurrentDirectory();
                    string sampleInCurrent = Path.Combine(currentDir, "Samples", "1510-Rama-Shrine-from-the-Grandview-Trail.jpg");
                    if (File.Exists(sampleInCurrent))
                    {
                        fileToLoad = sampleInCurrent;
                    }
                    else
                    {
                        string sampleInParent = Path.Combine(currentDir, "PwImageMetadataViewer", "Samples", "1510-Rama-Shrine-from-the-Grandview-Trail.jpg");
                        if (File.Exists(sampleInParent))
                        {
                            fileToLoad = sampleInParent;
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(fileToLoad) && File.Exists(fileToLoad))
            {
                App.StartupFilePath = fileToLoad;
                App.StartupFileName = Path.GetFileName(fileToLoad);
                try
                {
                    App.StartupFileBytes = File.ReadAllBytes(fileToLoad);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Photino] Error reading startup file: {ex.Message}");
                }
            }

            string windowTitle = "Pointless Waymarks Image Metadata Viewer";

            var window = new PhotinoWindow()
                .SetTitle(windowTitle)
                .SetUseOsDefaultSize(true)
                .Center()
                .SetResizable(true)
                .SetLogVerbosity(0)
                .ConfigureOpenSilver<App>()
                .Load("wwwroot/index.html");

            window.WaitForClose();
        }
    }
}
