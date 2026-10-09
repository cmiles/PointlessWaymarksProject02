using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PwTrackTrimmer.Services;
using PwTrackTrimmer.ViewModels;

namespace PwTrackTrimmer
{
    public partial class MainPage : Page
    {
        public TrackTrimmerViewModel ViewModel { get; }

        public MainPage()
        {
            this.InitializeComponent();

            ViewModel = new TrackTrimmerViewModel();
            this.DataContext = ViewModel;

            this.KeyDown += MainPage_KeyDown;
            this.SizeChanged += (s, e) => UpdateResponsiveLayout(e.NewSize.Width);
            this.Loaded += (s, e) =>
            {
                SetupFileDragAndDrop();
                InjectGlobalCaretStyles();
                UpdateResponsiveLayout(this.ActualWidth);
            };
        }

        private void SetupFileDragAndDrop()
        {
            FileDropHelper.Initialize(
                onFileDropped: (fileName, bytes) =>
                {
                    Dispatcher.BeginInvoke(async () =>
                    {
                        if (bytes == null || bytes.Length == 0) return;

                        string ext = Path.GetExtension(fileName)?.ToLowerInvariant();
                        if (ext != ".gpx" && ext != ".tcx" && ext != ".fit")
                        {
                            ViewModel.StatusMessage = $"Cannot open '{fileName}': Unsupported format. Please drop a .gpx, .tcx, or .fit file.";
                            ViewModel.Logs.Warn($"Dropped unsupported file '{fileName}'. Only .gpx, .tcx, and .fit files are supported.", "I/O");
                            return;
                        }

                        ViewModel.Logs.Info($"File dropped: '{fileName}' ({bytes.Length:N0} bytes). Loading...", "I/O");
                        using var stream = new MemoryStream(bytes);
                        await ViewModel.LoadFromStreamAsync(stream, fileName);
                    });
                },
                onDragEnter: () =>
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        ViewModel.IsDragOver = true;
                    });
                },
                onDragLeave: () =>
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        ViewModel.IsDragOver = false;
                    });
                }
            );
        }

        private void MainPage_KeyDown(object sender, KeyEventArgs e)
        {
            // Keyboard shortcuts
            bool isCtrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

            if (isCtrl && e.Key == Key.Z)
            {
                if (ViewModel.UndoRedo.CanUndo)
                {
                    ViewModel.UndoRedo.Undo();
                    e.Handled = true;
                }
            }
            else if (isCtrl && e.Key == Key.Y)
            {
                if (ViewModel.UndoRedo.CanRedo)
                {
                    ViewModel.UndoRedo.Redo();
                    e.Handled = true;
                }
            }
            else if (isCtrl && e.Key == Key.O)
            {
                ViewModel.LoadFileCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.Delete)
            {
                if (ViewModel?.SelectedPoint != null || ViewModel?.SelectionStartIndex >= 0)
                {
                    ViewModel.DeleteSelectedPoint();
                    e.Handled = true;
                }
            }
        }

        private void Hyperlink_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is HyperlinkButton btn && btn.NavigateUri != null)
            {
                try
                {
                    OpenSilver.Interop.ExecuteJavaScriptVoid($"window.open('{btn.NavigateUri.AbsoluteUri}', '_blank');");
                }
                catch
                {
                    // Fallback to default HyperlinkButton behavior
                }
            }
        }

        private void ToolsButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateToolsMenuCardPosition();
        }

        private void MenuItem_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.IsToolsMenuOpen = false;
            }
        }

        private void ToolsMenu_Backdrop_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.IsToolsMenuOpen = false;
            }
        }

        private void InjectGlobalCaretStyles()
        {
            try
            {
                OpenSilver.Interop.ExecuteJavaScriptVoid(@"
                    (function() {
                        var styleId = 'pw-global-caret-styles';
                        if (!document.getElementById(styleId)) {
                            var style = document.createElement('style');
                            style.id = styleId;
                            style.textContent = 'input, textarea, .opensilver-textboxview { caret-color: #38BDF8 !important; }';
                            document.head.appendChild(style);
                        }
                    })();
                ");
            }
            catch { }
        }

        private bool _isNarrowLayout = false;
        private bool _layoutInitialized = false;
        private const double NarrowLayoutBreakpoint = 850.0;
        private GridLength _savedWidePointsWidth = new GridLength(360, GridUnitType.Pixel);

        private void UpdateResponsiveLayout(double width)
        {
            if (width <= 0) return;

            bool shouldBeNarrow = width < NarrowLayoutBreakpoint;
            if (shouldBeNarrow == _isNarrowLayout && _layoutInitialized)
            {
                UpdateToolsMenuCardPosition();
                return;
            }

            if (_layoutInitialized && MainAreaGrid != null && MainAreaGrid.ColumnDefinitions.Count >= 3)
            {
                if (!_isNarrowLayout)
                {
                    var currentWidth = MainAreaGrid.ColumnDefinitions[2].Width;
                    if (currentWidth.Value > 50)
                    {
                        _savedWidePointsWidth = currentWidth;
                    }
                }
            }

            _isNarrowLayout = shouldBeNarrow;
            _layoutInitialized = true;

            if (MainAreaGrid == null || MainAreaGrid.ColumnDefinitions.Count < 3 || PointsGridControlElement == null || MainAreaSplitter == null)
                return;

            if (_isNarrowLayout)
            {
                // Narrow / Small Screen: Hide Track Points list and splitter; collapse columns to 0 so Map takes full area
                PointsGridControlElement.Visibility = Visibility.Collapsed;
                MainAreaSplitter.Visibility = Visibility.Collapsed;

                MainAreaGrid.ColumnDefinitions[1].Width = new GridLength(0);
                MainAreaGrid.ColumnDefinitions[2].MinWidth = 0;
                MainAreaGrid.ColumnDefinitions[2].Width = new GridLength(0);

                // Enable vertical scrollbar on small screens so the elevation profile is accessible
                if (RootScrollViewer != null)
                {
                    RootScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                }

                if (RowMainArea != null)
                {
                    RowMainArea.Height = new GridLength(380, GridUnitType.Pixel);
                    RowMainArea.MinHeight = 300;
                }

                if (RowElevationChart != null)
                {
                    RowElevationChart.Height = new GridLength(220, GridUnitType.Pixel);
                    RowElevationChart.MinHeight = 180;
                }

                if (BottomAreaSplitter != null)
                {
                    BottomAreaSplitter.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                // Wide / Large Screen: Restore Track Points list and splitter beside the map
                PointsGridControlElement.Visibility = Visibility.Visible;
                MainAreaSplitter.Visibility = Visibility.Visible;

                MainAreaGrid.ColumnDefinitions[1].Width = new GridLength(4, GridUnitType.Pixel);
                MainAreaGrid.ColumnDefinitions[2].MinWidth = 260;
                MainAreaGrid.ColumnDefinitions[2].Width = _savedWidePointsWidth;

                PointsGridControlElement.SetIsNarrow(false);

                // Wide screen fits cleanly on 1 screen height without page scrolling
                if (RootScrollViewer != null)
                {
                    RootScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                    RootScrollViewer.ScrollToVerticalOffset(0);
                }

                if (RowMainArea != null)
                {
                    RowMainArea.Height = new GridLength(1, GridUnitType.Star);
                    RowMainArea.MinHeight = 250;
                }

                if (RowElevationChart != null)
                {
                    RowElevationChart.Height = new GridLength(200, GridUnitType.Pixel);
                    RowElevationChart.MinHeight = 140;
                }

                if (BottomAreaSplitter != null)
                {
                    BottomAreaSplitter.Visibility = Visibility.Visible;
                }
            }

            UpdateToolsMenuCardPosition();
        }

        private void UpdateToolsMenuCardPosition()
        {
            if (ToolsMenuCard != null)
            {
                double topOffset = TopBarBorder != null && TopBarBorder.ActualHeight > 0
                    ? TopBarBorder.ActualHeight + 4
                    : 46;
                ToolsMenuCard.Margin = new Thickness(0, topOffset, 14, 0);
            }
        }
    }
}
