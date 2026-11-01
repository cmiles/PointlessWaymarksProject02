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

            string windowTitle = "PointedHorizons";

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
