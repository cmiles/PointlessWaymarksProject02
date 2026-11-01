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

        if (ToolbarDivider != null)
        {
            ToolbarDivider.Background = tm.MapOverlayBorder;
        }

        if (BtnToggleTheme != null)
        {
            BtnToggleTheme.Background = tm.ButtonInactiveBackground;
            BtnToggleTheme.BorderBrush = tm.ButtonInactiveBorder;
            BtnToggleTheme.Foreground = tm.TextPrimary;
        }

        if (FocalLengthBoxBorder != null)
        {
            FocalLengthBoxBorder.Background = tm.ButtonInactiveBackground;
            FocalLengthBoxBorder.BorderBrush = tm.ButtonInactiveBorder;
        }

        if (TxtFocalLength != null)
        {
            TxtFocalLength.Foreground = tm.TextPrimary;
        }

        if (TxtFocalLengthUnit != null)
        {
            TxtFocalLengthUnit.Foreground = tm.TextSecondary;
        }

        UpdateFovButtons(ViewModel?.FieldOfView ?? 54.39);
    }

    private void Vm_ViewParametersChanged(double? azimuth, double? fov)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (fov.HasValue)
            {
                UpdateFovButtons(fov.Value);
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
            }
            RenderScene();
        });
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

        if (!_isEditingFocalLength && TxtFocalLength != null)
        {
            TxtFocalLength.Text = (Math.Abs(fl - Math.Round(fl)) < 0.05)
                ? Math.Round(fl).ToString()
                : fl.ToString("0.#");
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
        TxtFocalLength?.SelectAll();
    }

    private void TxtFocalLength_LostFocus(object sender, RoutedEventArgs e)
    {
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

    private void BtnToggleTheme_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.IsDarkTheme = !ViewModel.IsDarkTheme;
        }
    }
}
