using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using WaterDaze.Services;
using WaterDaze.ViewModels;

namespace WaterDaze;

public partial class MainPage
{

    public MainPage()
    {
        InitializeComponent();

        ViewModel = new MainViewModel();
        DataContext = ViewModel;

        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        UpdateBannerVisibilities();

        Loaded += (_, _) =>
        {
            UpdateResponsiveLayout(ActualWidth > 0 ? ActualWidth : RankingsGrid?.ActualWidth ?? 0);
            ApplyTheme();
            Dispatcher.BeginInvoke(() =>
            {
                _ = ViewModel.CheckForUpdatesAsync(silent: true);
            });
        };

        SizeChanged += (_, e) => UpdateResponsiveLayout(e.NewSize.Width > 0 ? e.NewSize.Width : ActualWidth);
        ThemeManager.ThemeChanged += (_, _) => ApplyTheme();
        ApplyTheme();
    }

    public MainViewModel ViewModel { get; }

    public void ApplyTheme()
    {
        SyncThemeSelector();
    }

    private void SyncThemeSelector()
    {
        if (TxtThemeToggle != null)
        {
            TxtThemeToggle.Text = ThemeManager.ThemeButtonLabel;
        }
        if (TxtThemeToggleMobile != null)
        {
            TxtThemeToggleMobile.Text = ThemeManager.ThemeButtonLabel;
        }
    }



    private void BtnThemeToggle_Click(object sender, RoutedEventArgs e)
    {
        ThemeManager.CycleTheme();
    }

    private void Hyperlink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is HyperlinkButton btn && btn.NavigateUri != null)
        {
            try
            {
                OpenSilver.Interop.ExecuteJavaScriptVoid($"window.open('{btn.NavigateUri.AbsoluteUri}', '_blank');");
            }
            catch { }
        }
    }

    public void UpdateResponsiveLayout(double width)
    {
        var effectiveWidth = width > 0 ? width : ActualWidth;
        var isCompact = effectiveWidth is > 0 and < 720;

        if (RankingsGrid == null || CardWetStreaks == null || CardDryStreaks == null ||
            CardAnnualMeanFlow == null || CardPeakFlowDays == null)
            return;

        if (PanelDesktopActions != null)
            PanelDesktopActions.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;

        if (BtnHamburger != null)
            BtnHamburger.Visibility = isCompact ? Visibility.Visible : Visibility.Collapsed;

        if (!isCompact && MobileMenuDropdown != null)
            MobileMenuDropdown.Visibility = Visibility.Collapsed;

        if (isCompact)
        {
            if (RankingsCol1 != null)
                RankingsCol1.Width = new GridLength(0);

            Grid.SetRow(CardWetStreaks, 0);
            Grid.SetColumn(CardWetStreaks, 0);
            Grid.SetColumnSpan(CardWetStreaks, 2);
            CardWetStreaks.Margin = new Thickness(0, 0, 0, 12);

            Grid.SetRow(CardDryStreaks, 1);
            Grid.SetColumn(CardDryStreaks, 0);
            Grid.SetColumnSpan(CardDryStreaks, 2);
            CardDryStreaks.Margin = new Thickness(0, 0, 0, 12);

            Grid.SetRow(CardAnnualMeanFlow, 2);
            Grid.SetColumn(CardAnnualMeanFlow, 0);
            Grid.SetColumnSpan(CardAnnualMeanFlow, 2);
            CardAnnualMeanFlow.Margin = new Thickness(0, 0, 0, 12);

            Grid.SetRow(CardPeakFlowDays, 3);
            Grid.SetColumn(CardPeakFlowDays, 0);
            Grid.SetColumnSpan(CardPeakFlowDays, 2);
            CardPeakFlowDays.Margin = new Thickness(0, 0, 0, 12);
        }
        else
        {
            if (RankingsCol1 != null)
                RankingsCol1.Width = new GridLength(1, GridUnitType.Star);

            Grid.SetRow(CardWetStreaks, 0);
            Grid.SetColumn(CardWetStreaks, 0);
            Grid.SetColumnSpan(CardWetStreaks, 1);
            CardWetStreaks.Margin = new Thickness(0, 0, 6, 12);

            Grid.SetRow(CardDryStreaks, 0);
            Grid.SetColumn(CardDryStreaks, 1);
            Grid.SetColumnSpan(CardDryStreaks, 1);
            CardDryStreaks.Margin = new Thickness(6, 0, 0, 12);

            Grid.SetRow(CardAnnualMeanFlow, 1);
            Grid.SetColumn(CardAnnualMeanFlow, 0);
            Grid.SetColumnSpan(CardAnnualMeanFlow, 1);
            CardAnnualMeanFlow.Margin = new Thickness(0, 0, 6, 12);

            Grid.SetRow(CardPeakFlowDays, 1);
            Grid.SetColumn(CardPeakFlowDays, 1);
            Grid.SetColumnSpan(CardPeakFlowDays, 1);
            CardPeakFlowDays.Margin = new Thickness(6, 0, 0, 12);
        }
    }

    private void UpdateBannerVisibilities()
    {
        if (LoadingBanner != null)
            LoadingBanner.Visibility = ViewModel.IsLoading ? Visibility.Visible : Visibility.Collapsed;

        if (ErrorBanner != null)
            ErrorBanner.Visibility = ViewModel.HasError ? Visibility.Visible : Visibility.Collapsed;

        if (WarningBanner != null)
            WarningBanner.Visibility = ViewModel.HasWarning ? Visibility.Visible : Visibility.Collapsed;
    }
    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.IsLoading) or nameof(MainViewModel.HasError) or nameof(MainViewModel.HasWarning))
            UpdateBannerVisibilities();
    }

    private void BtnUpdates_Click(object sender, RoutedEventArgs e)
    {
        _ = ViewModel.CheckForUpdatesAsync(silent: false);
    }

    private void BtnHamburger_Click(object sender, RoutedEventArgs e)
    {
        if (MobileMenuDropdown != null)
        {
            MobileMenuDropdown.Visibility = MobileMenuDropdown.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
        }
    }

    private void MobileMenuAction_Click(object sender, RoutedEventArgs e)
    {
        if (MobileMenuDropdown != null)
        {
            MobileMenuDropdown.Visibility = Visibility.Collapsed;
        }
    }

    private void BtnUpdatesMobile_Click(object sender, RoutedEventArgs e)
    {
        if (MobileMenuDropdown != null)
        {
            MobileMenuDropdown.Visibility = Visibility.Collapsed;
        }
        _ = ViewModel.CheckForUpdatesAsync(silent: false);
    }
}
