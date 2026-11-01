using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using PointedHorizons.Services;

namespace PointedHorizons
{
    public sealed partial class App : Application
    {
        public App()
        {
            this.InitializeComponent();

            ThemeManager.Current.Initialize();
            OfflineDataService.Initialize();
            LocalTileServer.StartServer();

            var mainPage = new MainPage();
            this.RootVisual = mainPage;
        }
    }
}
