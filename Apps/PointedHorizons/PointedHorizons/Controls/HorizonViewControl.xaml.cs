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
    private MainViewModel? ViewModel => DataContext as MainViewModel;

    public HorizonViewControl()
    {
        InitializeComponent();
        Loaded += HorizonViewControl_Loaded;
        SizeChanged += HorizonViewControl_SizeChanged;
        DataContextChanged += HorizonViewControl_DataContextChanged;

        LeafletScrollIsolationHelper.ProtectControl(this, CanvasContainer);
    }

    private void HorizonViewControl_Loaded(object sender, RoutedEventArgs e)
    {
        InitializeCanvas();
    }

    private void HorizonViewControl_SizeChanged(object sender, SizeChangedEventArgs e)
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
        var activeBg = new SolidColorBrush(Color.FromArgb(255, 2, 132, 199)); // #0284C7
        var inactiveBg = new SolidColorBrush(Color.FromArgb(255, 241, 245, 249)); // #F1F5F9
        var activeFg = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
        var inactiveFg = new SolidColorBrush(Color.FromArgb(255, 51, 65, 85));

        void StyleBtn(Button btn, bool active)
        {
            btn.Background = active ? activeBg : inactiveBg;
            btn.Foreground = active ? activeFg : inactiveFg;
        }

        StyleBtn(BtnFov45, Math.Abs(fov - 45) < 1);
        StyleBtn(BtnFov60, Math.Abs(fov - 60) < 1);
        StyleBtn(BtnFov90, Math.Abs(fov - 90) < 1);
        StyleBtn(BtnFov120, Math.Abs(fov - 120) < 1);
    }

    private void BtnFov45_Click(object sender, RoutedEventArgs e) => SetFov(45);
    private void BtnFov60_Click(object sender, RoutedEventArgs e) => SetFov(60);
    private void BtnFov90_Click(object sender, RoutedEventArgs e) => SetFov(90);
    private void BtnFov120_Click(object sender, RoutedEventArgs e) => SetFov(120);

    private void SetFov(double fov)
    {
        if (ViewModel != null)
        {
            ViewModel.FieldOfView = fov;
            UpdateFovButtons(fov);
        }
    }

    private void BtnToggleUnits_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.IsMetric = !ViewModel.IsMetric;
        }
    }

    private void BtnToggleTheme_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.IsDarkTheme = !ViewModel.IsDarkTheme;
        }
    }
}
