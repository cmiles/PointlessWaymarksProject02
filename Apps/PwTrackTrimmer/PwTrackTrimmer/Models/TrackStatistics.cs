using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PwTrackTrimmer.Models
{
    public class TrackStatistics : INotifyPropertyChanged
    {
        private double _totalDistanceMeters;
        private double _totalElevationGainMeters;
        private double _totalElevationLossMeters;
        private double _minElevationMeters;
        private double _maxElevationMeters;
        private TimeSpan _totalDuration;
        private TimeSpan _movingDuration;
        private double _averageSpeedKmh;
        private double _maxSpeedKmh;
        private int _pointCount;

        public double TotalDistanceMeters
        {
            get => _totalDistanceMeters;
            set
            {
                if (SetProperty(ref _totalDistanceMeters, value))
                {
                    OnPropertyChanged(nameof(TotalDistanceKm));
                    OnPropertyChanged(nameof(TotalDistanceMiles));
                    OnPropertyChanged(nameof(DistanceFormatted));
                }
            }
        }

        public double TotalDistanceKm => TotalDistanceMeters / 1000.0;
        public double TotalDistanceMiles => TotalDistanceMeters / 1609.344;

        public double TotalElevationGainMeters
        {
            get => _totalElevationGainMeters;
            set
            {
                if (SetProperty(ref _totalElevationGainMeters, value))
                {
                    OnPropertyChanged(nameof(TotalElevationGainFeet));
                    OnPropertyChanged(nameof(ElevationGainFormatted));
                }
            }
        }

        public double TotalElevationGainFeet => TotalElevationGainMeters * 3.280839895;

        public double TotalElevationLossMeters
        {
            get => _totalElevationLossMeters;
            set
            {
                if (SetProperty(ref _totalElevationLossMeters, value))
                {
                    OnPropertyChanged(nameof(TotalElevationLossFeet));
                    OnPropertyChanged(nameof(ElevationLossFormatted));
                }
            }
        }

        public double TotalElevationLossFeet => TotalElevationLossMeters * 3.280839895;

        public double MinElevationMeters
        {
            get => _minElevationMeters;
            set
            {
                if (SetProperty(ref _minElevationMeters, value))
                {
                    OnPropertyChanged(nameof(MinElevationFeet));
                    OnPropertyChanged(nameof(MinElevationFormatted));
                }
            }
        }

        public double MinElevationFeet => MinElevationMeters * 3.280839895;

        public double MaxElevationMeters
        {
            get => _maxElevationMeters;
            set
            {
                if (SetProperty(ref _maxElevationMeters, value))
                {
                    OnPropertyChanged(nameof(MaxElevationFeet));
                    OnPropertyChanged(nameof(MaxElevationFormatted));
                }
            }
        }

        public double MaxElevationFeet => MaxElevationMeters * 3.280839895;

        public TimeSpan TotalDuration
        {
            get => _totalDuration;
            set
            {
                if (SetProperty(ref _totalDuration, value))
                {
                    OnPropertyChanged(nameof(DurationFormatted));
                    OnPropertyChanged(nameof(DurationCardTitle));
                    OnPropertyChanged(nameof(DurationCardValue));
                    OnPropertyChanged(nameof(DurationCardSubtitle));
                    OnPropertyChanged(nameof(OverallSpeedKmh));
                    OnPropertyChanged(nameof(OverallSpeedMph));
                    OnPropertyChanged(nameof(OverallSpeedFormatted));
                    OnPropertyChanged(nameof(SpeedToolTipText));
                }
            }
        }

        public TimeSpan MovingDuration
        {
            get => _movingDuration;
            set
            {
                if (SetProperty(ref _movingDuration, value))
                {
                    OnPropertyChanged(nameof(MovingDurationFormatted));
                    OnPropertyChanged(nameof(DurationCardTitle));
                    OnPropertyChanged(nameof(DurationCardValue));
                    OnPropertyChanged(nameof(DurationCardSubtitle));
                    OnPropertyChanged(nameof(SpeedToolTipText));
                }
            }
        }

        public double AverageSpeedKmh
        {
            get => _averageSpeedKmh;
            set
            {
                if (SetProperty(ref _averageSpeedKmh, value))
                {
                    OnPropertyChanged(nameof(AverageSpeedMph));
                    OnPropertyChanged(nameof(AverageSpeedFormatted));
                    OnPropertyChanged(nameof(AveragePaceFormatted));
                    OnPropertyChanged(nameof(SpeedToolTipText));
                }
            }
        }

        public double AverageSpeedMph => AverageSpeedKmh / 1.609344;

        public double OverallSpeedKmh => TotalDuration.TotalHours > 0 ? TotalDistanceKm / TotalDuration.TotalHours : 0;

        public double OverallSpeedMph => OverallSpeedKmh / 1.609344;

        public string OverallSpeedFormatted => OverallSpeedMph > 0.05 ? $"{OverallSpeedMph:F1} mph" : "--";

        public string OverallPaceFormatted
        {
            get
            {
                if (OverallSpeedMph <= 0.1) return "--:-- /mi";
                double minutesPerMile = 60.0 / OverallSpeedMph;
                int mins = (int)minutesPerMile;
                int secs = (int)((minutesPerMile - mins) * 60.0);
                return $"{mins}:{secs:D2} /mi";
            }
        }

        public bool HasSignificantStoppedTime => (TotalDuration - MovingDuration).TotalMinutes >= 2.0 && MovingDuration > TimeSpan.Zero;

        public string DurationCardTitle => HasSignificantStoppedTime ? "MOVING TIME" : "DURATION";

        public string DurationCardValue => HasSignificantStoppedTime ? MovingDurationFormatted : DurationFormatted;

        public string DurationCardSubtitle => HasSignificantStoppedTime ? $"Elapsed {DurationFormatted}" : "";

        public string SpeedToolTipText
        {
            get
            {
                if (HasSignificantStoppedTime)
                {
                    return $"Moving Speed: {AverageSpeedFormatted} ({AveragePaceFormatted})\nOverall Speed: {OverallSpeedFormatted} ({OverallPaceFormatted})";
                }
                return $"Average Speed: {AverageSpeedFormatted} ({AveragePaceFormatted})";
            }
        }

        public double MaxSpeedKmh
        {
            get => _maxSpeedKmh;
            set
            {
                if (SetProperty(ref _maxSpeedKmh, value))
                {
                    OnPropertyChanged(nameof(MaxSpeedMph));
                    OnPropertyChanged(nameof(MaxSpeedFormatted));
                }
            }
        }

        public double MaxSpeedMph => MaxSpeedKmh / 1.609344;

        public int PointCount
        {
            get => _pointCount;
            set => SetProperty(ref _pointCount, value);
        }

        public string DistanceFormatted => $"{TotalDistanceMiles:F2} mi";
        public string ElevationGainFormatted => $"+{TotalElevationGainFeet:F0} ft";
        public string ElevationLossFormatted => $"-{TotalElevationLossFeet:F0} ft";
        public string MinElevationFormatted => $"{MinElevationFeet:F0} ft";
        public string MaxElevationFormatted => $"{MaxElevationFeet:F0} ft";
        public string DurationFormatted => FormatTimeSpan(TotalDuration);
        public string MovingDurationFormatted => FormatTimeSpan(MovingDuration);
        public string AverageSpeedFormatted => $"{AverageSpeedMph:F1} mph";
        public string MaxSpeedFormatted => $"{MaxSpeedMph:F1} mph";

        public string AveragePaceFormatted
        {
            get
            {
                if (AverageSpeedMph <= 0.1) return "--:-- /mi";
                double minutesPerMile = 60.0 / AverageSpeedMph;
                int mins = (int)minutesPerMile;
                int secs = (int)((minutesPerMile - mins) * 60.0);
                return $"{mins}:{secs:D2} /mi";
            }
        }

        private static string FormatTimeSpan(TimeSpan ts)
        {
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours}h {ts.Minutes:D2}m {ts.Seconds:D2}s";
            return $"{ts.Minutes}m {ts.Seconds:D2}s";
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
