using System.ComponentModel;
using System.Windows;
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
    }

    public MainViewModel ViewModel { get; }

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
        if (e.PropertyName == nameof(MainViewModel.IsLoading) ||
            e.PropertyName == nameof(MainViewModel.HasError) ||
            e.PropertyName == nameof(MainViewModel.HasWarning))
            UpdateBannerVisibilities();
    }
}