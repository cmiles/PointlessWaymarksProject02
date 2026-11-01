using System;
using System.Windows;
using System.Windows.Controls;
using OpenSilver;
using PointedHorizons.Services;
using PointedHorizons.ViewModels;

namespace PointedHorizons.Controls;

public partial class DirectionsBarControl : UserControl
{
    private bool _isInitialized;
    private MainViewModel? ViewModel => DataContext as MainViewModel;

    public DirectionsBarControl()
    {
        InitializeComponent();
        Loaded += DirectionsBarControl_Loaded;
        SizeChanged += DirectionsBarControl_SizeChanged;
        DataContextChanged += DirectionsBarControl_DataContextChanged;

        ThemeManager.Current.ThemeChanged += (s, e) =>
        {
            Dispatcher.BeginInvoke(UpdateParams);
        };

        LeafletScrollIsolationHelper.ProtectControl(this, CanvasContainer);
    }

    private void DirectionsBarControl_Loaded(object sender, RoutedEventArgs e)
    {
        InitializeCanvas();
    }

    private void DirectionsBarControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateParams();
    }

    public void Redraw()
    {
        UpdateParams();
    }

    private void DirectionsBarControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MainViewModel oldVm)
        {
            oldVm.ViewParametersChanged -= Vm_ViewParametersChanged;
            oldVm.PropertyChanged -= Vm_PropertyChanged;
        }

        if (e.NewValue is MainViewModel newVm)
        {
            newVm.ViewParametersChanged += Vm_ViewParametersChanged;
            newVm.PropertyChanged += Vm_PropertyChanged;
            UpdateParams();
        }
    }

    private void Vm_ViewParametersChanged(double? azimuth, double? fov)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (!_isInitialized) return;
            try
            {
                Interop.ExecuteJavaScriptVoid(@"
                    if (window.setDirectionsBarParams) {
                        window.setDirectionsBarParams($0, $1, $2);
                    }
                ", azimuth, fov, ThemeManager.Current.IsDark);
            }
            catch
            {
            }
        });
    }

    private void Vm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsDarkTheme))
        {
            Dispatcher.BeginInvoke(UpdateParams);
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

        _isInitialized = true;

        Action<double> onPan = (newAzimuth) =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                ViewModel?.SetAzimuthFromView(newAzimuth);
            });
        };

        double initAz = ViewModel?.Azimuth ?? 270.0;
        double initFov = ViewModel?.FieldOfView ?? 90.0;
        bool isDark = ThemeManager.Current.IsDark;

        try
        {
            Interop.ExecuteJavaScriptVoid(@"
                (function(container, az, fov, isDark, panCb) {
                    function tryInit() {
                        if (typeof window.initDirectionsBar !== 'function') {
                            setTimeout(tryInit, 50);
                            return;
                        }
                        window.initDirectionsBar(container, az, fov, isDark, function(newAz) {
                            if (panCb) panCb(newAz);
                        });
                    }
                    tryInit();
                })($0, $1, $2, $3, $4);
            ", div, initAz, initFov, isDark, onPan);
        }
        catch
        {
        }
    }

    private void UpdateParams()
    {
        if (!_isInitialized || ViewModel == null) return;
        try
        {
            Interop.ExecuteJavaScriptVoid(@"
                if (window.setDirectionsBarParams) {
                    window.setDirectionsBarParams($0, $1, $2);
                }
            ", ViewModel.Azimuth, ViewModel.FieldOfView, ThemeManager.Current.IsDark);
        }
        catch
        {
        }
    }
}
