using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OpenSilver;
using PointedHorizons.Services;
using PointedHorizons.ViewModels;

namespace PointedHorizons.Controls;

public partial class HorizonViewControl : UserControl
{
    private bool _isInitialized;
    private bool _isEditingFocalLength;
    private MainViewModel? ViewModel => DataContext as MainViewModel;

    public HorizonViewControl()
    {
        InitializeComponent();
        Loaded += HorizonViewControl_Loaded;
        SizeChanged += HorizonViewControl_SizeChanged;
        DataContextChanged += HorizonViewControl_DataContextChanged;

        ThemeManager.Current.ThemeChanged += (s, e) =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                UpdateTheme(ThemeManager.Current.IsDark);
            });
        };

        LeafletScrollIsolationHelper.ProtectControl(this, CanvasContainer);
    }

    private void HorizonViewControl_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateTheme(ThemeManager.Current.IsDark);
        InitializeCanvas();
    }

    private void HorizonViewControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        RenderScene();
    }

    public void Redraw()
    {
        RenderScene();
    }

    private void HorizonViewControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MainViewModel oldVm)
        {
            oldVm.HorizonUpdated -= Vm_HorizonUpdated;
            oldVm.ViewParametersChanged -= Vm_ViewParametersChanged;
        }

        if (e.NewValue is MainViewModel newVm)
        {
            newVm.HorizonUpdated += Vm_HorizonUpdated;
            newVm.ViewParametersChanged += Vm_ViewParametersChanged;
            UpdateFovButtons(newVm.FieldOfView);
            RenderScene();
        }
    }

    public void UpdateTheme(bool isDark)
    {
        var tm = ThemeManager.Current;

        if (QuickToolbarBorder != null)
        {
            QuickToolbarBorder.Background = tm.MapOverlayBackground;
            QuickToolbarBorder.BorderBrush = tm.MapOverlayBorder;
        }

        if (BtnCompass != null)
        {
            BtnCompass.Background = tm.ButtonInactiveBackground;
            BtnCompass.BorderBrush = tm.ButtonInactiveBorder;
            BtnCompass.Foreground = tm.Accent;
        }

        if (CompassDivider != null)
        {
            CompassDivider.Background = tm.MapOverlayBorder;
        }

        if (ToolbarDivider != null)
        {
            ToolbarDivider.Background = tm.MapOverlayBorder;
        }

        if (BtnHorizonMenu != null)
        {
            BtnHorizonMenu.Background = tm.ButtonInactiveBackground;
            BtnHorizonMenu.BorderBrush = tm.ButtonInactiveBorder;
            BtnHorizonMenu.Foreground = tm.TextPrimary;
        }

        if (HorizonMenuPanel != null)
        {
            HorizonMenuPanel.Background = tm.MapOverlayBackground;
            HorizonMenuPanel.BorderBrush = tm.MapOverlayBorder;
        }

        if (HorizonSunMoonCardBorder != null)
        {
            HorizonSunMoonCardBorder.Background = tm.ButtonInactiveBackground;
            HorizonSunMoonCardBorder.BorderBrush = tm.ButtonInactiveBorder;
        }

        if (HorizonMenuDivider1 != null) HorizonMenuDivider1.Background = tm.MapOverlayBorder;
        if (HorizonMenuDivider2 != null) HorizonMenuDivider2.Background = tm.MapOverlayBorder;
        if (HorizonMenuDivider3 != null) HorizonMenuDivider3.Background = tm.MapOverlayBorder;
        if (HorizonMenuDividerLoc != null) HorizonMenuDividerLoc.Background = tm.MapOverlayBorder;
        if (HorizonSunMoonCardDivider != null) HorizonSunMoonCardDivider.Background = tm.MapOverlayBorder;

        if (TxtHorizonMenuTitle != null) TxtHorizonMenuTitle.Foreground = tm.TextPrimary;
        if (TxtHorizonSectionLocation != null) TxtHorizonSectionLocation.Foreground = tm.TextSecondary;
        if (TxtHorizonSectionFov != null) TxtHorizonSectionFov.Foreground = tm.TextSecondary;
        if (TxtHorizonSectionSunMoon != null) TxtHorizonSectionSunMoon.Foreground = tm.TextSecondary;

        if (BtnHorizonCurrentLocation != null)
        {
            BtnHorizonCurrentLocation.Background = tm.ButtonInactiveBackground;
            BtnHorizonCurrentLocation.BorderBrush = tm.ButtonInactiveBorder;
            BtnHorizonCurrentLocation.Foreground = tm.TextPrimary;
        }

        if (BtnHorizonLocationFromMainMap != null)
        {
            BtnHorizonLocationFromMainMap.Background = tm.ButtonInactiveBackground;
            BtnHorizonLocationFromMainMap.BorderBrush = tm.ButtonInactiveBorder;
            BtnHorizonLocationFromMainMap.Foreground = tm.TextPrimary;
        }

        if (FocalLengthBoxBorder != null)
        {
            FocalLengthBoxBorder.Background = tm.InputBoxBackground;
            FocalLengthBoxBorder.BorderBrush = _isEditingFocalLength ? tm.ButtonActiveBorder : tm.InputBoxBorder;
        }

        if (MenuFocalLengthBorder != null)
        {
            MenuFocalLengthBorder.Background = tm.InputBoxBackground;
            MenuFocalLengthBorder.BorderBrush = tm.InputBoxBorder;
        }

        if (TxtFocalLengthPrefix != null)
        {
            TxtFocalLengthPrefix.Foreground = tm.Accent;
        }

        if (TxtMenuFocalPrefix != null)
        {
            TxtMenuFocalPrefix.Foreground = tm.Accent;
        }

        if (TxtFocalLength != null)
        {
            TxtFocalLength.Foreground = tm.TextPrimary;
        }

        if (TxtMenuFocalLength != null)
        {
            TxtMenuFocalLength.Foreground = tm.TextPrimary;
        }

        if (TxtFocalLengthUnit != null)
        {
            TxtFocalLengthUnit.Foreground = tm.TextSecondary;
        }

        if (TxtMenuFocalUnit != null)
        {
            TxtMenuFocalUnit.Foreground = tm.TextSecondary;
        }

        UpdateFovButtons(ViewModel?.FieldOfView ?? 54.39);
    }

    public void SetCompactMode(bool isCompact)
    {
        if (QuickFovPanel != null)
        {
            QuickFovPanel.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
        }
        if (FocalLengthBoxBorder != null)
        {
            FocalLengthBoxBorder.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
        }
        if (ToolbarDivider != null)
        {
            ToolbarDivider.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
        }
        if (QuickToolbarBorder != null)
        {
            QuickToolbarBorder.Visibility = Visibility.Visible;
        }
        if (HorizonMenuPanel != null)
        {
            if (isCompact)
            {
                HorizonMenuPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
                HorizonMenuPanel.Width = double.NaN;
                HorizonMenuPanel.Margin = new Thickness(10, 48, 10, 10);
                if (ActualHeight > 0)
                {
                    HorizonMenuPanel.MaxHeight = Math.Max(260, ActualHeight - 60);
                }
            }
            else
            {
                HorizonMenuPanel.HorizontalAlignment = HorizontalAlignment.Right;
                HorizonMenuPanel.Width = 440;
                HorizonMenuPanel.Margin = new Thickness(0, 48, 12, 0);
                HorizonMenuPanel.MaxHeight = 520;
            }
        }
    }

    private void Vm_ViewParametersChanged(double? azimuth, double? fov)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (fov.HasValue)
            {
                UpdateFovButtons(fov.Value);
            }
            if (azimuth.HasValue && TxtCompassHeading != null)
            {
                TxtCompassHeading.Text = $"{Math.Round(azimuth.Value)}°";
            }

            object div = Interop.GetDiv(CanvasContainer);
            if (div == null) return;

            try
            {
                Interop.ExecuteJavaScriptVoid(@"
                    if (window.setHorizonParams) {
                        window.setHorizonParams($0, $1);
                    }
                ", azimuth, fov);
            }
            catch
            {
            }
        });
    }

    private void Vm_HorizonUpdated()
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (ViewModel != null)
            {
                UpdateFovButtons(ViewModel.FieldOfView);
                if (TxtCompassHeading != null)
                {
                    TxtCompassHeading.Text = ViewModel.AzimuthDisplay;
                }
            }
            RenderScene();
        });
    }

    private void BtnCompass_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.Azimuth = 0.0;
        }
    }

    private void InitializeCanvas()
    {
        if (_isInitialized) return;

        object div = Interop.GetDiv(CanvasContainer);
        if (div == null)
        {
            Dispatcher.BeginInvoke(InitializeCanvas);
            return;
        }

        try
        {
            Interop.ExecuteJavaScriptVoid(@"
                (function(d) {
                    if (d && d.style) {
                        d.style.touchAction = 'none';
                        d.style.userSelect = 'none';
                        d.style.webkitUserSelect = 'none';
                    }
                })($0);
            ", div);
        }
        catch { }

        _isInitialized = true;
        RenderScene();
    }

    private void RenderScene()
    {
        if (!_isInitialized || ViewModel == null) return;

        object div = Interop.GetDiv(CanvasContainer);
        if (div == null) return;

        string sceneJson = ViewModel.CurrentSceneJson;
        if (string.IsNullOrEmpty(sceneJson)) return;

        Action<double> onPan = (newAzimuth) =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                ViewModel?.SetAzimuthFromView(newAzimuth);
            });
        };

        Action<double> onFov = (newFov) =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (ViewModel != null)
                {
                    ViewModel.SetFovFromView(newFov);
                    UpdateFovButtons(newFov);
                }
            });
        };

        try
        {
            Interop.ExecuteJavaScriptVoid(@"
                (function(container, json, panCb, fovCb) {
                    function tryRender() {
                        if (typeof window.initHorizonCanvas !== 'function') {
                            setTimeout(tryRender, 50);
                            return;
                        }
                        window.initHorizonCanvas(container, json, function(newAz) {
                            if (panCb) panCb(newAz);
                        }, function(newFov) {
                            if (fovCb) fovCb(newFov);
                        });
                    }
                    tryRender();
                })($0, $1, $2, $3);
            ", div, sceneJson, onPan, onFov);
        }
        catch
        {
        }
    }

    private void UpdateFovButtons(double fov)
    {
        var tm = ThemeManager.Current;

        var activeBg = tm.ButtonActiveBackground;
        var activeBorder = tm.ButtonActiveBorder;
        var activeFg = tm.ButtonActiveForeground;

        var inactiveBg = tm.ButtonInactiveBackground;
        var inactiveBorder = tm.ButtonInactiveBorder;
        var inactiveFg = tm.ButtonInactiveForeground;

        void StyleBtn(Button btn, bool active)
        {
            if (btn == null) return;
            btn.Background = active ? activeBg : inactiveBg;
            btn.BorderBrush = active ? activeBorder : inactiveBorder;
            btn.Foreground = active ? activeFg : inactiveFg;
            btn.FontWeight = active ? FontWeights.Bold : FontWeights.SemiBold;
        }

        double fl = MainViewModel.FieldOfViewToFocalLength(fov);

        StyleBtn(BtnFov15, Math.Abs(fl - 15.0) < 2.0);
        StyleBtn(BtnFov35, Math.Abs(fl - 35.0) < 3.0);
        StyleBtn(BtnFov50, Math.Abs(fl - 50.0) < 4.0);
        StyleBtn(BtnFov90, Math.Abs(fl - 90.0) < 6.0);

        StyleBtn(BtnMenuFov15, Math.Abs(fl - 15.0) < 2.0);
        StyleBtn(BtnMenuFov35, Math.Abs(fl - 35.0) < 3.0);
        StyleBtn(BtnMenuFov50, Math.Abs(fl - 50.0) < 4.0);
        StyleBtn(BtnMenuFov90, Math.Abs(fl - 90.0) < 6.0);

        string flFormatted = (Math.Abs(fl - Math.Round(fl)) < 0.05)
            ? Math.Round(fl).ToString()
            : fl.ToString("0.#");

        if (!_isEditingFocalLength && TxtFocalLength != null)
        {
            TxtFocalLength.Text = flFormatted;
        }

        if (!_isEditingFocalLength && TxtMenuFocalLength != null)
        {
            TxtMenuFocalLength.Text = flFormatted;
        }
    }

    private void BtnFov15_Click(object sender, RoutedEventArgs e) => SetFocalLength(15.0);
    private void BtnFov35_Click(object sender, RoutedEventArgs e) => SetFocalLength(35.0);
    private void BtnFov50_Click(object sender, RoutedEventArgs e) => SetFocalLength(50.0);
    private void BtnFov90_Click(object sender, RoutedEventArgs e) => SetFocalLength(90.0);

    private void SetFocalLength(double focalLengthMm)
    {
        if (ViewModel != null)
        {
            double fov = MainViewModel.FocalLengthToFieldOfView(focalLengthMm);
            ViewModel.FieldOfView = fov;
            UpdateFovButtons(fov);
        }
    }

    private void SetFov(double fov)
    {
        if (ViewModel != null)
        {
            ViewModel.FieldOfView = fov;
            UpdateFovButtons(fov);
        }
    }

    private void TxtFocalLength_GotFocus(object sender, RoutedEventArgs e)
    {
        _isEditingFocalLength = true;
        if (FocalLengthBoxBorder != null)
        {
            FocalLengthBoxBorder.BorderBrush = ThemeManager.Current.ButtonActiveBorder;
        }
        TxtFocalLength?.SelectAll();
    }

    private void TxtFocalLength_LostFocus(object sender, RoutedEventArgs e)
    {
        if (FocalLengthBoxBorder != null)
        {
            FocalLengthBoxBorder.BorderBrush = ThemeManager.Current.InputBoxBorder;
        }
        CommitFocalLengthInput();
    }

    private void TxtFocalLength_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            CommitFocalLengthInput();
            e.Handled = true;
        }
    }

    private void CommitFocalLengthInput()
    {
        _isEditingFocalLength = false;
        if (TxtFocalLength == null || ViewModel == null) return;

        string text = TxtFocalLength.Text?.Trim() ?? string.Empty;
        if (double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double fl)
            || double.TryParse(text, out fl))
        {
            if (fl > 0)
            {
                fl = Math.Clamp(fl, 5.0, 600.0);
                double fov = MainViewModel.FocalLengthToFieldOfView(fl);
                ViewModel.FieldOfView = fov;
                UpdateFovButtons(fov);
                return;
            }
        }

        UpdateFovButtons(ViewModel.FieldOfView);
    }

    private void TxtMenuFocalLength_LostFocus(object sender, RoutedEventArgs e)
    {
        CommitMenuFocalLengthInput();
    }

    private void TxtMenuFocalLength_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            CommitMenuFocalLengthInput();
            e.Handled = true;
        }
    }

    private void CommitMenuFocalLengthInput()
    {
        if (TxtMenuFocalLength == null || ViewModel == null) return;

        string text = TxtMenuFocalLength.Text?.Trim() ?? string.Empty;
        if (double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double fl)
            || double.TryParse(text, out fl))
        {
            if (fl > 0)
            {
                fl = Math.Clamp(fl, 5.0, 600.0);
                double fov = MainViewModel.FocalLengthToFieldOfView(fl);
                ViewModel.FieldOfView = fov;
                UpdateFovButtons(fov);
                return;
            }
        }

        UpdateFovButtons(ViewModel.FieldOfView);
    }

    private void BtnHorizonMenu_Click(object sender, RoutedEventArgs e)
    {
        if (HorizonMenuPanel != null)
        {
            HorizonMenuPanel.Visibility = HorizonMenuPanel.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
        }
    }

    private void BtnCloseHorizonMenu_Click(object sender, RoutedEventArgs e)
    {
        if (HorizonMenuPanel != null)
        {
            HorizonMenuPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void BtnMenuRefreshHorizon_Click(object sender, RoutedEventArgs e)
    {
        RenderScene();
        Redraw();
        BtnCloseHorizonMenu_Click(sender, e);
    }

    private void BtnMenuSnapNorth_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.Azimuth = 0.0;
        }
        BtnCloseHorizonMenu_Click(sender, e);
    }

    private void BtnToggleTheme_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.IsDarkTheme = !ViewModel.IsDarkTheme;
        }
    }

    private async void BtnHorizonCurrentLocation_Click(object sender, RoutedEventArgs e)
    {
        BtnCloseHorizonMenu_Click(sender, e);
        if (ViewModel == null) return;
        ViewModel.StatusMessage = "Acquiring current GPS location...";
        var pos = await GeolocationService.GetCurrentPositionAsync(timeoutSeconds: 8);
        if (pos != null)
        {
            ViewModel.SetLocation(pos.Latitude, pos.Longitude, "Current Location", panMap: true);
            ViewModel.StatusMessage = $"Aim Map & Horizon moved to GPS: {pos.Latitude:F4}°, {pos.Longitude:F4}° (±{pos.AccuracyM:F0}m)";
        }
        else
        {
            ViewModel.StatusMessage = "GPS location unavailable or permission denied.";
        }
    }

    private void BtnHorizonLocationFromMainMap_Click(object sender, RoutedEventArgs e)
    {
        BtnCloseHorizonMenu_Click(sender, e);
        if (FullScreenMapControl.Current != null && ViewModel != null)
        {
            var loc = FullScreenMapControl.Current.GetCurrentTargetLocation();
            ViewModel.SetLocation(loc.Latitude, loc.Longitude, $"Main Map ({loc.Source})", panMap: true);
            ViewModel.StatusMessage = $"Aim Map & Horizon updated from Main Map ({loc.Source}): {loc.Latitude:F4}°, {loc.Longitude:F4}°";
        }
    }
}
