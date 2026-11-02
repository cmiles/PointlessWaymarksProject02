using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PointedHorizons.Services;

namespace PointedHorizons.Controls;

public partial class PmTilesConfigControl : UserControl
{
    public event EventHandler? CloseRequested;
    public event EventHandler? ApplyAndUseRequested;

    private List<PmTileLayerConfig> _workingLayers = new();

    public PmTilesConfigControl()
    {
        InitializeComponent();
        Loaded += PmTilesConfigControl_Loaded;

        ThemeManager.Current.ThemeChanged += (s, e) =>
        {
            Dispatcher.BeginInvoke(() => UpdateTheme(ThemeManager.Current.IsDark));
        };
    }

    private void PmTilesConfigControl_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateTheme(ThemeManager.Current.IsDark);
        RefreshView();
    }

    public void RefreshView()
    {
        TxtPmTilesDir.Text = OfflineDataService.PmTilesDirectory;
        TxtDemDir.Text = OfflineDataService.DemDirectory;

        bool canConfig = OfflineDataService.CanConfigurePaths;
        TxtPmTilesDir.IsReadOnly = !canConfig;
        TxtDemDir.IsReadOnly = !canConfig;
        BtnResetPmTiles.Visibility = canConfig ? Visibility.Visible : Visibility.Collapsed;
        BtnResetDem.Visibility = canConfig ? Visibility.Visible : Visibility.Collapsed;

        if (OfflineDataService.IsAndroid)
        {
            TxtStorageHint.Text = "📱 Android: Folders are located in your device's Documents directory. Connect via USB or use a file manager to add files.";
        }
        else
        {
            TxtStorageHint.Text = "Downloaded DEMs are automatically cached here. You can also add offline .hgt or .zip files directly.";
        }

        int demCount = OfflineDataService.GetLocalDemFileCount();
        TxtDemStatus.Text = demCount > 0
            ? $"✓ {demCount} local DEM tile{(demCount == 1 ? "" : "s")} found in folder"
            : "No local DEM files currently in folder (network DEMs will be cached when loaded)";

        _workingLayers = OfflineDataService.GetConfiguredLayers().Select(l => new PmTileLayerConfig
        {
            FileName = l.FileName,
            IsEnabled = l.IsEnabled,
            Opacity = l.Opacity,
            Order = l.Order
        }).OrderBy(l => l.Order).ToList();

        // Populate Style Files
        var availableStyles = OfflineDataService.GetAvailableStyleFiles();
        if (CmbStyleFile != null)
        {
            CmbStyleFile.Items.Clear();
            CmbStyleFile.Items.Add("(None / Default Styling)");

            int selectedStyleIndex = 0;
            for (int i = 0; i < availableStyles.Count; i++)
            {
                string styleName = availableStyles[i];
                CmbStyleFile.Items.Add(styleName);
                if (!string.IsNullOrEmpty(OfflineDataService.SelectedStyleFile) &&
                    string.Equals(styleName, OfflineDataService.SelectedStyleFile, StringComparison.OrdinalIgnoreCase))
                {
                    selectedStyleIndex = i + 1;
                }
            }
            CmbStyleFile.SelectedIndex = selectedStyleIndex;
        }

        if (TxtStyleFoundCount != null)
        {
            TxtStyleFoundCount.Text = availableStyles.Count > 0
                ? $"({availableStyles.Count} style file{(availableStyles.Count == 1 ? "" : "s")} found)"
                : "(No .json files found in folder)";
        }

        RebuildLayersList();
        UpdateTheme(ThemeManager.Current.IsDark);
    }

    private void RebuildLayersList()
    {
        if (LayersListPanel == null) return;
        LayersListPanel.Children.Clear();

        var availableFiles = OfflineDataService.GetAvailablePmTileFiles();

        if (availableFiles.Count == 0)
        {
            if (NoFilesBorder != null) NoFilesBorder.Visibility = Visibility.Visible;
            return;
        }

        if (NoFilesBorder != null) NoFilesBorder.Visibility = Visibility.Collapsed;

        var tm = ThemeManager.Current;

        for (int i = 0; i < _workingLayers.Count; i++)
        {
            int index = i;
            var layer = _workingLayers[i];
            var fileInfo = availableFiles.FirstOrDefault(f => string.Equals(f.FileName, layer.FileName, StringComparison.OrdinalIgnoreCase));

            var rowBorder = new Border
            {
                BorderThickness = new Thickness(1),
                BorderBrush = tm.MapOverlayBorder,
                Background = tm.MapOverlayBackground,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 2, 0, 4)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // 1. Enable Checkbox
            var chk = new CheckBox
            {
                IsChecked = layer.IsEnabled,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            chk.Checked += (s, e) => layer.IsEnabled = true;
            chk.Unchecked += (s, e) => layer.IsEnabled = false;
            Grid.SetColumn(chk, 0);
            grid.Children.Add(chk);

            // 2. File Name & Size
            var namePanel = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center };
            var txtName = new TextBlock
            {
                Text = layer.FileName,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = tm.TextPrimary,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap
            };
            var txtSize = new TextBlock
            {
                Text = fileInfo != null
                    ? (fileInfo.IsValidArchive ? $"Size: {fileInfo.FormattedSize}" : $"⚠️ {fileInfo.ArchiveFormatError} ({fileInfo.FormattedSize})")
                    : "Local PMTiles file",
                FontSize = 10,
                Foreground = (fileInfo != null && !fileInfo.IsValidArchive)
                    ? new SolidColorBrush(Color.FromArgb(255, 239, 68, 68))
                    : tm.TextSecondary,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap
            };
            namePanel.Children.Add(txtName);
            namePanel.Children.Add(txtSize);
            Grid.SetColumn(namePanel, 1);
            grid.Children.Add(namePanel);

            // 3. Opacity Selector Button (cycles 100% -> 75% -> 50% -> 25%)
            var btnOpacity = new Button
            {
                Content = $"{(int)(layer.Opacity * 100)}%",
                Style = Application.Current.Resources["PillButtonStyle"] as Style,
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(4, 0, 6, 0),
                FontSize = 10,
                FontWeight = FontWeights.SemiBold
            };
            ToolTipService.SetToolTip(btnOpacity, "Click to cycle layer opacity (100% / 75% / 50% / 25%)");
            btnOpacity.Click += (s, e) =>
            {
                if (layer.Opacity >= 0.95) layer.Opacity = 0.75;
                else if (layer.Opacity >= 0.7) layer.Opacity = 0.50;
                else if (layer.Opacity >= 0.45) layer.Opacity = 0.25;
                else layer.Opacity = 1.0;

                btnOpacity.Content = $"{(int)(layer.Opacity * 100)}%";
            };
            Grid.SetColumn(btnOpacity, 2);
            grid.Children.Add(btnOpacity);

            // 4. Up / Down Reorder Buttons
            var orderPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var btnUp = new Button
            {
                Content = "▲",
                Style = Application.Current.Resources["PillButtonStyle"] as Style,
                Padding = new Thickness(5, 2, 5, 2),
                Margin = new Thickness(1, 0, 2, 0),
                FontSize = 9,
                IsEnabled = index > 0
            };
            btnUp.Click += (s, e) => MoveLayer(index, -1);

            var btnDown = new Button
            {
                Content = "▼",
                Style = Application.Current.Resources["PillButtonStyle"] as Style,
                Padding = new Thickness(5, 2, 5, 2),
                Margin = new Thickness(1, 0, 0, 0),
                FontSize = 9,
                IsEnabled = index < _workingLayers.Count - 1
            };
            btnDown.Click += (s, e) => MoveLayer(index, 1);

            orderPanel.Children.Add(btnUp);
            orderPanel.Children.Add(btnDown);
            Grid.SetColumn(orderPanel, 3);
            grid.Children.Add(orderPanel);

            rowBorder.Child = grid;
            LayersListPanel.Children.Add(rowBorder);
        }
    }

    private void MoveLayer(int index, int direction)
    {
        int target = index + direction;
        if (target < 0 || target >= _workingLayers.Count) return;

        var temp = _workingLayers[index];
        _workingLayers[index] = _workingLayers[target];
        _workingLayers[target] = temp;

        for (int i = 0; i < _workingLayers.Count; i++)
        {
            _workingLayers[i].Order = i;
        }

        RebuildLayersList();
    }

    private void BtnRefreshFiles_Click(object sender, RoutedEventArgs e)
    {
        OfflineDataService.EnsureDirectoriesExist();
        RefreshView();
    }

    private void BtnResetPmTiles_Click(object sender, RoutedEventArgs e)
    {
        TxtPmTilesDir.Text = OfflineDataService.DefaultPhotinoPmTilesDir;
    }

    private void BtnResetDem_Click(object sender, RoutedEventArgs e)
    {
        TxtDemDir.Text = OfflineDataService.DefaultPhotinoDemDir;
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void BtnSaveOnly_Click(object sender, RoutedEventArgs e)
    {
        SaveData();
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void BtnApplyAndUse_Click(object sender, RoutedEventArgs e)
    {
        SaveData();
        ApplyAndUseRequested?.Invoke(this, EventArgs.Empty);
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SaveData()
    {
        if (OfflineDataService.CanConfigurePaths)
        {
            OfflineDataService.SetCustomPaths(TxtPmTilesDir.Text, TxtDemDir.Text);
        }

        // Save selected style file
        if (CmbStyleFile != null && CmbStyleFile.SelectedItem is string selectedStyle &&
            !string.IsNullOrWhiteSpace(selectedStyle) &&
            !selectedStyle.StartsWith("(", StringComparison.Ordinal))
        {
            OfflineDataService.SelectedStyleFile = selectedStyle;
        }
        else
        {
            OfflineDataService.SelectedStyleFile = null;
        }

        for (int i = 0; i < _workingLayers.Count; i++)
        {
            _workingLayers[i].Order = i;
        }

        OfflineDataService.SaveConfiguredLayers(_workingLayers);
    }

    public void UpdateTheme(bool isDark)
    {
        var tm = ThemeManager.Current;

        if (CardBorder != null)
        {
            CardBorder.Background = tm.HeaderBackground;
            CardBorder.BorderBrush = tm.HeaderBorder;
        }

        if (Divider1 != null) Divider1.Background = tm.HeaderBorder;
        if (Divider2 != null) Divider2.Background = tm.HeaderBorder;
        if (Divider3 != null) Divider3.Background = tm.HeaderBorder;
        if (Divider4 != null) Divider4.Background = tm.HeaderBorder;

        if (TxtDialogTitle != null) TxtDialogTitle.Foreground = tm.TextPrimary;
        if (TxtSectionFolders != null) TxtSectionFolders.Foreground = tm.Accent;
        if (TxtSectionLayers != null) TxtSectionLayers.Foreground = tm.Accent;
        if (TxtSectionStyle != null) TxtSectionStyle.Foreground = tm.Accent;
        if (TxtLayerInstructions != null) TxtLayerInstructions.Foreground = tm.TextSecondary;
        if (TxtStyleInstructions != null) TxtStyleInstructions.Foreground = tm.TextSecondary;
        if (TxtStyleFoundCount != null) TxtStyleFoundCount.Foreground = tm.TextSecondary;

        if (CmbStyleFile != null)
        {
            CmbStyleFile.Background = tm.ButtonInactiveBackground;
            CmbStyleFile.Foreground = tm.TextPrimary;
            CmbStyleFile.BorderBrush = tm.ButtonInactiveBorder;
        }

        if (TxtPmTilesDir != null)
        {
            TxtPmTilesDir.Background = tm.ButtonInactiveBackground;
            TxtPmTilesDir.Foreground = tm.TextPrimary;
            TxtPmTilesDir.BorderBrush = tm.ButtonInactiveBorder;
        }

        if (TxtDemDir != null)
        {
            TxtDemDir.Background = tm.ButtonInactiveBackground;
            TxtDemDir.Foreground = tm.TextPrimary;
            TxtDemDir.BorderBrush = tm.ButtonInactiveBorder;
        }

        if (StorageInfoBadge != null)
        {
            StorageInfoBadge.Background = tm.MapOverlayBackground;
            StorageInfoBadge.BorderBrush = tm.MapOverlayBorder;
        }

        if (TxtDemStatus != null) TxtDemStatus.Foreground = tm.TextPrimary;
        if (TxtStorageHint != null) TxtStorageHint.Foreground = tm.TextSecondary;

        if (NoFilesBorder != null)
        {
            NoFilesBorder.Background = tm.MapOverlayBackground;
            NoFilesBorder.BorderBrush = tm.MapOverlayBorder;
        }

        if (TxtNoFiles != null) TxtNoFiles.Foreground = tm.TextSecondary;

        if (BtnApplyAndUse != null)
        {
            BtnApplyAndUse.Background = tm.Accent;
            BtnApplyAndUse.Foreground = new SolidColorBrush(Colors.White);
        }
    }
}
