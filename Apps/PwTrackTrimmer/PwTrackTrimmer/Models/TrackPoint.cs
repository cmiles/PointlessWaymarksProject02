using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PwTrackTrimmer.Models
{
    public class TrackPoint : INotifyPropertyChanged
    {
        private int _index;
        private double _latitude;
        private double _longitude;
        private double? _elevation;
        private DateTime? _time;
        private double _distanceFromStart;
        private double? _speed;
        private short? _heartRate;
        private short? _cadence;
        private bool _isSelected;
        private bool _isTrimmed;

        public Guid Id { get; set; } = Guid.NewGuid();

        public int Index
        {
            get => _index;
            set => SetProperty(ref _index, value);
        }

        public double Latitude
        {
            get => _latitude;
            set => SetProperty(ref _latitude, value);
        }

        public double Longitude
        {
            get => _longitude;
            set => SetProperty(ref _longitude, value);
        }

        public double? Elevation
        {
            get => _elevation;
            set
            {
                if (SetProperty(ref _elevation, value))
                {
                    OnPropertyChanged(nameof(ElevationFeet));
                    OnPropertyChanged(nameof(ElevationFormatted));
                }
            }
        }

        public DateTime? Time
        {
            get => _time;
            set => SetProperty(ref _time, value);
        }

        public double DistanceFromStart
        {
            get => _distanceFromStart;
            set
            {
                if (SetProperty(ref _distanceFromStart, value))
                {
                    OnPropertyChanged(nameof(DistanceFromStartMiles));
                    OnPropertyChanged(nameof(DistanceKmFormatted));
                    OnPropertyChanged(nameof(DistanceMilesFormatted));
                    OnPropertyChanged(nameof(DistanceFormatted));
                }
            }
        }

        public double? Speed
        {
            get => _speed;
            set => SetProperty(ref _speed, value);
        }

        public short? HeartRate
        {
            get => _heartRate;
            set => SetProperty(ref _heartRate, value);
        }

        public short? Cadence
        {
            get => _cadence;
            set => SetProperty(ref _cadence, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value))
                {
                    OnPropertyChanged(nameof(RowBackground));
                    OnPropertyChanged(nameof(RowBorderBrush));
                }
            }
        }

        public string RowBackground => IsSelected ? "#1E3A8A" : "Transparent";
        public string RowBorderBrush => IsSelected ? "#38BDF8" : "Transparent";

        public bool IsTrimmed
        {
            get => _isTrimmed;
            set => SetProperty(ref _isTrimmed, value);
        }

        public double DistanceFromStartMiles => DistanceFromStart / 1609.344;

        public double? ElevationFeet => Elevation.HasValue ? Elevation.Value * 3.280839895 : (double?)null;

        public string DistanceKmFormatted => $"{DistanceFromStartMiles:F2} mi";

        public string DistanceMilesFormatted => $"{DistanceFromStartMiles:F2} mi";

        public string DistanceFormatted => $"{DistanceFromStartMiles:F2} mi";

        public string ElevationFormatted => ElevationFeet.HasValue ? $"{ElevationFeet.Value:F1} ft" : "--";

        public string TimeFormatted => Time.HasValue ? Time.Value.ToString("HH:mm:ss") : "--";

        public TrackPoint Clone()
        {
            return new TrackPoint
            {
                Id = this.Id,
                Index = this.Index,
                Latitude = this.Latitude,
                Longitude = this.Longitude,
                Elevation = this.Elevation,
                Time = this.Time,
                DistanceFromStart = this.DistanceFromStart,
                Speed = this.Speed,
                HeartRate = this.HeartRate,
                Cadence = this.Cadence,
                IsSelected = this.IsSelected,
                IsTrimmed = this.IsTrimmed
            };
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
