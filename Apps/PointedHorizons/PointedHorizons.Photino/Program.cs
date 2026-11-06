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

            window.WaitForClose();
        }
    }
}
