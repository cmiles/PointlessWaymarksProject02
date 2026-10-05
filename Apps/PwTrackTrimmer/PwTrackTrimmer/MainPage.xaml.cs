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
        private GridLength _savedNarrowMapHeight = new GridLength(420, GridUnitType.Pixel);
        private GridLength _savedNarrowPointsHeight = new GridLength(340, GridUnitType.Pixel);

        private void UpdateResponsiveLayout(double width)
        {
            if (width <= 0) return;

            bool shouldBeNarrow = width < NarrowLayoutBreakpoint;
            if (shouldBeNarrow == _isNarrowLayout && _layoutInitialized)
            {
                UpdateToolsMenuCardPosition();
                return;
            }

            if (_layoutInitialized && MainAreaGrid != null)
            {
                if (!_isNarrowLayout && MainAreaGrid.ColumnDefinitions.Count >= 3)
                {
                    var currentWidth = MainAreaGrid.ColumnDefinitions[2].Width;
                    if (currentWidth.Value > 50)
                    {
                        _savedWidePointsWidth = currentWidth;
                    }
                }
                else if (_isNarrowLayout && MainAreaGrid.RowDefinitions.Count >= 3)
                {
                    var currentMapHeight = MainAreaGrid.RowDefinitions[0].Height;
                    if (currentMapHeight.Value > 100)
                    {
                        _savedNarrowMapHeight = currentMapHeight;
                    }
                    var currentPointsHeight = MainAreaGrid.RowDefinitions[2].Height;
                    if (currentPointsHeight.Value > 100)
                    {
                        _savedNarrowPointsHeight = currentPointsHeight;
                    }
                }
            }

            _isNarrowLayout = shouldBeNarrow;
            _layoutInitialized = true;

            if (MainAreaGrid == null || MapControlElement == null || PointsGridControlElement == null || MainAreaSplitter == null)
                return;

            if (_isNarrowLayout)
            {
                // Narrow / Small Screen: App adjusts to be more than 1 screen tall with full vertical scrolling
                if (RootScrollViewer != null)
                {
                    RootScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                }

                if (RowMainArea != null)
                {
                    RowMainArea.Height = GridLength.Auto;
                    RowMainArea.MinHeight = 0;
                }

                if (RowElevationChart != null)
                {
                    RowElevationChart.Height = new GridLength(240, GridUnitType.Pixel);
                    RowElevationChart.MinHeight = 180;
                }

                if (BottomAreaSplitter != null)
                {
                    BottomAreaSplitter.Visibility = Visibility.Collapsed;
                }

                // Inside MainArea: Map on top (generous height), Points list wrapped underneath (generous height)
                MainAreaGrid.ColumnDefinitions.Clear();
                MainAreaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                MainAreaGrid.RowDefinitions.Clear();
                MainAreaGrid.RowDefinitions.Add(new RowDefinition { Height = _savedNarrowMapHeight, MinHeight = 260 });
                MainAreaGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(4, GridUnitType.Pixel) });
                MainAreaGrid.RowDefinitions.Add(new RowDefinition { Height = _savedNarrowPointsHeight, MinHeight = 200 });

                Grid.SetRow(MapControlElement, 0);
                Grid.SetColumn(MapControlElement, 0);

                Grid.SetRow(MainAreaSplitter, 1);
                Grid.SetColumn(MainAreaSplitter, 0);
                MainAreaSplitter.Height = 4;
                MainAreaSplitter.Width = double.NaN;
                MainAreaSplitter.HorizontalAlignment = HorizontalAlignment.Stretch;
                MainAreaSplitter.VerticalAlignment = VerticalAlignment.Stretch;

                Grid.SetRow(PointsGridControlElement, 2);
                Grid.SetColumn(PointsGridControlElement, 0);

                PointsGridControlElement.SetIsNarrow(true);
            }
            else
            {
                // Wide / Large Screen: App fits cleanly on 1 screen height without whole-page scroll
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

                // Inside MainArea: Map on left, Points list beside it on right
                MainAreaGrid.RowDefinitions.Clear();
                MainAreaGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                MainAreaGrid.ColumnDefinitions.Clear();
                MainAreaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 300 });
                MainAreaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4, GridUnitType.Pixel) });
                MainAreaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = _savedWidePointsWidth, MinWidth = 260, MaxWidth = 600 });

                Grid.SetRow(MapControlElement, 0);
                Grid.SetColumn(MapControlElement, 0);

                Grid.SetRow(MainAreaSplitter, 0);
                Grid.SetColumn(MainAreaSplitter, 1);
                MainAreaSplitter.Width = 4;
                MainAreaSplitter.Height = double.NaN;
                MainAreaSplitter.HorizontalAlignment = HorizontalAlignment.Stretch;
                MainAreaSplitter.VerticalAlignment = VerticalAlignment.Stretch;

                Grid.SetRow(PointsGridControlElement, 0);
                Grid.SetColumn(PointsGridControlElement, 2);

                PointsGridControlElement.SetIsNarrow(false);
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
