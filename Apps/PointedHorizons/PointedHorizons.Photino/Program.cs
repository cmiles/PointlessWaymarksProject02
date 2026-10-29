using OpenSilver.Photino;
using Photino.NET;

namespace PointedHorizons.Photino
{
    internal class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
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
