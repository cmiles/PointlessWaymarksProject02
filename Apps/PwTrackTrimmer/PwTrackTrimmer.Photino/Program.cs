using OpenSilver.Photino;
using Photino.NET;

namespace PwTrackTrimmer.Photino
{
    //NOTE: To hide the console window, go to the project properties and change the Output Type to Windows Application.
    // Or edit the .csproj file and change the <OutputType> tag from "WinExe" to "Exe".
    internal class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // Window title declared here for visibility
            string windowTitle = "PwTrackTrimmer";

            // Parse CLI arguments for Host Dialog Mode
            string? inputPath = null;
            string? outputPath = null;
            bool isDialog = false;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg.Equals("--dialog", StringComparison.OrdinalIgnoreCase) || arg.Equals("-d", StringComparison.OrdinalIgnoreCase))
                {
                    isDialog = true;
                }
                else if ((arg.Equals("--input", StringComparison.OrdinalIgnoreCase) || arg.Equals("-i", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
                {
                    inputPath = args[++i];
                }
                else if ((arg.Equals("--output", StringComparison.OrdinalIgnoreCase) || arg.Equals("-o", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
                {
                    outputPath = args[++i];
                }
                else if (!arg.StartsWith("-") && string.IsNullOrEmpty(inputPath))
                {
                    inputPath = arg;
                }
            }

            if (!string.IsNullOrEmpty(outputPath))
            {
                isDialog = true;
            }

            if (!string.IsNullOrEmpty(inputPath))
            {
                PwTrackTrimmer.Services.HostDialogService.InputFilePath = inputPath;
            }

            if (isDialog)
            {
                PwTrackTrimmer.Services.HostDialogService.IsDialogMode = true;
                PwTrackTrimmer.Services.HostDialogService.OutputFilePath = outputPath ?? inputPath ?? string.Empty;
                PwTrackTrimmer.Services.HostDialogService.ExitCode = 1; // Default to cancel unless confirmed
                windowTitle = "PwTrackTrimmer · Edit Track (Host Dialog)";
            }

            // Creating a new PhotinoWindow instance with the fluent API
            PhotinoWindow? window = null;
            window = new PhotinoWindow()
                .SetTitle(windowTitle)
                .SetUseOsDefaultSize(true)
                .Center() // Center window in the middle of the screen
                .SetResizable(true) // Allow resizing for optimal editing experience
                .SetLogVerbosity(0);

            PwTrackTrimmer.Services.HostDialogService.OnConfirm = () =>
            {
                PwTrackTrimmer.Services.HostDialogService.ExitCode = 0;
                window?.Close();
            };

            PwTrackTrimmer.Services.HostDialogService.OnCancel = () =>
            {
                PwTrackTrimmer.Services.HostDialogService.ExitCode = 1;
                window?.Close();
            };

            window.ConfigureOpenSilver<App>() // Configure OpenSilver App
                .Load("wwwroot/index.html"); // Can be used with relative path strings or "new URI()" instance to load a website.

            window.WaitForClose(); // Starts the application event loop

            if (PwTrackTrimmer.Services.HostDialogService.IsDialogMode)
            {
                Environment.Exit(PwTrackTrimmer.Services.HostDialogService.ExitCode);
            }
        }
    }
}
