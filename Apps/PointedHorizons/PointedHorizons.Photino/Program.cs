using OpenSilver.Photino;
using Photino.NET;
using PointedHorizons.Services;

namespace PointedHorizons.Photino
{
    internal class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            OfflineDataService.Initialize();
            LocalTileServer.StartServer();

            string windowTitle = "Pointed Horizons";
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "favicon.ico");
            if (!File.Exists(iconPath)) iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");

            var window = new PhotinoWindow()
                .SetTitle(windowTitle)
                .SetIconFile(File.Exists(iconPath) ? iconPath : "app.ico")
                .SetUseOsDefaultSize(true)
                .Center()
                .SetResizable(true)
                .SetLogVerbosity(0)
                .ConfigureOpenSilver<App>()
                .Load("wwwroot/index.html");

            FolderPickerService.CustomFolderPickerHandler = async (initialPath) =>
            {
                try
                {
                    string defaultDir = string.IsNullOrWhiteSpace(initialPath) || !Directory.Exists(initialPath)
                        ? OfflineDataService.DefaultPhotinoGpxDir
                        : initialPath;

                    string[] folders = await window.ShowOpenFolderAsync("Select GPX / Tracks Folder", defaultDir, false);
                    if (folders != null && folders.Length > 0 && !string.IsNullOrWhiteSpace(folders[0]))
                    {
                        return folders[0];
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Photino] ShowOpenFolder error: {ex.Message}");
                }
                return null;
            };

            window.WaitForClose();
        }
    }
}
