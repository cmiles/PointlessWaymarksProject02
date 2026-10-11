#nullable enable
using System;
using System.IO;
using System.Windows;

namespace PwImageMetadataViewer
{
    public sealed partial class App : Application
    {
        public static string? StartupFilePath { get; set; }
        public static string? StartupFileName { get; set; }
        public static byte[]? StartupFileBytes { get; set; }

        public App()
        {
            this.InitializeComponent();

            var mainPage = new MainPage();
            this.RootVisual = mainPage;
        }
    }
}
