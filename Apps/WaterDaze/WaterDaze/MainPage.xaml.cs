using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
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

        Loaded += (_, _) => UpdateResponsiveLayout(ActualWidth > 0 ? ActualWidth : RankingsGrid?.ActualWidth ?? 0);
        SizeChanged += (_, e) => UpdateResponsiveLayout(e.NewSize.Width > 0 ? e.NewSize.Width : ActualWidth);
    }

    public MainViewModel ViewModel { get; }

    public void UpdateResponsiveLayout(double width)
    {
        if (RankingsGrid == null || CardWetStreaks == null || CardDryStreaks == null ||
            CardAnnualMeanFlow == null || CardPeakFlowDays == null)
            return;

        var isCompact = width is > 0 and < 720;

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
}