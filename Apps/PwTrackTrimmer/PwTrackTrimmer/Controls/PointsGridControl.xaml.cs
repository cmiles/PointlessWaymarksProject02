using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using OpenSilver;
using PwTrackTrimmer.Models;
using PwTrackTrimmer.ViewModels;

namespace PwTrackTrimmer.Controls
{
    public partial class PointsGridControl : UserControl
    {
        private TrackTrimmerViewModel ViewModel => DataContext as TrackTrimmerViewModel;

        public PointsGridControl()
        {
            this.InitializeComponent();
            this.KeyDown += PointsGridControl_KeyDown;
            this.DataContextChanged += PointsGridControl_DataContextChanged;
            this.PointsListBox.SelectionChanged += PointsListBox_SelectionChanged;
        }

        public void SetIsNarrow(bool isNarrow)
        {
            if (RootBorder != null)
            {
                RootBorder.BorderThickness = isNarrow ? new Thickness(0, 1, 0, 0) : new Thickness(1, 0, 0, 0);
            }
        }

        private void PointsGridControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is TrackTrimmerViewModel oldVm)
            {
                oldVm.SelectionChanged -= Vm_SelectionChanged;
            }
            if (e.NewValue is TrackTrimmerViewModel newVm)
            {
                newVm.SelectionChanged += Vm_SelectionChanged;
            }
        }

        private void Vm_SelectionChanged()
        {
            ScrollSelectedPointIntoView();
        }

        private void PointsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ScrollSelectedPointIntoView();
        }

        private void ScrollSelectedPointIntoView()
        {
            var selectedItem = ViewModel?.SelectedPoint ?? PointsListBox.SelectedItem as TrackPoint;
            if (selectedItem == null) return;

            // Attempt immediate scroll
            TryScroll(selectedItem);

            // If item containers are currently generating, wait for completion
            if (PointsListBox.ItemContainerGenerator.Status != GeneratorStatus.ContainersGenerated)
            {
                EventHandler statusHandler = null;
                statusHandler = (s, e) =>
                {
                    if (PointsListBox.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
                    {
                        PointsListBox.ItemContainerGenerator.StatusChanged -= statusHandler;
                        Dispatcher.BeginInvoke(() => TryScroll(selectedItem));
                    }
                };
                PointsListBox.ItemContainerGenerator.StatusChanged += statusHandler;
            }

            // Retry on short intervals to guarantee DOM mounting and layout completion
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            int retries = 0;
            timer.Tick += (s, e) =>
            {
                retries++;
                bool scrolled = TryScroll(selectedItem);
                if (scrolled || retries >= 8 || ViewModel?.SelectedPoint != selectedItem)
                {
                    timer.Stop();
                }
            };
            timer.Start();
        }

        private bool TryScroll(TrackPoint item)
        {
            if (ViewModel?.SelectedPoint != item && PointsListBox.SelectedItem != item) return true;

            int index = PointsListBox.Items.IndexOf(item);
            if (index < 0) return false;

            var container = PointsListBox.ItemContainerGenerator.ContainerFromIndex(index) as ListBoxItem;
            if (container != null)
            {
                PointsListBox.ScrollIntoView(item);

                object div = Interop.GetDiv(container);
                if (div != null)
                {
                    Interop.ExecuteJavaScriptVoid("$0.scrollIntoView({ block: 'nearest', behavior: 'smooth' });", div);
                    return true;
                }
            }
            else
            {
                PointsListBox.ScrollIntoView(item);
            }
            return false;
        }

        private void PointsGridControl_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Delete)
            {
                ViewModel?.DeleteSelectedPoint();
                e.Handled = true;
            }
        }

        private void DeleteRow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is TrackPoint pt)
            {
                ViewModel?.DeletePoints(new[] { pt });
            }
        }

        private void Row_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is TrackPoint pt)
            {
                bool isShift = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) == System.Windows.Input.ModifierKeys.Shift;
                ViewModel?.SelectPointOrRange(pt.Index, isShift);
                e.Handled = true;
            }
        }
    }
}
