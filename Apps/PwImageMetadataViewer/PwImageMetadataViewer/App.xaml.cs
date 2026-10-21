#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

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

            var mainGrid = new Grid();
            this.RootVisual = mainGrid;

            // Initialize theme before instantiating MainPage so that initial static resources and palettes match system/saved preference
            Services.ThemeManager.Current.Initialize();

            if (mainGrid.Children.Count == 0)
            {
                mainGrid.Children.Add(new MainPage());
            }
        }
    }
}

