using System;
using System.Windows;
using System.Windows.Controls;
using OpenSilver;
using PointedHorizons.Services;
using PointedHorizons.ViewModels;

namespace PointedHorizons.Controls;

public partial class LeafletMapControl : UserControl
{
    private bool _isMapInitialized;
    private bool _isMapInitializing;
    private bool _isUpdatingFromInternal;

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    public LeafletMapControl()
    {
        InitializeComponent();
        Loaded += LeafletMapControl_Loaded;
        SizeChanged += LeafletMapControl_SizeChanged;
        DataContextChanged += LeafletMapControl_DataContextChanged;

        LeafletScrollIsolationHelper.ProtectControl(this, MapContainer);
    }

    private void LeafletMapControl_Loaded(object sender, RoutedEventArgs e)
    {
        InitializeMap();
    }

    private void LeafletMapControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isMapInitialized)
        {
            try
            {
                Interop.ExecuteJavaScriptVoid("if (window.pointedMap && window.pointedMap.invalidateSize) { window.pointedMap.invalidateSize(); }");
            }
            catch
            {
            }
        }
        else
        {
            InitializeMap();
        }
    }

    private void LeafletMapControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MainViewModel oldVm)
        {
            oldVm.MapLocationRequested -= Vm_MapLocationRequested;
            oldVm.MapDirectionRequested -= Vm_MapDirectionRequested;
            oldVm.MapFovRequested -= Vm_MapFovRequested;
        }

        if (e.NewValue is MainViewModel newVm)
        {
            newVm.MapLocationRequested += Vm_MapLocationRequested;
            newVm.MapDirectionRequested += Vm_MapDirectionRequested;
            newVm.MapFovRequested += Vm_MapFovRequested;

            if (_isMapInitialized)
            {
                SyncMapFromViewModel(panTo: true);
            }
        }
    }

    private void Vm_MapLocationRequested(double lat, double lon, bool panTo)
    {
        if (!_isMapInitialized) return;
        try
        {
            Interop.ExecuteJavaScriptVoid("if (window.pointedMap && window.pointedMap.setLocation) { window.pointedMap.setLocation($0, $1, $2); }", lat, lon, panTo);
        }
        catch
        {
        }
    }

    private void Vm_MapDirectionRequested(double bearing)
    {
        if (!_isMapInitialized) return;
        try
        {
            Interop.ExecuteJavaScriptVoid("if (window.pointedMap && window.pointedMap.setDirection) { window.pointedMap.setDirection($0); }", bearing);
        }
        catch
        {
        }
    }

    private void Vm_MapFovRequested(double fov)
    {
        if (!_isMapInitialized) return;
        try
        {
            Interop.ExecuteJavaScriptVoid("if (window.pointedMap && window.pointedMap.setFieldOfView) { window.pointedMap.setFieldOfView($0); }", fov);
        }
        catch
        {
        }
    }

    private void InitializeMap()
    {
        if (_isMapInitialized || _isMapInitializing) return;

        object div = Interop.GetDiv(MapContainer);
        if (div == null)
        {
            Dispatcher.BeginInvoke(InitializeMap);
            return;
        }

        _isMapInitializing = true;

        Action<double, double> onLocationPicked = (lat, lon) =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (ViewModel != null)
                {
                    _isUpdatingFromInternal = true;
                    try
                    {
                        ViewModel.UpdateLocationFromMap(lat, lon);
                    }
                    finally
                    {
                        _isUpdatingFromInternal = false;
                    }
                }
            });
        };

        Action<double> onDirectionChanged = (bearing) =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (ViewModel != null)
                {
                    _isUpdatingFromInternal = true;
                    try
                    {
                        ViewModel.UpdateDirectionFromMap(bearing);
                    }
                    finally
                    {
                        _isUpdatingFromInternal = false;
                    }
                }
            });
        };

        double initLat = ViewModel?.Latitude ?? 32.4431;
        double initLon = ViewModel?.Longitude ?? -110.7884;
        double initBearing = ViewModel?.Azimuth ?? 270.0;
        double initFov = ViewModel?.FieldOfView ?? 90.0;

        try
        {
            Interop.ExecuteJavaScriptVoid(@"
                (function(container, lat, lon, bearing, fov, locCb, dirCb) {
                    function tryInit() {
                        if (typeof L === 'undefined' || !window.pointedMap || !window.pointedMap.init) {
                            setTimeout(tryInit, 50);
                            return;
                        }
                        window.pointedMap.init(container, {
                            lat: lat,
                            lon: lon,
                            bearing: bearing,
                            fov: fov
                        }, function(newLat, newLon) {
                            if (locCb) locCb(newLat, newLon);
                        }, function(newBearing) {
                            if (dirCb) dirCb(newBearing);
                        });
                    }
                    tryInit();
                })($0, $1, $2, $3, $4, $5, $6);
            ", div, initLat, initLon, initBearing, initFov, onLocationPicked, onDirectionChanged);

            _isMapInitialized = true;
        }
        catch
        {
        }
        finally
        {
            _isMapInitializing = false;
        }
    }

    private void SyncMapFromViewModel(bool panTo = false)
    {
        if (ViewModel == null || !_isMapInitialized) return;

        try
        {
            Interop.ExecuteJavaScriptVoid(@"
                if (window.pointedMap) {
                    if (window.pointedMap.setLocation) window.pointedMap.setLocation($0, $1, $2);
                    if (window.pointedMap.setDirection) window.pointedMap.setDirection($3);
                    if (window.pointedMap.setFieldOfView) window.pointedMap.setFieldOfView($4);
                }
            ", ViewModel.Latitude, ViewModel.Longitude, panTo, ViewModel.Azimuth, ViewModel.FieldOfView);
        }
        catch
        {
        }
    }

    private void SliderAzimuth_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingFromInternal) return;
        if (_isMapInitialized)
        {
            Vm_MapDirectionRequested(e.NewValue);
        }
    }

    private void BtnCardN_Click(object sender, RoutedEventArgs e) => ViewModel?.SetQuickAzimuth(0);
    private void BtnCardNE_Click(object sender, RoutedEventArgs e) => ViewModel?.SetQuickAzimuth(45);
    private void BtnCardE_Click(object sender, RoutedEventArgs e) => ViewModel?.SetQuickAzimuth(90);
    private void BtnCardSE_Click(object sender, RoutedEventArgs e) => ViewModel?.SetQuickAzimuth(135);
    private void BtnCardS_Click(object sender, RoutedEventArgs e) => ViewModel?.SetQuickAzimuth(180);
    private void BtnCardSW_Click(object sender, RoutedEventArgs e) => ViewModel?.SetQuickAzimuth(225);
    private void BtnCardW_Click(object sender, RoutedEventArgs e) => ViewModel?.SetQuickAzimuth(270);
    private void BtnCardNW_Click(object sender, RoutedEventArgs e) => ViewModel?.SetQuickAzimuth(315);
}
