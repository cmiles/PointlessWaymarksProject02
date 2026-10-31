using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PointedHorizons.Models;
using PointedHorizons.Services;

namespace PointedHorizons.ViewModels;

public record LocationPreset(string Name, double Lat, double Lon, double Bearing, double Fov, string Description);

public class MainViewModel : INotifyPropertyChanged
{
    private double _latitude = 32.10646;      // Rincon Mountains / Tucson East (matches PeakFinder screenshot)
    private double _longitude = -110.60845;
    private double _observerElevationM = 1064.0;
    private double _azimuth = 83.5;          // Looking East across the Rincon ridges
    private double _fieldOfView = 45.0;
    private bool _isMetric = true;
    private bool _isDarkTheme = false;
    private bool _isBusy;
    private string _statusMessage = "Ready. Click on the map or drag the arrow to compute horizon.";
    private string _locationPresetName = "Tucson East (Rincon Ridges), AZ";
    private string _currentSceneJson = string.Empty;
    private HorizonSceneData? _currentSceneData;
    private CancellationTokenSource? _debounceCts;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? HorizonUpdated;
    public event Action<double?, double?>? ViewParametersChanged;
    public event Action<double, double, bool>? MapLocationRequested;
    public event Action<double>? MapDirectionRequested;
    public event Action<double>? MapFovRequested;
    public event Action<double, double, double, double, double>? FarthestVisibleLineCalculated;
    public event Action? FarthestVisibleLineCleared;

    private CancellationTokenSource? _farthestFeatureCts;

    public MainViewModel()
    {
        _isDarkTheme = ThemeManager.Current.IsDark;
        ThemeManager.Current.ThemeChanged += (s, e) =>
        {
            if (_isDarkTheme != ThemeManager.Current.IsDark)
            {
                _isDarkTheme = ThemeManager.Current.IsDark;
                OnPropertyChanged(nameof(IsDarkTheme));
                UpdateSceneJson();
            }
        };
    }

    public List<LocationPreset> Presets { get; } =
    [
        new("Tucson East (Rincon Ridges), AZ", 32.10646, -110.60845, 83.5, 45.0, "East-facing panorama towards the Rincon Mountains and Mica Mountain as seen in PeakFinder"),
        new("Mount Lemmon, AZ", 32.4431, -110.7884, 270.0, 90.0, "Spectacular ridge view over Santa Catalina peaks and Tucson basin"),
        new("Mount Rainier (Paradise), WA", 46.7865, -121.7350, 350.0, 90.0, "Iconic alpine skyline gazing north towards Mount Rainier and glaciers"),
        new("Grand Canyon (Mather Point), AZ", 36.0617, -112.1077, 345.0, 90.0, "Expansive canyon rim panoramas across Bright Angel and North Rim"),
        new("Pikes Peak, CO", 38.8405, -105.0442, 280.0, 90.0, "Front Range high country looking west towards the Sawatch Range"),
        new("Yosemite (Glacier Point), CA", 37.7275, -119.5742, 70.0, 90.0, "Granite domes, Half Dome, and high Sierra peaks"),
        new("Mount Washington, NH", 44.2705, -71.3033, 260.0, 90.0, "Highest peak in the Northeast; panoramic vistas across the Presidential Range, New Hampshire")
    ];

    public double Latitude
    {
        get => _latitude;
        set
        {
            if (Math.Abs(_latitude - value) > 1e-6)
            {
                _latitude = Math.Clamp(value, 24.0, 50.5);
                OnPropertyChanged();
                OnPropertyChanged(nameof(CoordinatesDisplay));
                ScheduleHorizonGeneration();
                ScheduleFarthestVisibleFeatureCalculation();
            }
        }
    }

    public double Longitude
    {
        get => _longitude;
        set
        {
            if (Math.Abs(_longitude - value) > 1e-6)
            {
                _longitude = Math.Clamp(value, -126.0, -66.0);
                OnPropertyChanged();
                OnPropertyChanged(nameof(CoordinatesDisplay));
                ScheduleHorizonGeneration();
                ScheduleFarthestVisibleFeatureCalculation();
            }
        }
    }

    public double ObserverElevationM
    {
        get => _observerElevationM;
        set
        {
            if (Math.Abs(_observerElevationM - value) > 0.1)
            {
                _observerElevationM = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ObserverElevationDisplay));
            }
        }
    }

    public string ObserverElevationDisplay => IsMetric
        ? $"{Math.Round(ObserverElevationM):N0} m"
        : $"{Math.Round(ObserverElevationM * 3.28084):N0} ft";

    public string CoordinatesDisplay =>
        $"{Math.Abs(Latitude):F4}° {(Latitude >= 0 ? "N" : "S")}, {Math.Abs(Longitude):F4}° {(Longitude >= 0 ? "E" : "W")}";

    public double Azimuth
    {
        get => _azimuth;
        set
        {
            double normalized = (value % 360 + 360) % 360;
            if (Math.Abs(_azimuth - normalized) > 0.1)
            {
                _azimuth = Math.Round(normalized, 1);
                OnPropertyChanged();
                OnPropertyChanged(nameof(AzimuthDisplay));
                MapDirectionRequested?.Invoke(_azimuth);
                ViewParametersChanged?.Invoke(_azimuth, null);
                ScheduleFarthestVisibleFeatureCalculation();
            }
        }
    }

    public string AzimuthDisplay => $"{Math.Round(Azimuth)}° {GetCardinal(Azimuth)}";

    public double FieldOfView
    {
        get => _fieldOfView;
        set
        {
            double clamped = Math.Clamp(value, 30.0, 160.0);
            if (Math.Abs(_fieldOfView - clamped) > 0.1)
            {
                _fieldOfView = clamped;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FieldOfViewDisplay));
                MapFovRequested?.Invoke(_fieldOfView);
                ViewParametersChanged?.Invoke(null, _fieldOfView);
            }
        }
    }

    public string FieldOfViewDisplay => $"{Math.Round(FieldOfView)}°";

    public bool IsMetric
    {
        get => _isMetric;
        set
        {
            if (_isMetric != value)
            {
                _isMetric = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ObserverElevationDisplay));
                UpdateSceneJson();
            }
        }
    }

    public bool IsDarkTheme
    {
        get => ThemeManager.Current.IsDark;
        set
        {
            if (ThemeManager.Current.IsDark != value)
            {
                _isDarkTheme = value;
                ThemeManager.Current.Mode = value ? AppThemeMode.Dark : AppThemeMode.Light;
                OnPropertyChanged();
                UpdateSceneJson();
            }
        }
    }

    private static readonly System.Windows.Media.SolidColorBrush BrushSky = new(System.Windows.Media.Color.FromArgb(255, 56, 189, 248));
    private static readonly System.Windows.Media.SolidColorBrush BrushAmber = new(System.Windows.Media.Color.FromArgb(255, 245, 158, 11));

    public System.Windows.Media.SolidColorBrush StatusIndicatorBrush => IsBusy ? BrushAmber : BrushSky;

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (_isBusy != value)
            {
                _isBusy = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusIndicatorBrush));
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (_statusMessage != value)
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }
    }

    public string LocationPresetName
    {
        get => _locationPresetName;
        set
        {
            if (_locationPresetName != value)
            {
                _locationPresetName = value;
                OnPropertyChanged();
            }
        }
    }

    public HorizonSceneData? CurrentSceneData
    {
        get => _currentSceneData;
        private set
        {
            _currentSceneData = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PeaksCountDisplay));
            OnPropertyChanged(nameof(TilesSummary));
        }
    }

    public string CurrentSceneJson
    {
        get => _currentSceneJson;
        private set
        {
            _currentSceneJson = value;
            OnPropertyChanged();
            HorizonUpdated?.Invoke();
        }
    }

    public string PeaksCountDisplay => CurrentSceneData != null
        ? $"{CurrentSceneData.Peaks.Count} peaks identified"
        : "No peaks";

    public string TilesSummary => CurrentSceneData != null && CurrentSceneData.TileKeysUsed.Count > 0
        ? $"DEM Tiles: {string.Join(", ", CurrentSceneData.TileKeysUsed)} ({CurrentSceneData.CalculationTimeMs:F0} ms)"
        : string.Empty;

    public void UpdateLocationFromMap(double lat, double lon)
    {
        _locationPresetName = "Custom Location";
        OnPropertyChanged(nameof(LocationPresetName));
        _latitude = Math.Clamp(lat, 24.0, 50.5);
        _longitude = Math.Clamp(lon, -126.0, -66.0);
        OnPropertyChanged(nameof(Latitude));
        OnPropertyChanged(nameof(Longitude));
        OnPropertyChanged(nameof(CoordinatesDisplay));
        ScheduleHorizonGeneration();
        ScheduleFarthestVisibleFeatureCalculation();
    }

    public void UpdateDirectionFromMap(double bearing)
    {
        double normalized = (bearing % 360 + 360) % 360;
        if (Math.Abs(_azimuth - normalized) > 0.1)
        {
            _azimuth = Math.Round(normalized, 1);
            OnPropertyChanged(nameof(Azimuth));
            OnPropertyChanged(nameof(AzimuthDisplay));
            ViewParametersChanged?.Invoke(_azimuth, null);
            ScheduleFarthestVisibleFeatureCalculation();
        }
    }

    public void SetAzimuthFromView(double bearing)
    {
        double normalized = (bearing % 360 + 360) % 360;
        if (Math.Abs(_azimuth - normalized) > 0.1)
        {
            _azimuth = Math.Round(normalized, 1);
            OnPropertyChanged(nameof(Azimuth));
            OnPropertyChanged(nameof(AzimuthDisplay));
            MapDirectionRequested?.Invoke(_azimuth);
            ScheduleFarthestVisibleFeatureCalculation();
        }
    }

    public void SetFovFromView(double fov)
    {
        double clamped = Math.Clamp(fov, 30.0, 160.0);
        if (Math.Abs(_fieldOfView - clamped) > 0.1)
        {
            _fieldOfView = clamped;
            OnPropertyChanged(nameof(FieldOfView));
            OnPropertyChanged(nameof(FieldOfViewDisplay));
            MapFovRequested?.Invoke(_fieldOfView);
        }
    }

    public void SetQuickAzimuth(double bearing)
    {
        Azimuth = bearing;
    }

    public void ApplyPreset(LocationPreset preset)
    {
        _locationPresetName = preset.Name;
        OnPropertyChanged(nameof(LocationPresetName));

        _latitude = preset.Lat;
        _longitude = preset.Lon;
        _azimuth = preset.Bearing;
        _fieldOfView = preset.Fov;

        OnPropertyChanged(nameof(Latitude));
        OnPropertyChanged(nameof(Longitude));
        OnPropertyChanged(nameof(CoordinatesDisplay));
        OnPropertyChanged(nameof(Azimuth));
        OnPropertyChanged(nameof(AzimuthDisplay));
        OnPropertyChanged(nameof(FieldOfView));
        OnPropertyChanged(nameof(FieldOfViewDisplay));

        MapLocationRequested?.Invoke(_latitude, _longitude, true);
        MapDirectionRequested?.Invoke(_azimuth);
        MapFovRequested?.Invoke(_fieldOfView);

        ScheduleHorizonGeneration(immediate: true);
        ScheduleFarthestVisibleFeatureCalculation();
    }

    public void ScheduleFarthestVisibleFeatureCalculation()
    {
        _farthestFeatureCts?.Cancel();
        _farthestFeatureCts = new CancellationTokenSource();
        var token = _farthestFeatureCts.Token;

        // Immediately notify UI and map to remove the solid line
        FarthestVisibleLineCleared?.Invoke();

        Task.Run(async () =>
        {
            try
            {
                // Wait for direction to settle
                await Task.Delay(350, token);
                if (token.IsCancellationRequested) return;

                double lat = Latitude;
                double lon = Longitude;
                double az = Azimuth;
                double eyeElev = ObserverElevationM > 0 ? ObserverElevationM + 2.0 : 1000.0;
                const double maxSightKm = 300.0;

                // 1. Get tile keys along the ray from 0 to 300 km
                var neededKeys = DemService.GetTileKeysAlongRay(lat, lon, az, maxSightKm);

                // 2. Preload needed DEM tiles
                foreach (var key in neededKeys)
                {
                    if (token.IsCancellationRequested) return;
                    if (!DemService.IsTileLoaded(key))
                    {
                        await DemService.GetOrLoadTileAsync(key);
                    }
                }

                if (token.IsCancellationRequested) return;

                // Ensure observer elevation is accurate if DEM tile is loaded
                double? groundElev = DemService.GetElevation(lat, lon);
                if (groundElev.HasValue && groundElev.Value > 0)
                {
                    eyeElev = groundElev.Value + 2.0;
                }

                // 3. Find the farthest visible land feature along the direction of view line
                var result = HorizonRayTracer.FindFarthestVisibleLandFeature(lat, lon, eyeElev, az, maxSightKm);

                if (token.IsCancellationRequested) return;

                RunOnUIThread(() =>
                {
                    if (!token.IsCancellationRequested && result.HasValue)
                    {
                        FarthestVisibleLineCalculated?.Invoke(
                            result.Value.Lat,
                            result.Value.Lon,
                            result.Value.DistanceKm,
                            result.Value.ElevationM,
                            az);
                    }
                });
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ScheduleFarthestVisibleFeatureCalculation error: " + ex.Message);
            }
        }, token);
    }

    public void ScheduleHorizonGeneration(bool immediate = false)
    {
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        int delayMs = immediate ? 20 : 250;

        Task.Run(async () =>
        {
            try
            {
                if (delayMs > 0)
                {
                    await Task.Delay(delayMs, token);
                }

                if (token.IsCancellationRequested) return;

                await GenerateHorizonAsync(token);
            }
            catch (TaskCanceledException)
            {
            }
        }, token);
    }

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private static void RunOnUIThread(Action action)
    {
        var dispatcher = System.Windows.Deployment.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(action);
        }
        else
        {
            action();
        }
    }

    public async Task GenerateHorizonAsync(CancellationToken token = default)
    {
        RunOnUIThread(() =>
        {
            IsBusy = true;
            StatusMessage = "Checking DEM coverage...";
        });

        try
        {
            double lat = Latitude;
            double lon = Longitude;
            double az = Azimuth;
            double fov = FieldOfView;
            const double rangeKm = 70.0;

            var progress = new Progress<string>(msg =>
            {
                RunOnUIThread(() => StatusMessage = msg);
            });

            // Preload all DEM tiles required for the scene
            var loadedTiles = await DemService.PreloadTilesForRadiusAsync(lat, lon, rangeKm, progress);

            if (token.IsCancellationRequested) return;

            // Query observer elevation
            double? groundElev = DemService.GetElevation(lat, lon);
            if (groundElev.HasValue)
            {
                RunOnUIThread(() =>
                {
                    ObserverElevationM = Math.Round(groundElev.Value, 1);
                });
                ScheduleFarthestVisibleFeatureCalculation();
            }

            RunOnUIThread(() => StatusMessage = "Ray tracing complete 360° horizon panorama and peaks...");

            // Ray trace the complete 360° horizon panorama (once per location)
            var scene = await Task.Run(() =>
                HorizonRayTracer.TraceScene(lat, lon, ObserverElevationM, az, fov, rangeKm, rayCount: 1800), token);

            if (token.IsCancellationRequested) return;

            scene.ObserverName = LocationPresetName;

            RunOnUIThread(() =>
            {
                CurrentSceneData = scene;
                UpdateSceneJson();
                StatusMessage = $"360° panorama calculated in {scene.CalculationTimeMs:F0} ms. {scene.Peaks.Count} peaks detected around horizon.";
            });
        }
        catch (Exception ex) when (ex is not TaskCanceledException)
        {
            RunOnUIThread(() =>
            {
                StatusMessage = $"Error: {ex.Message}";
            });
        }
        finally
        {
            RunOnUIThread(() =>
            {
                IsBusy = false;
            });
        }
    }

    private void UpdateSceneJson()
    {
        if (CurrentSceneData == null) return;

        var payload = new
        {
            observerLat = CurrentSceneData.ObserverLat,
            observerLon = CurrentSceneData.ObserverLon,
            observerElevationM = CurrentSceneData.ObserverElevationM,
            observerElevationFt = CurrentSceneData.ObserverElevationFt,
            observerName = CurrentSceneData.ObserverName,
            azimuthCenter = CurrentSceneData.AzimuthCenter,
            fieldOfView = CurrentSceneData.FieldOfView,
            minAltitude = CurrentSceneData.MinAltitude,
            maxAltitude = CurrentSceneData.MaxAltitude,
            isDark = IsDarkTheme,
            isMetric = IsMetric,
            calculationTimeMs = CurrentSceneData.CalculationTimeMs,
            profilePoints = CurrentSceneData.ProfilePoints,
            peaks = CurrentSceneData.Peaks,
            ridgeLayers = CurrentSceneData.RidgeLayers
        };

        CurrentSceneJson = JsonSerializer.Serialize(payload, _jsonOptions);
    }

    private static string GetCardinal(double deg)
    {
        string[] cardinals = ["N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW"];
        int idx = (int)Math.Round(deg / 22.5) % 16;
        return cardinals[idx];
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
