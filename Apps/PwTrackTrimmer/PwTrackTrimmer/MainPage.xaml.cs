#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
                ThemeManager.Current.ThemeChanged += (_, _) => ApplyTheme();
                ThemeManager.Current.Initialize();
                ApplyTheme();
                Dispatcher.BeginInvoke(() =>
                {
                    ApplyTheme();
                    UpdateResponsiveLayout(this.ActualWidth);
                });
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

        private readonly Dictionary<DependencyObject, (Brush? Background, Brush? BorderBrush, Brush? Foreground)> _originalDarkBrushes = new();

        private void BtnThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            ThemeManager.Current.CycleTheme();
        }

        private void ApplyTheme()
        {
            if (TxtThemeToggle != null)
            {
                TxtThemeToggle.Text = ThemeManager.Current.ThemeButtonLabel;
            }

            bool isDark = ThemeManager.Current.IsDark;

            this.Background = ThemeManager.Current.PageBackground;

            if (OuterRootGrid != null)
            {
                OuterRootGrid.Background = ThemeManager.Current.PageBackground;
            }

            if (RootScrollViewer != null)
            {
                RootScrollViewer.Background = ThemeManager.Current.PageBackground;
            }

            if (TopBarBorder != null)
            {
                TopBarBorder.Background = isDark ? ThemeManager.BrushDarkTopBarBg : ThemeManager.BrushLightTopBarBg;
                TopBarBorder.BorderBrush = isDark ? ThemeManager.BrushDarkDivider : ThemeManager.BrushLightDivider;
            }

            if (RootLayoutGrid != null)
            {
                RootLayoutGrid.Background = ThemeManager.Current.PageBackground;
                ApplyThemeToTree(RootLayoutGrid, isDark);
            }

            if (ToolsMenuCard != null)
            {
                ApplyThemeToTree(ToolsMenuCard, isDark);
            }

            ElevationChartControlElement?.Redraw();
        }

        private void ApplyThemeToTree(DependencyObject obj, bool isDark)
        {
            if (obj is Controls.MapControl)
            {
                return;
            }

            if (obj == OuterRootGrid || obj == RootLayoutGrid || obj == RootScrollViewer || obj == TopBarBorder)
            {
                int count = VisualTreeHelper.GetChildrenCount(obj);
                for (int i = 0; i < count; i++)
                {
                    var child = VisualTreeHelper.GetChild(obj, i);
                    if (child != null)
                    {
                        ApplyThemeToTree(child, isDark);
                    }
                }
                return;
            }

            if (!_originalDarkBrushes.ContainsKey(obj))
            {
                Brush? origBg = (obj as Control)?.Background ?? (obj as Border)?.Background ?? (obj as Panel)?.Background;
                Brush? origBorder = (obj as Control)?.BorderBrush ?? (obj as Border)?.BorderBrush;
                Brush? origFg = (obj as Control)?.Foreground ?? (obj as TextBlock)?.Foreground;
                _originalDarkBrushes[obj] = (origBg, origBorder, origFg);
            }

            var (darkBg, darkBorder, darkFg) = _originalDarkBrushes[obj];

            if (!isDark)
            {
                // Applying LIGHT mode
                if (obj is Border b)
                {
                    if (darkBg is SolidColorBrush scbBg)
                    {
                        if (IsColor(scbBg, 15, 23, 42))        // #0F172A
                            b.Background = ThemeManager.BrushLightSubCardBg;
                        else if (IsColor(scbBg, 9, 13, 22))    // #090D16
                            b.Background = ThemeManager.BrushLightTopBarBg;
                        else if (IsColor(scbBg, 30, 41, 59))   // #1E293B
                            b.Background = ThemeManager.BrushLightCardBg;
                        else if (IsColor(scbBg, 11, 15, 25))   // #0B0F19
                            b.Background = ThemeManager.BrushLightChartBg;
                        else if (IsColor(scbBg, 11, 17, 32))   // #0B1120
                            b.Background = ThemeManager.BrushLightPageBg;
                        else if (IsColor(scbBg, 51, 65, 85))   // #334155
                            b.Background = ThemeManager.BrushLightSubCardBg;
                        else if (IsColor(scbBg, 6, 9, 17))     // #060911
                            b.Background = ThemeManager.BrushLightSubCardBg;
                        else if (IsColor(scbBg, 9, 14, 23))    // #090E17
                            b.Background = ThemeManager.BrushLightSubCardBg;
                    }

                    if (darkBorder is SolidColorBrush scbBd)
                    {
                        if (IsColor(scbBd, 51, 65, 85) || IsColor(scbBd, 30, 41, 59)) // #334155 or #1E293B
                            b.BorderBrush = ThemeManager.BrushLightCardBorder;
                        else if (IsColor(scbBd, 71, 85, 105)) // #475569
                            b.BorderBrush = ThemeManager.BrushLightControlBorder;
                    }
                }
                else if (obj is GridSplitter gs)
                {
                    gs.Background = ThemeManager.BrushLightDivider;
                }
                else if (obj is Button btn)
                {
                    if (darkBg is SolidColorBrush scbBtnBg)
                    {
                        if (IsColor(scbBtnBg, 30, 41, 59) || IsColor(scbBtnBg, 51, 65, 85)) // #1E293B or #334155
                        {
                            btn.Background = ThemeManager.BrushLightSubCardBg;
                            btn.BorderBrush = ThemeManager.BrushLightControlBorder;
                            btn.Foreground = ThemeManager.BrushLightControlFg;
                        }
                    }
                }
                else if (obj is TextBlock tb)
                {
                    if (darkFg is SolidColorBrush scbFg)
                    {
                        if (IsColor(scbFg, 248, 250, 252) || IsColor(scbFg, 226, 232, 240) || IsColor(scbFg, 241, 245, 249)) // #F8FAFC / #E2E8F0
                            tb.Foreground = ThemeManager.BrushLightTextPrimary;
                        else if (IsColor(scbFg, 203, 213, 225)) // #CBD5E1
                            tb.Foreground = ThemeManager.BrushLightTextSecondary;
                        else if (IsColor(scbFg, 148, 163, 184) || IsColor(scbFg, 100, 116, 139)) // #94A3B8 / #64748B
                            tb.Foreground = ThemeManager.BrushLightTextMuted;
                        else if (IsColor(scbFg, 56, 189, 248)) // #38BDF8
                            tb.Foreground = ThemeManager.BrushSkyAccentDark;
                        else if (IsColor(scbFg, 52, 211, 153)) // #34D399
                            tb.Foreground = ThemeManager.BrushEmeraldAccent;
                    }
                }
                else if (obj is TextBox txtBox)
                {
                    txtBox.Foreground = ThemeManager.BrushLightTextPrimary;
                }
            }
            else
            {
                // Restoring DARK mode
                if (obj is Border b)
                {
                    b.Background = darkBg;
                    b.BorderBrush = darkBorder;
                }
                else if (obj is GridSplitter gs)
                {
                    gs.Background = darkBg;
                }
                else if (obj is Button btn)
                {
                    btn.Background = darkBg;
                    btn.BorderBrush = darkBorder;
                    btn.Foreground = darkFg;
                }
                else if (obj is TextBlock tb)
                {
                    tb.Foreground = darkFg;
                }
                else if (obj is TextBox txtBox)
                {
                    txtBox.Foreground = darkFg;
                }
            }

            int childCount = VisualTreeHelper.GetChildrenCount(obj);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                if (child != null)
                {
                    ApplyThemeToTree(child, isDark);
                }
            }
        }

        private static bool IsColor(SolidColorBrush brush, byte r, byte g, byte b)
        {
            return brush.Color.R == r && brush.Color.G == g && brush.Color.B == b;
        }
    }
}
