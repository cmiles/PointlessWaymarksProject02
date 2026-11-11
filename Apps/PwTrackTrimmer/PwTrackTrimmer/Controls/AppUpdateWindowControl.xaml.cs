#nullable enable
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using OpenSilver;
using PwTrackTrimmer.ViewModels;

namespace PwTrackTrimmer.Controls
{
    public partial class AppUpdateWindowControl : UserControl
    {
        private TrackTrimmerViewModel? _subscribedVm;
        private bool _isCompact;

        public AppUpdateWindowControl()
        {
            this.InitializeComponent();

            this.Loaded += AppUpdateWindowControl_Loaded;
            this.Unloaded += AppUpdateWindowControl_Unloaded;
            this.DataContextChanged += AppUpdateWindowControl_DataContextChanged;
            this.SizeChanged += AppUpdateWindowControl_SizeChanged;
        }

        private void AppUpdateWindowControl_Loaded(object sender, RoutedEventArgs e)
        {
            HookDataContext();
            ApplyResponsiveLayout(ActualWidth);
        }

        private void AppUpdateWindowControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width > 0)
            {
                ApplyResponsiveLayout(e.NewSize.Width);
            }
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

            if (DataContext is TrackTrimmerViewModel vm)
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
            if (e.PropertyName == nameof(TrackTrimmerViewModel.UpdateDownloadPercent))
            {
                if (DataContext is TrackTrimmerViewModel vm)
                {
                    Dispatcher.BeginInvoke(() => UpdateProgressBar(vm.UpdateDownloadPercent));
                }
            }
            else if (e.PropertyName == nameof(TrackTrimmerViewModel.UpdateInstallerFileName))
            {
                Dispatcher.BeginInvoke(ApplyWordBreakStyles);
            }
        }

        public void ApplyResponsiveLayout(double width)
        {
            if (width <= 0) width = ActualWidth;
            if (width <= 0) return;

            bool isCompact = width < 460;
            _isCompact = isCompact;

            if (PackageInfoGrid != null && InfoPackageLabelText != null && InfoPackageValueText != null)
            {
                if (isCompact)
                {
                    // Small screen: stack label on row 0 and package value on row 1 spanning full width
                    Grid.SetRow(InfoPackageValueText, 1);
                    Grid.SetColumn(InfoPackageValueText, 0);
                    Grid.SetColumnSpan(InfoPackageValueText, 2);
                    InfoPackageLabelText.Margin = new Thickness(0, 0, 0, 2);
                    InfoPackageValueText.Margin = new Thickness(0, 0, 0, 4);
                }
                else
                {
                    // Desktop / wide screen: label in column 0, package value in column 1
                    Grid.SetRow(InfoPackageValueText, 0);
                    Grid.SetColumn(InfoPackageValueText, 1);
                    Grid.SetColumnSpan(InfoPackageValueText, 1);
                    InfoPackageLabelText.Margin = new Thickness(0, 0, 4, 0);
                    InfoPackageValueText.Margin = new Thickness(0);
                }
            }

            ApplyWordBreakStyles();
        }

        private void ApplyWordBreakStyles()
        {
            try
            {
                if (InfoPackageValueText != null)
                {
                    object? div = Interop.GetDiv(InfoPackageValueText);
                    if (div != null)
                    {
                        Interop.ExecuteJavaScriptVoid(@"
                            var el = $0;
                            if (el) {
                                el.style.wordBreak = 'break-word';
                                el.style.overflowWrap = 'anywhere';
                            }
                        ", div);
                    }
                }
            }
            catch { }
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
            if (DataContext is TrackTrimmerViewModel vm && !vm.IsDownloadingUpdate)
            {
                _ = vm.StartUpdateAsync();
            }
        }

        private void Dismiss_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is TrackTrimmerViewModel vm && !vm.IsDownloadingUpdate)
            {
                vm.IsUpdateWindowOpen = false;
            }
        }
    }
}
