using System.Windows;
using System.IO;
using System.Windows.Threading;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.WpfCommon.Utility;
using Serilog;

namespace PointlessWaymarks.MetadataDisplayGui;

/// <summary>
///     Interaction logic for App.xaml
/// </summary>
public partial class App
{
    public App()
    {
        LogTools.StandardStaticLoggerForDefaultLogDirectory("MetadataDisplayGui");

        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var fileList = new List<string>();
        for (var i = 0; i < e.Args.Length; i++)
        {
            var arg = e.Args[i];
            if (arg.Equals("--file", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("-f", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < e.Args.Length)
                {
                    fileList.Add(e.Args[++i]);
                }
            }
            else if (!arg.StartsWith("-"))
            {
                var candidate = arg.Trim().Trim('"', '\'');
                if (File.Exists(candidate) || !string.IsNullOrWhiteSpace(candidate))
                {
                    fileList.Add(candidate);
                }
            }
        }

        var initialFile = fileList.FirstOrDefault();
        var window = await MetadataDisplayGui.MainWindow.CreateInstance(initialFile);
        MainWindow = window;
        await window.PositionWindowAndShowOnUiThread();

        if (fileList.Count > 1)
        {
            for (var i = 1; i < fileList.Count; i++)
            {
                if (File.Exists(fileList[i]))
                {
                    var extraWindow = await MetadataDisplayGui.MainWindow.CreateInstance(fileList[i]);
                    await extraWindow.PositionWindowAndShowOnUiThread();
                }
            }
        }
    }

    private static bool HandleApplicationException(Exception ex)
    {
        Log.Error(ex, "Application Reached HandleApplicationException thru App_DispatcherUnhandledException");

        var msg = $"Something went wrong...\r\n\r\n{ex.Message}\r\n\r\n" + "The error has been logged...\r\n\r\n" +
                  "Do you want to continue?";

        var res = MessageBox.Show(msg, "Pointless Waymarks Metadata Display App Error", MessageBoxButton.YesNo,
            MessageBoxImage.Error,
            MessageBoxResult.Yes);


        return res != MessageBoxResult.No;
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        if (!HandleApplicationException(e.Exception))
            Environment.Exit(1);

        e.Handled = true;
    }
}