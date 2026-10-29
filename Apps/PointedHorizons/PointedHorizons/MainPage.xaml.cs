using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PointedHorizons.Services;
using PointedHorizons.ViewModels;

namespace PointedHorizons;

public partial class MainPage : Page
{
    public MainViewModel ViewModel { get; }

    public MainPage()
    {
        InitializeComponent();

        ViewModel = new MainViewModel();
        DataContext = ViewModel;

        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        ThemeManager.Current.ThemeChanged += ThemeManager_ThemeChanged;
        Loaded += MainPage_Loaded;
    }

    private void ThemeManager_ThemeChanged(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            UpdateTheme(ThemeManager.Current.IsDark);
        });
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsDarkTheme))
        {
            UpdateTheme(ViewModel.IsDarkTheme);
        }
        else if (e.PropertyName == nameof(MainViewModel.LocationPresetName))
        {
            UpdatePresetButtonsTheme(ViewModel.IsDarkTheme);
        }
    }

    private void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateTheme(ViewModel.IsDarkTheme);
        ViewModel.ScheduleHorizonGeneration(immediate: true);
        ViewModel.ScheduleFarthestVisibleFeatureCalculation();
    }

    private void UpdateTheme(bool isDark)
    {
        var tm = ThemeManager.Current;

        // 1. Top Bar & App Header
        if (TopHeaderBorder != null)
        {
            TopHeaderBorder.Background = tm.HeaderBackground;
            TopHeaderBorder.BorderBrush = tm.HeaderBorder;
        }
        if (TxtAppTitle != null)
        {
            TxtAppTitle.Foreground = tm.TextPrimary;
        }
        if (TxtAppSubtitle != null)
        {
            TxtAppSubtitle.Foreground = tm.TextSecondary;
        }
        if (TxtPresetsLabel != null)
        {
            TxtPresetsLabel.Foreground = tm.TextSecondary;
        }

        // 2. Main Root Grid Background
        if (MainRootGrid != null)
        {
            MainRootGrid.Background = tm.PageBackground;
        }

        // 3. Middle Split Directions Bar & Refresh Button
        if (SplitBarBorder != null)
        {
            SplitBarBorder.Background = tm.SplitBarBackground;
            SplitBarBorder.BorderBrush = tm.SplitBarBorder;
        }
        if (BtnRefresh != null)
        {
            BtnRefresh.Background = tm.ButtonInactiveBackground;
            BtnRefresh.BorderBrush = tm.ButtonInactiveBorder;
            BtnRefresh.Foreground = tm.Accent;
        }

        // 4. Update Preset Buttons
        UpdatePresetButtonsTheme(isDark);
    }

    private void UpdatePresetButtonsTheme(bool isDark)
    {
        string selectedName = ViewModel.LocationPresetName;
        var tm = ThemeManager.Current;

        var selectedBg = tm.ButtonActiveBackground;
        var selectedBorder = tm.ButtonActiveBorder;
        var selectedFg = tm.ButtonActiveForeground;

        var unselectedBg = tm.ButtonInactiveBackground;
        var unselectedBorder = tm.ButtonInactiveBorder;
        var unselectedFg = tm.ButtonInactiveForeground;

        void StylePreset(Button btn, string presetName)
        {
            if (btn == null) return;
            bool isSelected = string.Equals(selectedName, presetName, StringComparison.OrdinalIgnoreCase);

            btn.Background = isSelected ? selectedBg : unselectedBg;
            btn.BorderBrush = isSelected ? selectedBorder : unselectedBorder;
            btn.Foreground = isSelected ? selectedFg : unselectedFg;
            btn.FontWeight = isSelected ? FontWeights.Bold : FontWeights.SemiBold;
        }

        StylePreset(BtnPresetRincon, "Tucson East (Rincon Ridges), AZ");
        StylePreset(BtnPresetLemmon, "Mount Lemmon, AZ");
        StylePreset(BtnPresetRainier, "Mount Rainier (Paradise), WA");
        StylePreset(BtnPresetCanyon, "Grand Canyon (Mather Point), AZ");
        StylePreset(BtnPresetPikes, "Pikes Peak, CO");
        StylePreset(BtnPresetYosemite, "Yosemite (Glacier Point), CA");
        StylePreset(BtnPresetWashington, "Mount Washington, NH");
    }

    private void BtnPresetRincon_Click(object sender, RoutedEventArgs e) =>
        ApplyPresetByName("Tucson East (Rincon Ridges), AZ");

    private void BtnPresetLemmon_Click(object sender, RoutedEventArgs e) =>
        ApplyPresetByName("Mount Lemmon, AZ");

    private void BtnPresetRainier_Click(object sender, RoutedEventArgs e) =>
        ApplyPresetByName("Mount Rainier (Paradise), WA");

    private void BtnPresetCanyon_Click(object sender, RoutedEventArgs e) =>
        ApplyPresetByName("Grand Canyon (Mather Point), AZ");

    private void BtnPresetPikes_Click(object sender, RoutedEventArgs e) =>
        ApplyPresetByName("Pikes Peak, CO");

    private void BtnPresetYosemite_Click(object sender, RoutedEventArgs e) =>
        ApplyPresetByName("Yosemite (Glacier Point), CA");

    private void BtnPresetWashington_Click(object sender, RoutedEventArgs e) =>
        ApplyPresetByName("Mount Washington, NH");

    private void ApplyPresetByName(string name)
    {
        var preset = ViewModel.Presets.FirstOrDefault(p => p.Name == name);
        if (preset != null)
        {
            ViewModel.ApplyPreset(preset);
        }
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ScheduleHorizonGeneration(immediate: true);
    }
}
