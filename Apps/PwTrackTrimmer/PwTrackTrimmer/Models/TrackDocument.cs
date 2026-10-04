using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PwTrackTrimmer.Models
{
    public class TrackDocument : INotifyPropertyChanged
    {
        private string _name = "Untitled Track";
        private string _description = string.Empty;
        private string _sourceFilePath = string.Empty;
        private string _fileFormat = "GPX";
        private ObservableCollection<TrackPoint> _points = new ObservableCollection<TrackPoint>();
        private TrackStatistics _statistics = new TrackStatistics();

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        public string SourceFilePath
        {
            get => _sourceFilePath;
            set => SetProperty(ref _sourceFilePath, value);
        }

        public string FileFormat
        {
            get => _fileFormat;
            set => SetProperty(ref _fileFormat, value);
        }

        public ObservableCollection<TrackPoint> Points
        {
            get => _points;
            set => SetProperty(ref _points, value);
        }

        public TrackStatistics Statistics
        {
            get => _statistics;
            set => SetProperty(ref _statistics, value);
        }

        public void SetPoints(IList<TrackPoint> newPoints)
        {
            Points = new ObservableCollection<TrackPoint>(newPoints ?? new List<TrackPoint>());
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
