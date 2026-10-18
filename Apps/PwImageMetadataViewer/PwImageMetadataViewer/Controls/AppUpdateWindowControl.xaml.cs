#nullable enable
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PwImageMetadataViewer.Services;
using PwImageMetadataViewer.ViewModels;

namespace PwImageMetadataViewer.Controls
{
    public partial class AppUpdateWindowControl : UserControl
    {
        private UpdateViewModel? _subscribedVm;

        public AppUpdateWindowControl()
        {
            this.InitializeComponent();

            this.Loaded += AppUpdateWindowControl_Loaded;
            this.Unloaded += AppUpdateWindowControl_Unloaded;
            this.DataContextChanged += AppUpdateWindowControl_DataContextChanged;
        }

        private void AppUpdateWindowControl_Loaded(object sender, RoutedEventArgs e)
        {
            ThemeManager.Current.ThemeChanged += OnThemeChanged;
            ApplyTheme(ThemeManager.Current.IsDark);
            HookDataContext();
        }

        private void AppUpdateWindowControl_Unloaded(object sender, RoutedEventArgs e)
        {
            ThemeManager.Current.ThemeChanged -= OnThemeChanged;
            UnhookDataContext();
        }

        private void AppUpdateWindowControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            HookDataContext();
        }

        private void OnThemeChanged(object? sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(() => ApplyTheme(ThemeManager.Current.IsDark));
        }

        private void HookDataContext()
        {
            if (_subscribedVm != null)
            {
                _subscribedVm.PropertyChanged -= OnViewModelPropertyChanged;
                _subscribedVm = null;
            }

            if (DataContext is UpdateViewModel vm)
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
            if (e.PropertyName == nameof(UpdateViewModel.UpdateDownloadPercent))
            {
                if (DataContext is UpdateViewModel vm)
                {
                    Dispatcher.BeginInvoke(() => UpdateProgressBar(vm.UpdateDownloadPercent));
                }
            }
            else if (e.PropertyName == nameof(UpdateViewModel.IsUpdateWindowOpen))
            {
                if (DataContext is UpdateViewModel vm && vm.IsUpdateWindowOpen)
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        ApplyTheme(ThemeManager.Current.IsDark);
                        UpdateProgressBar(vm.UpdateDownloadPercent);
                    });
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

        public void ApplyTheme(bool isDark)
        {
            if (WindowRootBorder == null) return;

            if (isDark)
            {
                // DARK MODE
                WindowRootBorder.Background = ThemeManager.BrushDarkCardBg;      // #161F30
                WindowRootBorder.BorderBrush = ThemeManager.BrushSkyAccentDark;   // #38BDF8

                if (HeaderBorder != null)
                {
                    HeaderBorder.Background = ThemeManager.BrushDarkSubCardBg;    // #0F172A
                    HeaderBorder.BorderBrush = ThemeManager.BrushDarkCardBorder;   // #273549
                }
                if (HeaderTitleText != null) HeaderTitleText.Foreground = ThemeManager.BrushDarkTextPrimary; // #F8FAFC
                if (BtnCloseHeader != null)
                {
                    BtnCloseHeader.Background = ThemeManager.BrushDarkCardBorder;
                    BtnCloseHeader.Foreground = ThemeManager.BrushDarkTextSecondary;
                }

                if (BodyScrollViewer != null) BodyScrollViewer.Background = ThemeManager.BrushDarkPageBg; // #0B0F19

                if (MainCardBorder != null)
                {
                    MainCardBorder.Background = ThemeManager.BrushDarkCardBg;
                    MainCardBorder.BorderBrush = ThemeManager.BrushDarkCardBorder;
                }
                if (UpdateStatusTextBlock != null) UpdateStatusTextBlock.Foreground = ThemeManager.BrushDarkTextSecondary;

                if (CurrentVersionBox != null)
                {
                    CurrentVersionBox.Background = ThemeManager.BrushDarkSubCardBg;
                    CurrentVersionBox.BorderBrush = ThemeManager.BrushDarkCardBorder;
                }
                if (CurrentVersionLabelText != null) CurrentVersionLabelText.Foreground = ThemeManager.BrushDarkTextMuted;
                if (CurrentVersionValueText != null) CurrentVersionValueText.Foreground = ThemeManager.BrushDarkTextPrimary;
                if (VersionArrowText != null) VersionArrowText.Foreground = ThemeManager.BrushSkyAccentDark;

                if (NewVersionBox != null)
                {
                    NewVersionBox.Background = new SolidColorBrush(Color.FromArgb(255, 6, 78, 59));
                    NewVersionBox.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 5, 150, 105));
                }
                if (NewVersionLabelText != null) NewVersionLabelText.Foreground = new SolidColorBrush(Color.FromArgb(255, 167, 243, 208));
                if (NewVersionValueText != null) NewVersionValueText.Foreground = new SolidColorBrush(Color.FromArgb(255, 52, 211, 153));

                if (InfoCardBorder != null)
                {
                    InfoCardBorder.Background = ThemeManager.BrushDarkCardBg;
                    InfoCardBorder.BorderBrush = ThemeManager.BrushDarkCardBorder;
                }
                if (InfoPackageLabelText != null) InfoPackageLabelText.Foreground = ThemeManager.BrushDarkTextMuted;
                if (InfoPackageValueText != null) InfoPackageValueText.Foreground = ThemeManager.BrushSkyAccentDark;
                if (InfoExplanationText != null) InfoExplanationText.Foreground = ThemeManager.BrushDarkTextMuted;

                if (ProgressCardBorder != null)
                {
                    ProgressCardBorder.Background = ThemeManager.BrushDarkCardBg;
                    ProgressCardBorder.BorderBrush = ThemeManager.BrushSkyAccentDark;
                }
                if (ProgressStatusText != null) ProgressStatusText.Foreground = ThemeManager.BrushSkyAccentDark;
                if (ProgressPercentText != null) ProgressPercentText.Foreground = ThemeManager.BrushSkyAccentDark;
                if (ProgressBarTrack != null)
                {
                    ProgressBarTrack.Background = ThemeManager.BrushDarkSubCardBg;
                    ProgressBarTrack.BorderBrush = ThemeManager.BrushDarkDivider;
                }
                if (ProgressBarFill != null) ProgressBarFill.Background = ThemeManager.BrushSkyAccentDark;

                if (FooterBorder != null)
                {
                    FooterBorder.Background = ThemeManager.BrushDarkSubCardBg;
                    FooterBorder.BorderBrush = ThemeManager.BrushDarkDivider;
                }
                if (BtnDismiss != null)
                {
                    BtnDismiss.Background = ThemeManager.BrushDarkCardBorder;
                    BtnDismiss.BorderBrush = ThemeManager.BrushDarkCardBorder;
                    BtnDismiss.Foreground = ThemeManager.BrushDarkTextPrimary;
                }
                if (BtnUpdate != null)
                {
                    BtnUpdate.Background = ThemeManager.BrushSkyAccentLight;
                    BtnUpdate.Foreground = ThemeManager.BrushLightCardBg;
                }
            }
            else
            {
                // LIGHT MODE
                WindowRootBorder.Background = ThemeManager.BrushLightCardBg;       // #FFFFFF
                WindowRootBorder.BorderBrush = ThemeManager.BrushSkyAccentLight;   // #0284C7

                if (HeaderBorder != null)
                {
                    HeaderBorder.Background = ThemeManager.BrushLightSubCardBg;    // #F1F5F9
                    HeaderBorder.BorderBrush = ThemeManager.BrushLightCardBorder;   // #E2E8F0
                }
                if (HeaderTitleText != null) HeaderTitleText.Foreground = ThemeManager.BrushLightTextPrimary; // #0F172A
                if (BtnCloseHeader != null)
                {
                    BtnCloseHeader.Background = ThemeManager.BrushLightCardBorder;
                    BtnCloseHeader.Foreground = ThemeManager.BrushLightTextSecondary;
                }

                if (BodyScrollViewer != null) BodyScrollViewer.Background = ThemeManager.BrushLightPageBg; // #F8FAFC

                if (MainCardBorder != null)
                {
                    MainCardBorder.Background = ThemeManager.BrushLightCardBg;
                    MainCardBorder.BorderBrush = ThemeManager.BrushLightCardBorder;
                }
                if (UpdateStatusTextBlock != null) UpdateStatusTextBlock.Foreground = ThemeManager.BrushLightTextSecondary;

                if (CurrentVersionBox != null)
                {
                    CurrentVersionBox.Background = ThemeManager.BrushLightSubCardBg;
                    CurrentVersionBox.BorderBrush = ThemeManager.BrushLightSubCardBorder;
                }
                if (CurrentVersionLabelText != null) CurrentVersionLabelText.Foreground = ThemeManager.BrushLightTextMuted;
                if (CurrentVersionValueText != null) CurrentVersionValueText.Foreground = ThemeManager.BrushLightTextPrimary;
                if (VersionArrowText != null) VersionArrowText.Foreground = ThemeManager.BrushSkyAccentLight;

                if (NewVersionBox != null)
                {
                    NewVersionBox.Background = new SolidColorBrush(Color.FromArgb(255, 236, 253, 245)); // Mint 50
                    NewVersionBox.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 5, 150, 105));
                }
                if (NewVersionLabelText != null) NewVersionLabelText.Foreground = new SolidColorBrush(Color.FromArgb(255, 4, 120, 87));
                if (NewVersionValueText != null) NewVersionValueText.Foreground = new SolidColorBrush(Color.FromArgb(255, 5, 150, 105));

                if (InfoCardBorder != null)
                {
                    InfoCardBorder.Background = ThemeManager.BrushLightCardBg;
                    InfoCardBorder.BorderBrush = ThemeManager.BrushLightCardBorder;
                }
                if (InfoPackageLabelText != null) InfoPackageLabelText.Foreground = ThemeManager.BrushLightTextMuted;
                if (InfoPackageValueText != null) InfoPackageValueText.Foreground = ThemeManager.BrushSkyAccentLight;
                if (InfoExplanationText != null) InfoExplanationText.Foreground = ThemeManager.BrushLightTextMuted;

                if (ProgressCardBorder != null)
                {
                    ProgressCardBorder.Background = ThemeManager.BrushLightCardBg;
                    ProgressCardBorder.BorderBrush = ThemeManager.BrushSkyAccentLight;
                }
                if (ProgressStatusText != null) ProgressStatusText.Foreground = ThemeManager.BrushSkyAccentLight;
                if (ProgressPercentText != null) ProgressPercentText.Foreground = ThemeManager.BrushSkyAccentLight;
                if (ProgressBarTrack != null)
                {
                    ProgressBarTrack.Background = ThemeManager.BrushLightSubCardBg;
                    ProgressBarTrack.BorderBrush = ThemeManager.BrushLightSubCardBorder;
                }
                if (ProgressBarFill != null) ProgressBarFill.Background = ThemeManager.BrushSkyAccentLight;

                if (FooterBorder != null)
                {
                    FooterBorder.Background = ThemeManager.BrushLightSubCardBg;
                    FooterBorder.BorderBrush = ThemeManager.BrushLightDivider;
                }
                if (BtnDismiss != null)
                {
                    BtnDismiss.Background = ThemeManager.BrushLightCardBorder;
                    BtnDismiss.BorderBrush = ThemeManager.BrushLightSubCardBorder;
                    BtnDismiss.Foreground = ThemeManager.BrushLightTextPrimary;
                }
                if (BtnUpdate != null)
                {
                    BtnUpdate.Background = ThemeManager.BrushSkyAccentLight;
                    BtnUpdate.Foreground = ThemeManager.BrushLightCardBg;
                }
            }
        }

        private void UpdateNow_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is UpdateViewModel vm && !vm.IsDownloadingUpdate)
            {
                _ = vm.StartUpdateAsync();
            }
        }

        private void Dismiss_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is UpdateViewModel vm && !vm.IsDownloadingUpdate)
            {
                vm.IsUpdateWindowOpen = false;
            }
        }
    }
}
