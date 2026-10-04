using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using PwTrackTrimmer.Models;
using PwTrackTrimmer.ViewModels;

namespace PwTrackTrimmer.Controls
{
    public partial class ElevationChartControl : UserControl
    {
        private bool _isDraggingStart;
        private bool _isDraggingEnd;
        private Point _dragStartPoint;
        private double _chartWidth;
        private double _chartHeight;
        private double _minEle;
        private double _maxEle;
        private DateTime _lastClickTime = DateTime.MinValue;
        private Point _lastClickPos;

        private const int MaxVisualPoints = 500; // Downsample limit for ultra-fast 60 FPS XAML rendering

        private TrackTrimmerViewModel ViewModel => DataContext as TrackTrimmerViewModel;

        public ElevationChartControl()
        {
            this.InitializeComponent();
            this.DataContextChanged += ElevationChartControl_DataContextChanged;
            this.KeyDown += ElevationChartControl_KeyDown;
        }

        private void ElevationChartControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is TrackTrimmerViewModel oldVm)
            {
                oldVm.TrackLoaded -= Redraw;
                oldVm.TrackDataChanged -= Redraw;
                oldVm.TrimChanged -= UpdateTrimMarkers;
                oldVm.SelectionChanged -= UpdateSelectionIndicator;
            }

            if (e.NewValue is TrackTrimmerViewModel newVm)
            {
                newVm.TrackLoaded += Redraw;
                newVm.TrackDataChanged += Redraw;
                newVm.TrimChanged += UpdateTrimMarkers;
                newVm.SelectionChanged += UpdateSelectionIndicator;
                Redraw();
            }
        }

        private void ChartArea_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _chartWidth = ChartArea.ActualWidth;
            _chartHeight = ChartArea.ActualHeight;
            Redraw();
        }

        public void Redraw()
        {
            ProfileCanvas.Children.Clear();
            if (ViewModel?.Document == null || ViewModel.Document.Points.Count < 2 || _chartWidth <= 0 || _chartHeight <= 0)
            {
                return;
            }

            var points = ViewModel.Document.Points;
            double totalDist = points[points.Count - 1].DistanceFromStart;
            if (totalDist <= 0) return;

            // Sync range slider
            UpdateRangeSliderVisuals();

            double minEle = double.MaxValue;
            double maxEle = double.MinValue;
            foreach (var pt in points)
            {
                double ele = pt.Elevation ?? 0;
                if (ele < minEle) minEle = ele;
                if (ele > maxEle) maxEle = ele;
            }

            if (Math.Abs(maxEle - minEle) < 1.0)
            {
                maxEle += 10;
                minEle -= 10;
            }

            _minEle = minEle;
            _maxEle = maxEle;

            double paddingY = 16;
            double usableHeight = _chartHeight - (paddingY * 2);

            // Draw horizontal elevation grid lines (3 lines)
            for (int i = 0; i <= 3; i++)
            {
                double ratio = i / 3.0;
                double eleVal = minEle + ratio * (maxEle - minEle);
                double y = _chartHeight - paddingY - (ratio * usableHeight);

                var line = new Line
                {
                    X1 = 0,
                    Y1 = y,
                    X2 = _chartWidth,
                    Y2 = y,
                    Stroke = new SolidColorBrush(Color.FromArgb(50, 148, 163, 184)),
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 2, 4 }
                };
                ProfileCanvas.Children.Add(line);

                var txt = new TextBlock
                {
                    Text = $"{eleVal * 3.280839895:F0} ft",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromArgb(140, 148, 163, 184))
                };
                Canvas.SetLeft(txt, 8);
                Canvas.SetTop(txt, Math.Max(0, y - 14));
                ProfileCanvas.Children.Add(txt);
            }

            // Downsample points for visual rendering (LOD)
            IList<TrackPoint> visualPoints = DownsamplePoints(points, MaxVisualPoints);

            // Draw elevation area polygon & line
            var polylinePoints = new PointCollection();
            var polygonPoints = new PointCollection();

            polygonPoints.Add(new Point(0, _chartHeight));

            for (int i = 0; i < visualPoints.Count; i++)
            {
                var pt = visualPoints[i];
                double x = (pt.DistanceFromStart / totalDist) * _chartWidth;
                double ele = pt.Elevation ?? minEle;
                double normalized = (ele - minEle) / (maxEle - minEle);
                double y = _chartHeight - paddingY - (normalized * usableHeight);

                var p = new Point(x, y);
                polylinePoints.Add(p);
                polygonPoints.Add(p);
            }

            polygonPoints.Add(new Point(_chartWidth, _chartHeight));

            // Gradient Fill Polygon
            var polygon = new Polygon
            {
                Points = polygonPoints,
                Fill = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(0, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop { Color = Color.FromArgb(160, 14, 165, 233), Offset = 0.0 },
                        new GradientStop { Color = Color.FromArgb(50, 3, 105, 161), Offset = 0.7 },
                        new GradientStop { Color = Color.FromArgb(10, 15, 23, 42), Offset = 1.0 }
                    }
                }
            };
            ProfileCanvas.Children.Add(polygon);

            // Polyline border curve
            var polyline = new Polyline
            {
                Points = polylinePoints,
                Stroke = new SolidColorBrush(Color.FromArgb(255, 56, 189, 248)),
                StrokeThickness = 2
            };
            ProfileCanvas.Children.Add(polyline);

            UpdateTrimMarkers();
            UpdateSelectionIndicator();
        }

        private static IList<TrackPoint> DownsamplePoints(IList<TrackPoint> input, int maxPoints)
        {
            if (input == null || input.Count <= maxPoints)
            {
                return input;
            }

            var result = new List<TrackPoint>(maxPoints);
            int bucketCount = maxPoints / 2;
            double step = (double)(input.Count - 1) / bucketCount;

            result.Add(input[0]);

            for (int b = 0; b < bucketCount; b++)
            {
                int start = (int)(b * step);
                int end = (int)((b + 1) * step);
                if (end >= input.Count) end = input.Count - 1;
                if (start >= end) continue;

                int minIdx = start;
                int maxIdx = start;
                double minVal = input[start].Elevation ?? double.MaxValue;
                double maxVal = input[start].Elevation ?? double.MinValue;

                for (int i = start + 1; i <= end; i++)
                {
                    double val = input[i].Elevation ?? 0;
                    if (val < minVal) { minVal = val; minIdx = i; }
                    if (val > maxVal) { maxVal = val; maxIdx = i; }
                }

                // Preserve in chronological order
                if (minIdx < maxIdx)
                {
                    result.Add(input[minIdx]);
                    result.Add(input[maxIdx]);
                }
                else
                {
                    result.Add(input[maxIdx]);
                    if (minIdx != maxIdx) result.Add(input[minIdx]);
                }
            }

            if (result[result.Count - 1] != input[input.Count - 1])
            {
                result.Add(input[input.Count - 1]);
            }

            return result;
        }

        public void UpdateTrimMarkers()
        {
            if (ViewModel?.Document == null || ViewModel.Document.Points.Count < 2 || _chartWidth <= 0 || _chartHeight <= 0)
            {
                return;
            }

            var points = ViewModel.Document.Points;
            double totalDist = points[points.Count - 1].DistanceFromStart;
            if (totalDist <= 0) return;

            int startIdx = Math.Max(0, Math.Min(ViewModel.TrimStartIndex, points.Count - 1));
            int endIdx = Math.Max(0, Math.Min(ViewModel.TrimEndIndex, points.Count - 1));

            double startX = (points[startIdx].DistanceFromStart / totalDist) * _chartWidth;
            double endX = (points[endIdx].DistanceFromStart / totalDist) * _chartWidth;

            // Start Marker Line & Handle
            StartMarkerLine.X1 = startX;
            StartMarkerLine.Y1 = 0;
            StartMarkerLine.X2 = startX;
            StartMarkerLine.Y2 = _chartHeight;
            Canvas.SetLeft(StartHandle, Math.Max(0, startX - 10));
            Canvas.SetTop(StartHandle, 4);

            // End Marker Line & Handle
            EndMarkerLine.X1 = endX;
            EndMarkerLine.Y1 = 0;
            EndMarkerLine.X2 = endX;
            EndMarkerLine.Y2 = _chartHeight;
            Canvas.SetLeft(EndHandle, Math.Min(_chartWidth - 20, endX - 10));
            Canvas.SetTop(EndHandle, 4);

            // Dim Overlays
            if (startX > 1)
            {
                LeftDimOverlay.Visibility = Visibility.Visible;
                LeftDimOverlay.Width = startX;
            }
            else
            {
                LeftDimOverlay.Visibility = Visibility.Collapsed;
            }

            if (_chartWidth - endX > 1)
            {
                RightDimOverlay.Visibility = Visibility.Visible;
                RightDimOverlay.Width = _chartWidth - endX;
            }
            else
            {
                RightDimOverlay.Visibility = Visibility.Collapsed;
            }

            // Labels
            TxtStartTrimLabel.Text = $"Start: #{startIdx + 1} ({points[startIdx].DistanceMilesFormatted})";
            TxtEndTrimLabel.Text = $"End: #{endIdx + 1} ({points[endIdx].DistanceMilesFormatted})";

            UpdateRangeSliderVisuals();
        }

        private void UpdateSelectionIndicator()
        {
            if (ViewModel?.Document == null || ViewModel.Document.Points.Count < 2 || _chartWidth <= 0 || _chartHeight <= 0)
            {
                HoverHairline.Visibility = Visibility.Collapsed;
                HoverPointDot.Visibility = Visibility.Collapsed;
                SelectionRangeOverlay.Visibility = Visibility.Collapsed;
                return;
            }

            var points = ViewModel.Document.Points;
            double totalDist = points[points.Count - 1].DistanceFromStart;
            if (totalDist <= 0) return;

            // Render Range Overlay if range is selected
            if (ViewModel.IsRangeSelected)
            {
                int sIdx = ViewModel.SelectionStartIndex;
                int eIdx = ViewModel.SelectionEndIndex;
                if (sIdx >= 0 && eIdx < points.Count && sIdx <= eIdx)
                {
                    double x1 = (points[sIdx].DistanceFromStart / totalDist) * _chartWidth;
                    double x2 = (points[eIdx].DistanceFromStart / totalDist) * _chartWidth;
                    double left = Math.Min(x1, x2);
                    double width = Math.Max(2, Math.Abs(x2 - x1));

                    Canvas.SetLeft(SelectionRangeOverlay, left);
                    Canvas.SetTop(SelectionRangeOverlay, 0);
                    SelectionRangeOverlay.Width = width;
                    SelectionRangeOverlay.Height = _chartHeight;
                    SelectionRangeOverlay.Visibility = Visibility.Visible;
                }
                else
                {
                    SelectionRangeOverlay.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                SelectionRangeOverlay.Visibility = Visibility.Collapsed;
            }

            var pt = ViewModel.SelectedPoint;
            if (pt == null)
            {
                HoverHairline.Visibility = Visibility.Collapsed;
                HoverPointDot.Visibility = Visibility.Collapsed;
                return;
            }

            double x = (pt.DistanceFromStart / totalDist) * _chartWidth;
            HoverHairline.X1 = x;
            HoverHairline.Y1 = 0;
            HoverHairline.X2 = x;
            HoverHairline.Y2 = _chartHeight;
            HoverHairline.Visibility = Visibility.Visible;

            double minEle = _maxEle > _minEle ? _minEle : ViewModel.Document.Statistics.MinElevationMeters;
            double maxEle = _maxEle > _minEle ? _maxEle : ViewModel.Document.Statistics.MaxElevationMeters;
            if (Math.Abs(maxEle - minEle) < 1.0) { maxEle += 10; minEle -= 10; }

            double usableHeight = _chartHeight - 32;
            double ele = pt.Elevation ?? minEle;
            double y = _chartHeight - 16 - (((ele - minEle) / (maxEle - minEle)) * usableHeight);

            Canvas.SetLeft(HoverPointDot, x - 4);
            Canvas.SetTop(HoverPointDot, y - 4);
            HoverPointDot.Visibility = Visibility.Visible;
        }

        private void ChartArea_MouseMove(object sender, MouseEventArgs e)
        {
            if (ViewModel?.Document == null || ViewModel.Document.Points.Count < 2 || _chartWidth <= 0) return;

            var pos = e.GetPosition(ChartArea);
            var points = ViewModel.Document.Points;
            double totalDist = points[points.Count - 1].DistanceFromStart;

            double distTarget = (pos.X / _chartWidth) * totalDist;
            int nearestIdx = FindNearestPointIndex(distTarget);

            if (nearestIdx >= 0 && nearestIdx < points.Count)
            {
                var pt = points[nearestIdx];
                TxtHoverInfo.Text = $"#{pt.Index + 1} · {pt.DistanceMilesFormatted} · {pt.ElevationFormatted} · {pt.TimeFormatted}";
            }
        }

        private void ChartArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (ViewModel?.Document == null || ViewModel.Document.Points.Count < 2 || _chartWidth <= 0) return;

            var pos = e.GetPosition(ChartArea);
            var points = ViewModel.Document.Points;
            double totalDist = points[points.Count - 1].DistanceFromStart;

            double distTarget = (pos.X / _chartWidth) * totalDist;
            int nearestIdx = FindNearestPointIndex(distTarget);
            if (nearestIdx < 0 || nearestIdx >= points.Count) return;

            bool isShift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            if (isShift)
            {
                ViewModel?.SelectPointOrRange(nearestIdx, isShift: true);
                return;
            }

            var now = DateTime.UtcNow;
            bool isDoubleClick = e.ClickCount >= 2 ||
                                ((now - _lastClickTime).TotalMilliseconds < 500 && Math.Abs(pos.X - _lastClickPos.X) < 15);
            _lastClickTime = now;
            _lastClickPos = pos;

            if (isDoubleClick)
            {
                int currentStart = ViewModel.TrimStartIndex;
                int currentEnd = ViewModel.TrimEndIndex;
                double midpoint = (currentStart + currentEnd) / 2.0;

                if (nearestIdx < midpoint)
                {
                    ViewModel.TrimStartIndex = nearestIdx;
                    ViewModel.Logs.Info($"Trim start moved to Point #{nearestIdx + 1} ({points[nearestIdx].DistanceMilesFormatted}) via double-click.", "Trim");
                    ViewModel.StatusMessage = $"Moved trim start to Point #{nearestIdx + 1}";
                }
                else
                {
                    ViewModel.TrimEndIndex = nearestIdx;
                    ViewModel.Logs.Info($"Trim end moved to Point #{nearestIdx + 1} ({points[nearestIdx].DistanceMilesFormatted}) via double-click.", "Trim");
                    ViewModel.StatusMessage = $"Moved trim end to Point #{nearestIdx + 1}";
                }

                UpdateTrimMarkers();
            }

            ViewModel.SelectPointOrRange(nearestIdx, isShift: false);
        }

        private int FindNearestPointIndex(double distanceMeters)
        {
            var points = ViewModel?.Document?.Points;
            if (points == null || points.Count == 0) return -1;

            int low = 0, high = points.Count - 1;
            while (low <= high)
            {
                int mid = (low + high) / 2;
                if (points[mid].DistanceFromStart < distanceMeters)
                    low = mid + 1;
                else
                    high = mid - 1;
            }

            if (low >= points.Count) return points.Count - 1;
            if (low <= 0) return 0;

            double d1 = Math.Abs(points[low - 1].DistanceFromStart - distanceMeters);
            double d2 = Math.Abs(points[low].DistanceFromStart - distanceMeters);
            return d1 < d2 ? low - 1 : low;
        }

        // Draggable Start Handle
        private void StartHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDraggingStart = true;
            _dragStartPoint = e.GetPosition(ChartArea);
            StartHandle.CaptureMouse();
            e.Handled = true;
        }

        private void StartHandle_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDraggingStart || ViewModel?.Document == null || _chartWidth <= 0) return;

            var pos = e.GetPosition(ChartArea);
            double totalDist = ViewModel.Document.Points[ViewModel.Document.Points.Count - 1].DistanceFromStart;
            double targetDist = Math.Max(0, Math.Min(pos.X / _chartWidth, 1.0)) * totalDist;

            int idx = FindNearestPointIndex(targetDist);
            if (idx <= ViewModel.TrimEndIndex)
            {
                ViewModel.TrimStartIndex = idx;
            }
        }

        // Draggable End Handle
        private void EndHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDraggingEnd = true;
            _dragStartPoint = e.GetPosition(ChartArea);
            EndHandle.CaptureMouse();
            e.Handled = true;
        }

        private void EndHandle_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDraggingEnd || ViewModel?.Document == null || _chartWidth <= 0) return;

            var pos = e.GetPosition(ChartArea);
            double totalDist = ViewModel.Document.Points[ViewModel.Document.Points.Count - 1].DistanceFromStart;
            double targetDist = Math.Max(0, Math.Min(pos.X / _chartWidth, 1.0)) * totalDist;

            int idx = FindNearestPointIndex(targetDist);
            if (idx >= ViewModel.TrimStartIndex)
            {
                ViewModel.TrimEndIndex = idx;
            }
        }

        private void Handle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingStart)
            {
                _isDraggingStart = false;
                StartHandle.ReleaseMouseCapture();
            }
            if (_isDraggingEnd)
            {
                _isDraggingEnd = false;
                EndHandle.ReleaseMouseCapture();
            }
        }

        private bool _isDraggingSliderStart;
        private bool _isDraggingSliderEnd;
        private bool _isDraggingSliderMiddle;
        private double _sliderMiddleDragStartPos;
        private int _sliderMiddleStartIdx;
        private int _sliderMiddleEndIdx;

        private void RangeSliderContainer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateRangeSliderVisuals();
        }

        private void UpdateRangeSliderVisuals()
        {
            if (ViewModel?.Document == null || ViewModel.Document.Points.Count == 0) return;
            var points = ViewModel.Document.Points;
            int startIdx = Math.Max(0, Math.Min(ViewModel.TrimStartIndex, points.Count - 1));
            int endIdx = Math.Max(0, Math.Min(ViewModel.TrimEndIndex, points.Count - 1));

            TxtTrimStartInfo.Text = $"#{startIdx + 1} ({points[startIdx].DistanceMilesFormatted})";
            TxtTrimEndInfo.Text = $"#{endIdx + 1} ({points[endIdx].DistanceMilesFormatted})";

            double sliderWidth = RangeSliderContainer.ActualWidth;
            if (sliderWidth <= 20) return;

            double usableWidth = sliderWidth - 18.0;
            int count = points.Count;
            double totalDist = points[count - 1].DistanceFromStart;

            double startFrac = totalDist > 0
                ? Math.Max(0, Math.Min(1.0, points[startIdx].DistanceFromStart / totalDist))
                : (count > 1 ? (double)startIdx / (count - 1) : 0);

            double endFrac = totalDist > 0
                ? Math.Max(0, Math.Min(1.0, points[endIdx].DistanceFromStart / totalDist))
                : (count > 1 ? (double)endIdx / (count - 1) : 1);

            double startX = startFrac * usableWidth;
            double endX = endFrac * usableWidth;

            Canvas.SetLeft(RangeStartThumb, startX);
            Canvas.SetLeft(RangeEndThumb, endX);

            Canvas.SetLeft(RangeActiveHighlight, startX + 9);
            RangeActiveHighlight.Width = Math.Max(0, endX - startX);
        }

        private int SliderPosToPointIndex(double mouseX)
        {
            if (ViewModel?.Document == null || ViewModel.Document.Points.Count == 0) return 0;
            var points = ViewModel.Document.Points;
            int count = points.Count;
            if (count <= 1) return 0;

            double sliderWidth = RangeSliderContainer.ActualWidth;
            double usableWidth = sliderWidth - 18.0;
            if (usableWidth <= 0) return 0;

            double thumbCenterX = mouseX - 9.0;
            double frac = Math.Max(0, Math.Min(1.0, thumbCenterX / usableWidth));

            double totalDist = points[count - 1].DistanceFromStart;
            if (totalDist > 0)
            {
                return FindNearestPointIndex(frac * totalDist);
            }
            return (int)Math.Round(frac * (count - 1));
        }

        private void RangeStartThumb_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _isDraggingSliderStart = true;
            RangeStartThumb.CaptureMouse();
            e.Handled = true;
        }

        private void RangeStartThumb_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDraggingSliderStart || ViewModel?.Document == null) return;
            var pos = e.GetPosition(RangeSliderContainer);
            int idx = SliderPosToPointIndex(pos.X);
            if (idx <= ViewModel.TrimEndIndex && idx != ViewModel.TrimStartIndex)
            {
                ViewModel.TrimStartIndex = idx;
            }
        }

        private void RangeEndThumb_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _isDraggingSliderEnd = true;
            RangeEndThumb.CaptureMouse();
            e.Handled = true;
        }

        private void RangeEndThumb_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDraggingSliderEnd || ViewModel?.Document == null) return;
            var pos = e.GetPosition(RangeSliderContainer);
            int idx = SliderPosToPointIndex(pos.X);
            if (idx >= ViewModel.TrimStartIndex && idx != ViewModel.TrimEndIndex)
            {
                ViewModel.TrimEndIndex = idx;
            }
        }

        private void RangeThumb_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingSliderStart)
            {
                _isDraggingSliderStart = false;
                RangeStartThumb.ReleaseMouseCapture();
            }
            if (_isDraggingSliderEnd)
            {
                _isDraggingSliderEnd = false;
                RangeEndThumb.ReleaseMouseCapture();
            }
        }

        private void RangeActiveHighlight_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (ViewModel?.Document == null) return;
            _isDraggingSliderMiddle = true;
            _sliderMiddleDragStartPos = e.GetPosition(RangeSliderContainer).X;
            _sliderMiddleStartIdx = ViewModel.TrimStartIndex;
            _sliderMiddleEndIdx = ViewModel.TrimEndIndex;
            RangeActiveHighlight.CaptureMouse();
            e.Handled = true;
        }

        private void RangeActiveHighlight_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDraggingSliderMiddle || ViewModel?.Document == null) return;
            var points = ViewModel.Document.Points;
            int count = points.Count;
            if (count <= 1) return;

            double currentX = e.GetPosition(RangeSliderContainer).X;
            double dx = currentX - _sliderMiddleDragStartPos;

            double usableWidth = RangeSliderContainer.ActualWidth - 18.0;
            if (usableWidth <= 0) return;

            double totalDist = points[count - 1].DistanceFromStart;
            int span = _sliderMiddleEndIdx - _sliderMiddleStartIdx;

            if (totalDist > 0)
            {
                double initialStartDist = points[_sliderMiddleStartIdx].DistanceFromStart;
                double targetStartDist = Math.Max(0, initialStartDist + (dx / usableWidth) * totalDist);
                int newStart = FindNearestPointIndex(targetStartDist);
                newStart = Math.Max(0, Math.Min(count - 1 - span, newStart));
                int newEnd = newStart + span;

                if (newStart != ViewModel.TrimStartIndex || newEnd != ViewModel.TrimEndIndex)
                {
                    ViewModel.TrimStartIndex = newStart;
                    ViewModel.TrimEndIndex = newEnd;
                }
            }
            else
            {
                int deltaIdx = (int)Math.Round((dx / usableWidth) * (count - 1));
                int newStart = Math.Max(0, Math.Min(count - 1 - span, _sliderMiddleStartIdx + deltaIdx));
                int newEnd = newStart + span;

                if (newStart != ViewModel.TrimStartIndex || newEnd != ViewModel.TrimEndIndex)
                {
                    ViewModel.TrimStartIndex = newStart;
                    ViewModel.TrimEndIndex = newEnd;
                }
            }
        }

        private void RangeActiveHighlight_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingSliderMiddle)
            {
                _isDraggingSliderMiddle = false;
                RangeActiveHighlight.ReleaseMouseCapture();
            }
        }

        private void RangeSlider_Track_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (ViewModel?.Document == null) return;
            var pos = e.GetPosition(RangeSliderContainer);
            int clickedIdx = SliderPosToPointIndex(pos.X);

            int distToStart = Math.Abs(clickedIdx - ViewModel.TrimStartIndex);
            int distToEnd = Math.Abs(clickedIdx - ViewModel.TrimEndIndex);

            if (distToStart <= distToEnd)
            {
                ViewModel.TrimStartIndex = Math.Min(clickedIdx, ViewModel.TrimEndIndex);
            }
            else
            {
                ViewModel.TrimEndIndex = Math.Max(clickedIdx, ViewModel.TrimStartIndex);
            }
        }

        private void ElevationChartControl_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                if (ViewModel?.SelectedPoint != null)
                {
                    ViewModel.DeleteSelectedPoint();
                    e.Handled = true;
                }
            }
        }
    }
}
