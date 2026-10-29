using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
        Loaded += MainPage_Loaded;
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
    }

    private void UpdateTheme(bool isDark)
    {
        // 1. Top Bar & App Header
        if (TopHeaderBorder != null)
        {
            TopHeaderBorder.Background = new SolidColorBrush(isDark ? Color.FromArgb(255, 11, 15, 25) : Color.FromArgb(255, 255, 255, 255));
            TopHeaderBorder.BorderBrush = new SolidColorBrush(isDark ? Color.FromArgb(255, 30, 41, 59) : Color.FromArgb(255, 226, 232, 240));
        }
        if (TxtAppTitle != null)
        {
            TxtAppTitle.Foreground = new SolidColorBrush(isDark ? Color.FromArgb(255, 255, 255, 255) : Color.FromArgb(255, 15, 23, 42));
        }
        if (TxtAppSubtitle != null)
        {
            TxtAppSubtitle.Foreground = new SolidColorBrush(isDark ? Color.FromArgb(255, 148, 163, 184) : Color.FromArgb(255, 100, 116, 139));
        }
        if (TxtPresetsLabel != null)
        {
            TxtPresetsLabel.Foreground = new SolidColorBrush(isDark ? Color.FromArgb(255, 148, 163, 184) : Color.FromArgb(255, 71, 85, 105));
        }

        // 2. Main Root Grid Background
        if (MainRootGrid != null)
        {
            MainRootGrid.Background = new SolidColorBrush(isDark ? Color.FromArgb(255, 7, 10, 16) : Color.FromArgb(255, 241, 245, 249));
        }

        // 3. Middle Split Directions Bar & Refresh Button
        if (SplitBarBorder != null)
        {
            SplitBarBorder.Background = new SolidColorBrush(isDark ? Color.FromArgb(255, 10, 14, 23) : Color.FromArgb(255, 248, 250, 252));
            SplitBarBorder.BorderBrush = new SolidColorBrush(isDark ? Color.FromArgb(255, 30, 41, 59) : Color.FromArgb(255, 226, 232, 240));
        }
        if (BtnRefresh != null)
        {
            BtnRefresh.Background = new SolidColorBrush(isDark ? Color.FromArgb(255, 30, 41, 59) : Color.FromArgb(255, 241, 245, 249));
            BtnRefresh.BorderBrush = new SolidColorBrush(isDark ? Color.FromArgb(255, 51, 65, 85) : Color.FromArgb(255, 203, 213, 225));
            BtnRefresh.Foreground = new SolidColorBrush(isDark ? Color.FromArgb(255, 56, 189, 248) : Color.FromArgb(255, 2, 132, 199));
        }

        // 4. Update Preset Buttons
        UpdatePresetButtonsTheme(isDark);
    }

    private void UpdatePresetButtonsTheme(bool isDark)
    {
        string selectedName = ViewModel.LocationPresetName;

        var selectedBg = new SolidColorBrush(Color.FromArgb(255, 2, 132, 199));     // #0284C7
        var selectedBorder = new SolidColorBrush(Color.FromArgb(255, 2, 132, 199)); // #0284C7
        var selectedFg = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));   // #FFFFFF

        // Dark Theme unselected: Slate dark background #1E293B, border #334155, crisp white text #F8FAFC
        var darkUnselectedBg = new SolidColorBrush(Color.FromArgb(255, 30, 41, 59));
        var darkUnselectedBorder = new SolidColorBrush(Color.FromArgb(255, 51, 65, 85));
        var darkUnselectedFg = new SolidColorBrush(Color.FromArgb(255, 248, 250, 252));

        // Light Theme unselected: Clean light slate background #F1F5F9, border #CBD5E1, deep dark slate text #0F172A
        var lightUnselectedBg = new SolidColorBrush(Color.FromArgb(255, 241, 245, 249));
        var lightUnselectedBorder = new SolidColorBrush(Color.FromArgb(255, 203, 213, 225));
        var lightUnselectedFg = new SolidColorBrush(Color.FromArgb(255, 15, 23, 42));

        var unselectedBg = isDark ? darkUnselectedBg : lightUnselectedBg;
        var unselectedBorder = isDark ? darkUnselectedBorder : lightUnselectedBorder;
        var unselectedFg = isDark ? darkUnselectedFg : lightUnselectedFg;

        void StylePreset(Button btn, TextBlock tb, string presetName)
        {
            if (btn == null) return;
            bool isSelected = string.Equals(selectedName, presetName, StringComparison.OrdinalIgnoreCase);

            var bg = isSelected ? selectedBg : unselectedBg;
            var border = isSelected ? selectedBorder : unselectedBorder;
            var fg = isSelected ? selectedFg : unselectedFg;

            btn.Background = bg;
            btn.BorderBrush = border;
            btn.Foreground = fg;
            if (tb != null)
            {
                tb.Foreground = fg;
                tb.FontWeight = isSelected ? FontWeights.Bold : FontWeights.SemiBold;
            }
        }

        StylePreset(BtnPresetRincon, TxtPresetRincon, "Tucson East (Rincon Ridges), AZ");
        StylePreset(BtnPresetLemmon, TxtPresetLemmon, "Mount Lemmon, AZ");
        StylePreset(BtnPresetRainier, TxtPresetRainier, "Mount Rainier (Paradise), WA");
        StylePreset(BtnPresetCanyon, TxtPresetCanyon, "Grand Canyon (Mather Point), AZ");
        StylePreset(BtnPresetPikes, TxtPresetPikes, "Pikes Peak, CO");
        StylePreset(BtnPresetYosemite, TxtPresetYosemite, "Yosemite (Glacier Point), CA");
        StylePreset(BtnPresetWashington, TxtPresetWashington, "Mount Washington, NH");
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
