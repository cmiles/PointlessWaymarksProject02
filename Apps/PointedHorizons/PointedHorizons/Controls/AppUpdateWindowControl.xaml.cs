#nullable enable
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using OpenSilver;
using PointedHorizons.ViewModels;

namespace PointedHorizons.Controls
{
    public partial class AppUpdateWindowControl : UserControl
    {
        private MainViewModel? _subscribedVm;
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

            if (DataContext is MainViewModel vm)
            {
                _subscribedVm = vm;
                _subscribedVm.PropertyChanged += OnViewModelPropertyChanged;
                UpdateProgressBar(vm.UpdateDownloadPercent);
                UpdateVersionDisplay(vm);
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
            else if (e.PropertyName == nameof(MainViewModel.UpdateCurrentVersion) ||
                     e.PropertyName == nameof(MainViewModel.UpdateNewVersion) ||
                     e.PropertyName == nameof(MainViewModel.ShowNewVersionComparison))
            {
                if (DataContext is MainViewModel vm)
                {
                    Dispatcher.BeginInvoke(() => UpdateVersionDisplay(vm));
                }
            }
            else if (e.PropertyName == nameof(MainViewModel.UpdateInstallerFileName))
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
                    InfoPackageLabelText.Margin = new Thickness(0, 4, 0, 0);
                    InfoPackageValueText.Margin = new Thickness(0);
                }
            }

            if (ProgressInfoGrid != null && ProgressStatusText != null && ProgressPercentText != null)
            {
                if (isCompact)
                {
                    // Small screen: stack status on row 0 and file size / percent on row 1
                    Grid.SetRow(ProgressPercentText, 1);
                    Grid.SetColumn(ProgressPercentText, 0);
                    Grid.SetColumnSpan(ProgressPercentText, 2);
                    ProgressPercentText.TextAlignment = TextAlignment.Left;
                    ProgressPercentText.Margin = new Thickness(0, 4, 0, 0);
                }
                else
                {
                    // Desktop: side by side
                    Grid.SetRow(ProgressPercentText, 0);
                    Grid.SetColumn(ProgressPercentText, 1);
                    Grid.SetColumnSpan(ProgressPercentText, 1);
                    ProgressPercentText.TextAlignment = TextAlignment.Right;
                    ProgressPercentText.Margin = new Thickness(0);
                }
            }

            if (FooterButtonsGrid != null && BtnDismiss != null && BtnUpdate != null)
            {
                if (isCompact)
                {
                    // Small screen: stack buttons vertically
                    Grid.SetRow(BtnDismiss, 1);
                    Grid.SetColumn(BtnDismiss, 0);
                    Grid.SetColumnSpan(BtnDismiss, 2);
                    BtnDismiss.HorizontalAlignment = HorizontalAlignment.Stretch;
                    BtnDismiss.Margin = new Thickness(0, 6, 0, 0);

                    Grid.SetRow(BtnUpdate, 0);
                    Grid.SetColumn(BtnUpdate, 0);
                    Grid.SetColumnSpan(BtnUpdate, 2);
                    BtnUpdate.HorizontalAlignment = HorizontalAlignment.Stretch;
                    BtnUpdate.Margin = new Thickness(0);
                }
                else
                {
                    Grid.SetRow(BtnDismiss, 0);
                    Grid.SetColumn(BtnDismiss, 0);
                    Grid.SetColumnSpan(BtnDismiss, 1);
                    BtnDismiss.HorizontalAlignment = HorizontalAlignment.Left;
                    BtnDismiss.Margin = new Thickness(0);

                    Grid.SetRow(BtnUpdate, 0);
                    Grid.SetColumn(BtnUpdate, 1);
                    Grid.SetColumnSpan(BtnUpdate, 1);
                    BtnUpdate.HorizontalAlignment = HorizontalAlignment.Right;
                    BtnUpdate.Margin = new Thickness(0);
                }
            }

            ApplyWordBreakStyles();
        }

        private void ApplyWordBreakStyles()
        {
            try
            {
                void SetBreak(FrameworkElement? element)
                {
                    if (element == null) return;
                    object? div = Interop.GetDiv(element);
                    if (div != null)
                    {
                        Interop.ExecuteJavaScriptVoid(@"
                            var el = $0;
                            if (el) {
                                el.style.wordBreak = 'break-word';
                                el.style.overflowWrap = 'anywhere';
                                el.style.whiteSpace = 'normal';
                            }
                        ", div);
                    }
                }

                SetBreak(InfoPackageValueText);
                SetBreak(ProgressStatusText);
                SetBreak(ProgressPercentText);
                SetBreak(HeaderTitleText);
                SetBreak(UpdateStatusTextBlock);
            }
            catch { }
        }

        public void UpdateVersionDisplay(MainViewModel vm)
        {
            bool showComparison = vm.ShowNewVersionComparison;
            if (VersionArrowText != null)
                VersionArrowText.Visibility = showComparison ? Visibility.Visible : Visibility.Collapsed;
            if (NewVersionBox != null)
                NewVersionBox.Visibility = showComparison ? Visibility.Visible : Visibility.Collapsed;
            if (CurrentVersionBox != null)
                Grid.SetColumnSpan(CurrentVersionBox, showComparison ? 1 : 3);
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

