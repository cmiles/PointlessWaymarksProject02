using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using PwTrackTrimmer.Models;

namespace PwTrackTrimmer.ViewModels
{
    public class MovePointAction : ITrackAction
    {
        private readonly TrackPoint _point;
        private readonly double _oldLat;
        private readonly double _oldLon;
        private readonly double _newLat;
        private readonly double _newLon;
        private readonly Action _onChanged;

        public MovePointAction(TrackPoint point, double oldLat, double oldLon, double newLat, double newLon, Action onChanged)
        {
            _point = point;
            _oldLat = oldLat;
            _oldLon = oldLon;
            _newLat = newLat;
            _newLon = newLon;
            _onChanged = onChanged;
        }

        public string Description => $"Move Point #{_point.Index + 1}";

        public void Undo()
        {
            _point.Latitude = _oldLat;
            _point.Longitude = _oldLon;
            _onChanged?.Invoke();
        }

        public void Redo()
        {
            _point.Latitude = _newLat;
            _point.Longitude = _newLon;
            _onChanged?.Invoke();
        }
    }

    public class DeletePointsAction : ITrackAction
    {
        private readonly ObservableCollection<TrackPoint> _collection;
        private readonly List<(int Index, TrackPoint Point)> _deletedItems;
        private readonly Action _onChanged;

        public DeletePointsAction(ObservableCollection<TrackPoint> collection, IEnumerable<TrackPoint> pointsToDelete, Action onChanged)
        {
            _collection = collection;
            _onChanged = onChanged;
            // Record items with their original indices
            _deletedItems = pointsToDelete
                .Select(p => (Index: collection.IndexOf(p), Point: p))
                .Where(x => x.Index >= 0)
                .OrderBy(x => x.Index)
                .ToList();
        }

        public string Description => $"Delete {_deletedItems.Count} point(s)";

        public void Undo()
        {
            // Reinsert in original order
            foreach (var item in _deletedItems)
            {
                if (item.Index <= _collection.Count)
                {
                    _collection.Insert(item.Index, item.Point);
                }
                else
                {
                    _collection.Add(item.Point);
                }
            }
            _onChanged?.Invoke();
        }

        public void Redo()
        {
            // Remove in descending index order
            foreach (var item in _deletedItems.OrderByDescending(x => x.Index))
            {
                _collection.Remove(item.Point);
            }
            _onChanged?.Invoke();
        }
    }

    public class TrimTrackAction : ITrackAction
    {
        private readonly ObservableCollection<TrackPoint> _collection;
        private readonly List<TrackPoint> _removedStart;
        private readonly List<TrackPoint> _removedEnd;
        private readonly Action _onChanged;

        public TrimTrackAction(
            ObservableCollection<TrackPoint> collection,
            List<TrackPoint> removedStart,
            List<TrackPoint> removedEnd,
            Action onChanged)
        {
            _collection = collection;
            _removedStart = removedStart;
            _removedEnd = removedEnd;
            _onChanged = onChanged;
        }

        public string Description => $"Trim Track ({_removedStart.Count + _removedEnd.Count} points removed)";

        public void Undo()
        {
            // Restore start
            for (int i = _removedStart.Count - 1; i >= 0; i--)
            {
                _collection.Insert(0, _removedStart[i]);
            }
            // Restore end
            foreach (var pt in _removedEnd)
            {
                _collection.Add(pt);
            }
            _onChanged?.Invoke();
        }

        public void Redo()
        {
            foreach (var pt in _removedStart)
            {
                _collection.Remove(pt);
            }
            foreach (var pt in _removedEnd)
            {
                _collection.Remove(pt);
            }
            _onChanged?.Invoke();
        }
    }

    public class SimplifyTrackAction : ITrackAction
    {
        private readonly ObservableCollection<TrackPoint> _collection;
        private readonly List<TrackPoint> _originalPoints;
        private readonly List<TrackPoint> _simplifiedPoints;
        private readonly Action _onChanged;

        public SimplifyTrackAction(
            ObservableCollection<TrackPoint> collection,
            List<TrackPoint> originalPoints,
            List<TrackPoint> simplifiedPoints,
            Action onChanged)
        {
            _collection = collection;
            _originalPoints = originalPoints.ToList();
            _simplifiedPoints = simplifiedPoints.ToList();
            _onChanged = onChanged;
        }

        public string Description => $"Simplify track ({_originalPoints.Count} ➔ {_simplifiedPoints.Count} points)";

        public void Undo()
        {
            _collection.Clear();
            foreach (var pt in _originalPoints)
            {
                _collection.Add(pt);
            }
            _onChanged?.Invoke();
        }

        public void Redo()
        {
            _collection.Clear();
            foreach (var pt in _simplifiedPoints)
            {
                _collection.Add(pt);
            }
            _onChanged?.Invoke();
        }
    }

    public class AdjustElevationsAction : ITrackAction
    {
        private readonly List<(TrackPoint Point, double? OldElevation, double? NewElevation)> _changes;
        private readonly Action _onChanged;

        public AdjustElevationsAction(
            List<(TrackPoint Point, double? OldElevation, double? NewElevation)> changes,
            Action onChanged)
        {
            _changes = changes;
            _onChanged = onChanged;
        }

        public string Description => $"Adjust Elevations from DEM ({_changes.Count} points)";

        public void Undo()
        {
            foreach (var item in _changes)
            {
                item.Point.Elevation = item.OldElevation;
            }
            _onChanged?.Invoke();
        }

        public void Redo()
        {
            foreach (var item in _changes)
            {
                item.Point.Elevation = item.NewElevation;
            }
            _onChanged?.Invoke();
        }
    }

    public class ReplaceTrackPointsAction : ITrackAction
    {
        private readonly ObservableCollection<TrackPoint> _collection;
        private readonly List<TrackPoint> _originalPoints;
        private readonly List<TrackPoint> _newPoints;
        private readonly string _description;
        private readonly Action _onChanged;

        public ReplaceTrackPointsAction(
            ObservableCollection<TrackPoint> collection,
            List<TrackPoint> originalPoints,
            List<TrackPoint> newPoints,
            string description,
            Action onChanged)
        {
            _collection = collection;
            _originalPoints = originalPoints.Select(p => p.Clone()).ToList();
            _newPoints = newPoints.Select(p => p.Clone()).ToList();
            _description = description;
            _onChanged = onChanged;
        }

        public string Description => _description;

        public void Undo()
        {
            _collection.Clear();
            foreach (var pt in _originalPoints)
            {
                _collection.Add(pt.Clone());
            }
            _onChanged?.Invoke();
        }

        public void Redo()
        {
            _collection.Clear();
            foreach (var pt in _newPoints)
            {
                _collection.Add(pt.Clone());
            }
            _onChanged?.Invoke();
        }
    }
}
