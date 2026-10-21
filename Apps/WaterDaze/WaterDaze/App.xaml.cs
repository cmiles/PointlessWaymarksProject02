using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace WaterDaze
{
    public sealed partial class App : Application
    {
        public App()
        {
            this.InitializeComponent();

            var mainGrid = new Grid();
            this.RootVisual = mainGrid;

            WaterDaze.Services.ThemeManager.Initialize();

            if (mainGrid.Children.Count == 0)
            {
                mainGrid.Children.Add(new MainPage());
            }
        }
    }
}
