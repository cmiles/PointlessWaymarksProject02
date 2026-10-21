#nullable enable
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using WaterDaze.ViewModels;

namespace WaterDaze.Controls
{
    public partial class AppUpdateWindowControl : UserControl
    {
        private MainViewModel? _subscribedVm;

        public AppUpdateWindowControl()
        {
            this.InitializeComponent();

            this.Loaded += AppUpdateWindowControl_Loaded;
            this.Unloaded += AppUpdateWindowControl_Unloaded;
            this.DataContextChanged += AppUpdateWindowControl_DataContextChanged;
        }

        private void AppUpdateWindowControl_Loaded(object sender, RoutedEventArgs e)
        {
            HookDataContext();
        }

        private void AppUpdateWindowControl_Unloaded(object sender, RoutedEventArgs e)
        {
            UnhookDataContext();
        }

        private void AppUpdateWindowControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            HookDataContext();
        }

        private void HookDataContext()
        {
            if (_subscribedVm != null)
            {
                _subscribedVm.PropertyChanged -= OnViewModelPropertyChanged;
                _subscribedVm = null;
            }

            if (DataContext is MainViewModel vm)
            {
                _subscribedVm = vm;
                _subscribedVm.PropertyChanged += OnViewModelPropertyChanged;
                UpdateProgressBar(vm.UpdateDownloadPercent);
            }
        }

        private void UnhookDataContext()
        {
            if (_subscribedVm != null)
            {
                _subscribedVm.PropertyChanged -= OnViewModelPropertyChanged;
                _subscribedVm = null;
            }
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.UpdateDownloadPercent))
            {
                if (DataContext is MainViewModel vm)
                {
                    Dispatcher.BeginInvoke(() => UpdateProgressBar(vm.UpdateDownloadPercent));
                }
            }
        }

        public void UpdateProgressBar(double percent)
        {
            if (ColProgressFill == null || ColProgressRemaining == null) return;

            double clamped = Math.Clamp(percent, 0, 100);
            double remaining = Math.Max(0, 100 - clamped);

            ColProgressFill.Width = new GridLength(clamped, GridUnitType.Star);
            ColProgressRemaining.Width = new GridLength(remaining, GridUnitType.Star);
        }

        private void UpdateNow_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm && !vm.IsDownloadingUpdate)
            {
                _ = vm.StartUpdateAsync();
            }
        }

        private void Dismiss_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm && !vm.IsDownloadingUpdate)
            {
                vm.IsUpdateWindowOpen = false;
            }
        }
    }
}
