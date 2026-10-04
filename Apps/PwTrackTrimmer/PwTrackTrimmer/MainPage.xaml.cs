using System.IO;
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
            this.Loaded += (s, e) =>
            {
                SetupFileDragAndDrop();
                InjectGlobalCaretStyles();
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
    }
}
