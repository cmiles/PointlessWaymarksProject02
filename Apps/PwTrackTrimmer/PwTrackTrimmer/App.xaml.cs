using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace PwTrackTrimmer
{
    public sealed partial class App : Application
    {
        public App()
        {
            this.InitializeComponent();

            // Enter construction logic here...

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
