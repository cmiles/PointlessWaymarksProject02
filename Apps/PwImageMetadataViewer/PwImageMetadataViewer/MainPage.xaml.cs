#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PwImageMetadataViewer.Models;
using PwImageMetadataViewer.Services;

namespace PwImageMetadataViewer
{
    public partial class MainPage : Page
    {
        private List<MetadataItem> _allTags = new List<MetadataItem>();
        private string _activeGroupFilter = "All";
        private ImageMetadataReport? _currentReport;
        private IDisposable? _jsCallbackHandle;

        private bool? _isSingleColumn;
        private bool? _isSmallScreen;

        public ViewModels.UpdateViewModel UpdateVm { get; } = new();

        public MainPage()
        {
            this.InitializeComponent();
            this.Loaded += MainPage_Loaded;
            this.SizeChanged += MainPage_SizeChanged;
            this.MouseWheel += MainPage_MouseWheel;
            this.MapContainerBorder.SizeChanged += (s, e) => InvalidateMapSize();
            this.MapContainerBorder.Loaded += (s, e) => InvalidateMapSize();

            // Configures OpenSilver routed MouseWheel and touch pan isolation:
            // 1. Sets AllowScrollOnTouchMove = false so OpenSilver PanHelper does not pan the ScrollViewer
            // 2. Marks e.Handled = true on MouseWheel so trackpad two-finger scroll does not scroll the app
            LeafletScrollIsolationHelper.ProtectControl(MapContainerBorder);

            AboutWindow.CloseRequested += (s, e) => AboutOverlayGrid.Visibility = Visibility.Collapsed;
            AboutOverlayGrid.MouseLeftButtonDown += (s, e) =>
            {
                if (e.OriginalSource == AboutOverlayGrid)
                {
                    AboutOverlayGrid.Visibility = Visibility.Collapsed;
                }
            };

            UpdateWindowControlElement.DataContext = UpdateVm;
            UpdateVm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ViewModels.UpdateViewModel.IsUpdateWindowOpen))
                {
                    UpdateWindowOverlayGrid.Visibility = UpdateVm.IsUpdateWindowOpen ? Visibility.Visible : Visibility.Collapsed;
                }
            };
            UpdateVm.OnStatusNotification = (msg, isError) =>
            {
                if (isError) ShowError(msg);
                else ShowInfo(msg);
            };
            UpdateWindowOverlayGrid.MouseLeftButtonDown += (s, e) =>
            {
                if (e.OriginalSource == UpdateWindowOverlayGrid && !UpdateVm.IsDownloadingUpdate)
                {
                    UpdateVm.IsUpdateWindowOpen = false;
                }
            };
        }

        private void MainPage_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Handled) return;
            if (MainScrollViewer != null)
            {
                double currentOffset = MainScrollViewer.VerticalOffset;
                double newOffset = Math.Max(0, currentOffset - e.Delta);
                MainScrollViewer.ScrollToVerticalOffset(newOffset);
                e.Handled = true;
            }
        }

        private void MainPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateResponsiveLayout(e.NewSize.Width);
            InvalidateMapSize();
        }

        private void UpdateResponsiveLayout(double width)
        {
            if (width <= 0) return;
            UpdateHeroLayout(width);
            UpdateSmallScreenLayout(width);
        }

        private void UpdateSmallScreenLayout(double width)
        {
            bool wantSmallScreen = width < 850;
            if (_isSmallScreen.HasValue && _isSmallScreen.Value == wantSmallScreen)
                return;

            _isSmallScreen = wantSmallScreen;

            // Header responsiveness: Collapse full banner to compact title + hamburger on small screens
            if (PnlWideHeader != null)
                PnlWideHeader.Visibility = wantSmallScreen ? Visibility.Collapsed : Visibility.Visible;

            if (PnlSmallHeader != null)
                PnlSmallHeader.Visibility = wantSmallScreen ? Visibility.Visible : Visibility.Collapsed;

            if (!wantSmallScreen)
            {
                CloseHamburgerMenu();
            }

            if (TopHeaderBorder != null)
                TopHeaderBorder.Padding = wantSmallScreen ? new Thickness(12, 8, 12, 8) : new Thickness(18, 12, 18, 12);

            // Container margins: Reduce left/right margins on small screens to maximize usable width
            if (MainContentStackPanel != null)
                MainContentStackPanel.Margin = wantSmallScreen ? new Thickness(8, 12, 8, 20) : new Thickness(24, 20, 24, 32);

            if (TagsSectionBorder != null)
                TagsSectionBorder.Padding = wantSmallScreen ? new Thickness(12, 14, 12, 14) : new Thickness(20);

            // Tags section responsiveness
            if (TableHeaderBorder != null)
                TableHeaderBorder.Visibility = wantSmallScreen ? Visibility.Collapsed : Visibility.Visible;

            if (PnlCategoryChips != null)
                PnlCategoryChips.Visibility = wantSmallScreen ? Visibility.Collapsed : Visibility.Visible;

            if (PnlCategoryMenu != null)
                PnlCategoryMenu.Visibility = wantSmallScreen ? Visibility.Visible : Visibility.Collapsed;

            if (TagsItemsControl != null && Resources.Contains(wantSmallScreen ? "SmallScreenTagTemplate" : "WideTagTemplate"))
            {
                TagsItemsControl.ItemTemplate = (DataTemplate)Resources[wantSmallScreen ? "SmallScreenTagTemplate" : "WideTagTemplate"];
                ApplyFilter();
            }
        }

        private void UpdateHeroLayout(double width)
        {
            if (width <= 0 || HeroSectionGrid == null || PreviewCardBorder == null || SpecsCardBorder == null || AttributionCardBorder == null)
                return;

            bool wantSingleColumn = width < 1100;
            if (_isSingleColumn.HasValue && _isSingleColumn.Value == wantSingleColumn)
                return;

            _isSingleColumn = wantSingleColumn;

            if (wantSingleColumn)
            {
                // Wrap top cards into a single vertical column
                HeroSectionGrid.ColumnDefinitions.Clear();
                HeroSectionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                HeroSectionGrid.RowDefinitions.Clear();
                HeroSectionGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                HeroSectionGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                HeroSectionGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                Grid.SetRow(PreviewCardBorder, 0);
                Grid.SetColumn(PreviewCardBorder, 0);
                PreviewCardBorder.Margin = new Thickness(0, 0, 0, 16);

                Grid.SetRow(SpecsCardBorder, 1);
                Grid.SetColumn(SpecsCardBorder, 0);
                SpecsCardBorder.Margin = new Thickness(0, 0, 0, 16);

                Grid.SetRow(AttributionCardBorder, 2);
                Grid.SetColumn(AttributionCardBorder, 0);
                AttributionCardBorder.Margin = new Thickness(0, 0, 0, 0);
            }
            else
            {
                // 3-column side-by-side layout (all 3 equal size)
                HeroSectionGrid.ColumnDefinitions.Clear();
                HeroSectionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                HeroSectionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                HeroSectionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                HeroSectionGrid.RowDefinitions.Clear();
                HeroSectionGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                Grid.SetRow(PreviewCardBorder, 0);
                Grid.SetColumn(PreviewCardBorder, 0);
                PreviewCardBorder.Margin = new Thickness(0, 0, 16, 0);

                Grid.SetRow(SpecsCardBorder, 0);
                Grid.SetColumn(SpecsCardBorder, 1);
                SpecsCardBorder.Margin = new Thickness(0, 0, 16, 0);

                Grid.SetRow(AttributionCardBorder, 0);
                Grid.SetColumn(AttributionCardBorder, 2);
                AttributionCardBorder.Margin = new Thickness(0, 0, 0, 0);
            }
        }

        private void InvalidateMapSize()
        {
            OpenSilver.Interop.ExecuteJavaScriptVoid(@"
                if (window.leafletMapInstance) {
                    window.leafletMapInstance.invalidateSize();
                } else if (window.pwExifBridge && window.pwExifBridge.invalidateMapSize) {
                    window.pwExifBridge.invalidateMapSize();
                }
            ");
        }

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateResponsiveLayout(this.ActualWidth);
                // 1. Inject Leaflet Scroll Isolation Helper script and register ScrollViewer
                string isolationScript = LeafletScrollIsolationHelper.GetJavaScriptDefinition();
                OpenSilver.Interop.ExecuteJavaScriptVoid(isolationScript);

                var scrollDiv = OpenSilver.Interop.GetDiv(MainScrollViewer);
                if (scrollDiv != null)
                {
                    OpenSilver.Interop.ExecuteJavaScriptVoid("window.pwMainScrollViewerElement = $0;", scrollDiv);
                }

                // 2. Protect Map Border against mouse wheel event bubbling and touch pan
                LeafletScrollIsolationHelper.ProtectControl(MapContainerBorder);

                // 3. Mount Leaflet container and Image Preview in DOM
                var mapDiv = OpenSilver.Interop.GetDiv(MapContainerBorder);
                if (mapDiv != null)
                {
                    OpenSilver.Interop.ExecuteJavaScriptVoid(
                        "if (window.pwExifBridge && window.pwExifBridge.ensureMapContainer) { window.pwExifBridge.ensureMapContainer($0); }",
                        mapDiv
                    );
                }

                var previewDiv = OpenSilver.Interop.GetDiv(ImagePreviewContainer);
                if (previewDiv != null)
                {
                    OpenSilver.Interop.ExecuteJavaScriptVoid(
                        "if (window.pwExifBridge && window.pwExifBridge.displayPreview) { window.pwExifBridge.displayPreview($0, null); }",
                        previewDiv
                    );
                }

                // 4. Register C# callback for metadata reports
                Action<string> onMetadataAction = OnMetadataReceived;
                _jsCallbackHandle = OpenSilver.Interop.CreateJavascriptCallback(onMetadataAction);
                OpenSilver.Interop.ExecuteJavaScriptVoid("window.pwExifBridge.registerCallback($0);", _jsCallbackHandle);

                // 5. Initial image loading: check command line argument or load default sample
                LoadInitialImage();

                // 6. Initialize ThemeManager
                ThemeManager.Current.ThemeChanged += (_, _) => ApplyTheme();
                ThemeManager.Current.Initialize();
                ApplyTheme();
                Dispatcher.BeginInvoke(() =>
                {
                    ApplyTheme();
                    _ = UpdateVm.CheckForUpdatesAsync(silent: true);
                });
            }
            catch (Exception ex)
            {
                ShowError($"Initialization error: {ex.Message}");
            }
        }

        private void LoadInitialImage()
        {
            try
            {
                // Check if Photino passed command line argument bytes
                if (App.StartupFileBytes != null && App.StartupFileBytes.Length > 0 && !string.IsNullOrEmpty(App.StartupFileName))
                {
                    string base64 = Convert.ToBase64String(App.StartupFileBytes);
                    OpenSilver.Interop.ExecuteJavaScriptVoid("window.pwExifBridge.loadFromBase64($0, $1);", base64, App.StartupFileName);
                    return;
                }

                // Check if desktop file path exists
                if (!string.IsNullOrEmpty(App.StartupFilePath) && File.Exists(App.StartupFilePath))
                {
                    byte[] bytes = File.ReadAllBytes(App.StartupFilePath);
                    string fileName = Path.GetFileName(App.StartupFilePath);
                    string base64 = Convert.ToBase64String(bytes);
                    OpenSilver.Interop.ExecuteJavaScriptVoid("window.pwExifBridge.loadFromBase64($0, $1);", base64, fileName);
                    return;
                }

                // Default: Load sample Grandview Trail image
                OpenSilver.Interop.ExecuteJavaScriptVoid("window.pwExifBridge.loadSample();");
            }
            catch (Exception ex)
            {
                ShowError($"Could not load initial image: {ex.Message}");
            }
        }

        private void OnMetadataReceived(string jsonString)
        {
            if (string.IsNullOrWhiteSpace(jsonString)) return;

            try
            {
                var options = new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true,
                    Converters = { new FlexibleStringConverter() }
                };
                var report = JsonSerializer.Deserialize<ImageMetadataReport>(jsonString, options);

                if (report == null)
                {
                    ShowError("Failed to parse metadata response from ExifTool.");
                    return;
                }

                _currentReport = report;

                if (!report.Success)
                {
                    ShowError(string.IsNullOrEmpty(report.Error) ? "Unsupported File Format: ExifTool could not extract metadata from this file." : report.Error);
                    ResetUiToError(report.FileName);
                    return;
                }

                // Successfully loaded
                HideError();
                PopulateUi(report);
            }
            catch (Exception ex)
            {
                ShowError($"Error processing metadata: {ex.Message}");
            }
        }

        private void PopulateUi(ImageMetadataReport report)
        {
            // Title & File info
            TxtCurrentFileName.Text = report.FileName;
            if (TxtMobileCurrentFileName != null)
            {
                TxtMobileCurrentFileName.Text = report.FileName;
            }
            TxtDimensionBadge.Text = (!string.IsNullOrEmpty(report.Width) && !string.IsNullOrEmpty(report.Height)) ? $"{report.Width} × {report.Height}" : "-- × --";
            TxtMegapixelBadge.Text = !string.IsNullOrEmpty(report.Megapixels) ? $"{report.Megapixels} MP" : "-- MP";
            TxtFileTypeBadge.Text = !string.IsNullOrEmpty(report.FileType) ? report.FileType : "IMAGE";

            // Camera & Specs
            string cameraMakeModel = $"{report.Make} {report.Model}".Trim();
            TxtCameraModel.Text = !string.IsNullOrEmpty(cameraMakeModel) ? cameraMakeModel : "Unknown Camera";
            TxtLensModel.Text = !string.IsNullOrEmpty(report.Lens) ? report.Lens : "Standard / Unknown Lens";
            TxtDateTimeTaken.Text = !string.IsNullOrEmpty(report.DateTime) ? report.DateTime : "No Date Recorded";

            // Exposure specs
            TxtShutter.Text = !string.IsNullOrEmpty(report.ExposureTime) ? $"{report.ExposureTime}s" : "--";
            TxtAperture.Text = !string.IsNullOrEmpty(report.FNumber) ? $"f/{report.FNumber}" : "--";
            TxtIso.Text = !string.IsNullOrEmpty(report.Iso) ? report.Iso : "--";
            TxtFocalLength.Text = FormatFocalLength(report.FocalLength);

            TxtFileSize.Text = report.FileSizeFormatted;
            TxtMimeType.Text = !string.IsNullOrEmpty(report.MimeType) ? report.MimeType : "image/*";

            // Attribution & Copyright
            TxtArtist.Text = GetTagValue(report.Tags, report.Artist, "Artist", "Creator", "By-line", "OwnerName", "Author");
            TxtCopyright.Text = GetTagValue(report.Tags, report.Copyright, "Copyright", "Rights", "CopyrightNotice");
            TxtCredit.Text = GetTagValue(report.Tags, report.Credit, "Credit", "Source");
            TxtCaption.Text = GetTagValue(report.Tags, report.Caption, "ImageDescription", "Description", "Caption-Abstract", "Title", "ObjectName", "Headline");
            TxtKeywords.Text = GetTagValue(report.Tags, report.TagsOrKeywords, "Keywords", "Subject", "HierarchicalSubject", "XPKeywords");

            // Preview Image - Rendered reliably via DOM mount to support RAW blobs & data URIs
            // (ImgPreview is kept collapsed to avoid OpenSilver prepending resources/ causing ERR_FILE_NOT_FOUND)
            TxtPreviewPlaceholder.Visibility = Visibility.Collapsed;
            ImgPreview.Visibility = Visibility.Collapsed;
            ImgPreview.Source = null;

            var previewDiv = OpenSilver.Interop.GetDiv(ImagePreviewContainer);
            if (!string.IsNullOrEmpty(report.PreviewUrl))
            {
                OpenSilver.Interop.ExecuteJavaScriptVoid(
                    "if (window.pwExifBridge && window.pwExifBridge.displayPreview) { window.pwExifBridge.displayPreview($0, $1); }",
                    previewDiv,
                    report.PreviewUrl
                );
            }
            else
            {
                OpenSilver.Interop.ExecuteJavaScriptVoid(
                    "if (window.pwExifBridge && window.pwExifBridge.displayPreview) { window.pwExifBridge.displayPreview($0, null); }",
                    previewDiv
                );
                TxtPreviewPlaceholder.Text = "No Preview Available";
                TxtPreviewPlaceholder.Visibility = Visibility.Visible;
            }

            // Location & Map
            if (report.HasGps && report.Latitude.HasValue && report.Longitude.HasValue)
            {
                GpsCardBorder.Visibility = Visibility.Visible;
                TxtCoordinatesSummary.Text = $"{report.LatitudeFormatted}\n{report.LongitudeFormatted}";
                TxtAltitudeSummary.Text = !string.IsNullOrEmpty(report.AltitudeFormatted) ? $"Alt: {report.AltitudeFormatted}" : "";
                TxtAltitudeSummary.Visibility = Visibility.Visible;
                PnlNoGpsPlaceholder.Visibility = Visibility.Collapsed;
                PnlMapActions.Visibility = Visibility.Visible;

                var mapDiv = OpenSilver.Interop.GetDiv(MapContainerBorder);
                string popupLabel = $"{report.Make} {report.Model}".Trim();
                if (string.IsNullOrEmpty(popupLabel)) popupLabel = report.FileName;
                string popupHtml = $"<b>{popupLabel}</b><br/>{report.LatitudeFormatted}<br/>{report.LongitudeFormatted}";
                if (!string.IsNullOrEmpty(report.AltitudeFormatted))
                {
                    popupHtml += $"<br/>Alt: {report.AltitudeFormatted}";
                }

                OpenSilver.Interop.ExecuteJavaScriptVoid(
                    "if (window.pwExifBridge && window.pwExifBridge.renderMap) { window.pwExifBridge.renderMap($0, $1, $2, $3); }",
                    mapDiv,
                    report.Latitude.Value,
                    report.Longitude.Value,
                    popupHtml
                );
            }
            else
            {
                GpsCardBorder.Visibility = Visibility.Collapsed;
                TxtCoordinatesSummary.Text = "No GPS Data";
                TxtAltitudeSummary.Text = "";
                TxtAltitudeSummary.Visibility = Visibility.Collapsed;
                PnlNoGpsPlaceholder.Visibility = Visibility.Collapsed;
                PnlMapActions.Visibility = Visibility.Collapsed;

                var mapDiv = OpenSilver.Interop.GetDiv(MapContainerBorder);
                OpenSilver.Interop.ExecuteJavaScriptVoid(
                    "if (window.pwExifBridge && window.pwExifBridge.hideMap) { window.pwExifBridge.hideMap($0); }",
                    mapDiv
                );
            }

            // Tags List
            _allTags = report.Tags ?? new List<MetadataItem>();
            ApplyFilter();
        }

        private void ResetUiToError(string fileName)
        {
            TxtCurrentFileName.Text = !string.IsNullOrEmpty(fileName) ? fileName : "Unknown File";
            if (TxtMobileCurrentFileName != null)
            {
                TxtMobileCurrentFileName.Text = !string.IsNullOrEmpty(fileName) ? fileName : "Unknown File";
            }
            TxtDimensionBadge.Text = "-- × --";
            TxtMegapixelBadge.Text = "-- MP";
            TxtFileTypeBadge.Text = "UNSUPPORTED";

            TxtCameraModel.Text = "--";
            TxtLensModel.Text = "--";
            TxtDateTimeTaken.Text = "--";
            TxtShutter.Text = "--";
            TxtAperture.Text = "--";
            TxtIso.Text = "--";
            TxtFocalLength.Text = "--";
            TxtFileSize.Text = "--";
            TxtMimeType.Text = "--";

            TxtArtist.Text = "--";
            TxtCopyright.Text = "--";
            TxtCredit.Text = "--";
            TxtCaption.Text = "--";
            TxtKeywords.Text = "--";

            TxtPreviewPlaceholder.Text = "⚠️ Format Not Supported";
            TxtPreviewPlaceholder.Visibility = Visibility.Visible;
            ImgPreview.Visibility = Visibility.Collapsed;
            ImgPreview.Source = null;
            var errPreviewDiv = OpenSilver.Interop.GetDiv(ImagePreviewContainer);
            OpenSilver.Interop.ExecuteJavaScriptVoid(
                "if (window.pwExifBridge && window.pwExifBridge.displayPreview) { window.pwExifBridge.displayPreview($0, null); }",
                errPreviewDiv
            );

            GpsCardBorder.Visibility = Visibility.Collapsed;
            TxtCoordinatesSummary.Text = "No GPS Data";
            TxtAltitudeSummary.Text = "";
            PnlNoGpsPlaceholder.Visibility = Visibility.Collapsed;
            PnlMapActions.Visibility = Visibility.Collapsed;
            var errMapDiv = OpenSilver.Interop.GetDiv(MapContainerBorder);
            OpenSilver.Interop.ExecuteJavaScriptVoid(
                "if (window.pwExifBridge && window.pwExifBridge.hideMap) { window.pwExifBridge.hideMap($0); }",
                errMapDiv
            );

            _allTags.Clear();
            ApplyFilter();
        }

        private static string GetTagValue(List<MetadataItem>? tags, string directValue, params string[] tagNames)
        {
            if (!string.IsNullOrWhiteSpace(directValue))
                return directValue.Trim();

            if (tags == null || tags.Count == 0)
                return "--";

            foreach (var name in tagNames)
            {
                var match = tags.FirstOrDefault(t =>
                    string.Equals(t.Tag, name, StringComparison.OrdinalIgnoreCase) ||
                    (t.RawKey != null && t.RawKey.EndsWith(":" + name, StringComparison.OrdinalIgnoreCase)));
                if (match != null && !string.IsNullOrWhiteSpace(match.Value))
                    return match.Value.Trim();
            }

            return "--";
        }

        private void ApplyFilter()
        {
            string query = (TxtSearchQuery?.Text ?? string.Empty).Trim().ToLowerInvariant();
            var filtered = _allTags.Where(item =>
            {
                // Group match
                if (_activeGroupFilter != "All")
                {
                    if (_activeGroupFilter == "EXIF")
                    {
                        if (!item.Group.Equals("EXIF", StringComparison.OrdinalIgnoreCase) &&
                            !item.Group.Equals("IFD0", StringComparison.OrdinalIgnoreCase) &&
                            !item.Group.Equals("ExifIFD", StringComparison.OrdinalIgnoreCase))
                            return false;
                    }
                    else if (_activeGroupFilter == "File")
                    {
                        if (!item.Group.Equals("File", StringComparison.OrdinalIgnoreCase) &&
                            !item.Group.Equals("System", StringComparison.OrdinalIgnoreCase))
                            return false;
                    }
                    else if (_activeGroupFilter == "GPS")
                    {
                        if (!item.Group.Equals("GPS", StringComparison.OrdinalIgnoreCase))
                            return false;
                    }
                    else if (_activeGroupFilter == "Composite")
                    {
                        if (!item.Group.Equals("Composite", StringComparison.OrdinalIgnoreCase))
                            return false;
                    }
                    else if (_activeGroupFilter == "MakerNotes")
                    {
                        if (!item.Group.Equals("MakerNotes", StringComparison.OrdinalIgnoreCase))
                            return false;
                    }
                    else if (_activeGroupFilter == "XMP")
                    {
                        if (!item.Group.StartsWith("XMP", StringComparison.OrdinalIgnoreCase) &&
                            !item.Group.StartsWith("IPTC", StringComparison.OrdinalIgnoreCase))
                            return false;
                    }
                    else
                    {
                        if (!item.Group.Equals(_activeGroupFilter, StringComparison.OrdinalIgnoreCase))
                            return false;
                    }
                }

                // Query match
                if (!string.IsNullOrEmpty(query))
                {
                    return item.Tag.ToLowerInvariant().Contains(query) ||
                           item.Value.ToLowerInvariant().Contains(query) ||
                           item.Group.ToLowerInvariant().Contains(query);
                }

                return true;
            }).ToList();

            TagsItemsControl.ItemsSource = filtered;
            TxtTagCount.Text = $"{filtered.Count} of {_allTags.Count} tags";

            Dispatcher.BeginInvoke(() =>
            {
                if (TagsItemsControl != null)
                {
                    ApplyThemeToTree(TagsItemsControl, ThemeManager.Current.IsDark);
                }
            });
        }

        private void ShowInfo(string message)
        {
            TxtStatusIcon.Text = "ℹ️ ";
            TxtStatusMessage.Text = message;
            StatusBannerBorder.Background = new SolidColorBrush(Color.FromArgb(255, 12, 74, 110)); // sky-900
            StatusBannerBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 2, 132, 199)); // sky-600
            StatusBannerBorder.Visibility = Visibility.Visible;
            if (BtnCopyError != null)
            {
                BtnCopyError.Visibility = Visibility.Collapsed;
            }
        }

        private void ShowError(string message)
        {
            TxtStatusIcon.Text = "⚠️ ";
            TxtStatusMessage.Text = message;
            StatusBannerBorder.Background = new SolidColorBrush(Color.FromArgb(255, 127, 29, 29)); // dark red
            StatusBannerBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 239, 68, 68)); // red-500
            StatusBannerBorder.Visibility = Visibility.Visible;
            if (BtnCopyError != null)
            {
                BtnCopyError.Visibility = Visibility.Visible;
                if (BtnCopyErrorText != null)
                {
                    BtnCopyErrorText.Text = "Copy Error";
                }
            }
        }

        private void HideError()
        {
            StatusBannerBorder.Visibility = Visibility.Collapsed;
            if (BtnCopyError != null)
            {
                BtnCopyError.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnCopyError_Click(object sender, RoutedEventArgs e)
        {
            string msg = TxtStatusMessage?.Text ?? string.Empty;
            if (!string.IsNullOrEmpty(msg))
            {
                OpenSilver.Interop.ExecuteJavaScriptVoid(
                    @"if (navigator.clipboard) {
                        navigator.clipboard.writeText($0);
                    }",
                    msg
                );

                if (BtnCopyErrorText != null)
                {
                    BtnCopyErrorText.Text = "✅ Copied!";
                    var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                    timer.Tick += (s, args) =>
                    {
                        timer.Stop();
                        if (BtnCopyErrorText != null)
                        {
                            BtnCopyErrorText.Text = "Copy Error";
                        }
                    };
                    timer.Start();
                }
            }
        }

        private void BtnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            OpenSilver.Interop.ExecuteJavaScriptVoid("window.pwExifBridge.openFileDialog();");
        }

        private void BtnHamburgerMenu_Click(object sender, RoutedEventArgs e)
        {
            if (PnlHamburgerDropdown == null) return;
            bool isExpanded = PnlHamburgerDropdown.Visibility == Visibility.Visible;
            PnlHamburgerDropdown.Visibility = isExpanded ? Visibility.Collapsed : Visibility.Visible;
            if (TxtHamburgerIcon != null)
            {
                TxtHamburgerIcon.Text = isExpanded ? "☰" : "✕";
            }
        }

        private void CloseHamburgerMenu()
        {
            if (PnlHamburgerDropdown != null)
                PnlHamburgerDropdown.Visibility = Visibility.Collapsed;
            if (TxtHamburgerIcon != null)
                TxtHamburgerIcon.Text = "☰";
        }

        private void BtnMobileOpenFile_Click(object sender, RoutedEventArgs e)
        {
            CloseHamburgerMenu();
            BtnOpenFile_Click(sender, e);
        }

        private void BtnMobileExportJson_Click(object sender, RoutedEventArgs e)
        {
            CloseHamburgerMenu();
            BtnExportJson_Click(sender, e);
        }

        private void BtnMobileAbout_Click(object sender, RoutedEventArgs e)
        {
            CloseHamburgerMenu();
            BtnAbout_Click(sender, e);
        }

        private void BtnAbout_Click(object sender, RoutedEventArgs e)
        {
            AboutOverlayGrid.Visibility = Visibility.Visible;
        }

        private void BtnUpdates_Click(object sender, RoutedEventArgs e)
        {
            _ = UpdateVm.CheckForUpdatesAsync(silent: false);
        }

        private void BtnMobileUpdates_Click(object sender, RoutedEventArgs e)
        {
            CloseHamburgerMenu();
            _ = UpdateVm.CheckForUpdatesAsync(silent: false);
        }

        private static string FormatFocalLength(string? rawFocalLength)
        {
            if (string.IsNullOrWhiteSpace(rawFocalLength)) return "--";
            string trimmed = rawFocalLength.Trim();

            // Match numbers with optional units, e.g. "50.0 mm", "50.0mm", "50 mm", "50.0", "50.5 mm"
            var match = System.Text.RegularExpressions.Regex.Match(trimmed, @"^([0-9]+(?:\.[0-9]+)?)\s*(mm)?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success)
            {
                if (double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double val))
                {
                    // Only format digits beyond the decimal if there are non-zero values
                    string formattedNum = (val % 1 == 0) ? ((long)val).ToString() : val.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                    return $"{formattedNum} mm";
                }
            }
            return trimmed;
        }

        private void BtnLoadSample_Click(object sender, RoutedEventArgs e)
        {
            OpenSilver.Interop.ExecuteJavaScriptVoid("window.pwExifBridge.loadSample();");
        }

        private void BtnExportJson_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenSilver.Interop.ExecuteJavaScriptVoid(
                    "var json = window.pwExifBridge.getCurrentMetadataJson(); if (navigator.clipboard) { navigator.clipboard.writeText(json).then(() => alert('Metadata JSON copied to clipboard!')); } else { alert('Clipboard not available'); }"
                );
            }
            catch (Exception ex)
            {
                ShowError($"Failed to copy JSON: {ex.Message}");
            }
        }

        private void BtnOpenOsm_Click(object sender, RoutedEventArgs e)
        {
            OpenSilver.Interop.ExecuteJavaScriptVoid("window.pwExifBridge.openOpenStreetMap();");
        }

        private void BtnOpenPeakFinder_Click(object sender, RoutedEventArgs e)
        {
            OpenSilver.Interop.ExecuteJavaScriptVoid("window.pwExifBridge.openPeakFinder();");
        }

        private void TxtSearchQuery_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void BtnFilter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string filterTag)
            {
                _activeGroupFilter = filterTag;
                UpdateFilterButtonsVisuals();
                ApplyFilter();
            }
        }

        private void BtnCategoryMenuToggle_Click(object sender, RoutedEventArgs e)
        {
            if (PnlCategoryDropdownList == null) return;
            bool isExpanded = PnlCategoryDropdownList.Visibility == Visibility.Visible;
            PnlCategoryDropdownList.Visibility = isExpanded ? Visibility.Collapsed : Visibility.Visible;
            if (TxtCategoryMenuChevron != null)
            {
                TxtCategoryMenuChevron.Text = isExpanded ? "▼" : "▲";
            }
        }

        private void BtnMenuCategory_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string filterTag)
            {
                _activeGroupFilter = filterTag;
                if (PnlCategoryDropdownList != null)
                    PnlCategoryDropdownList.Visibility = Visibility.Collapsed;
                if (TxtCategoryMenuChevron != null)
                    TxtCategoryMenuChevron.Text = "▼";

                UpdateFilterButtonsVisuals();
                ApplyFilter();
            }
        }

        private void UpdateFilterButtonsVisuals()
        {
            bool isDark = ThemeManager.Current.IsDark;
            var activeBg = isDark ? new SolidColorBrush(Color.FromArgb(255, 56, 189, 248)) : new SolidColorBrush(Color.FromArgb(255, 2, 132, 199)); // #38BDF8 / #0284C7
            var activeFg = isDark ? new SolidColorBrush(Color.FromArgb(255, 15, 23, 42)) : new SolidColorBrush(Colors.White);                       // #0F172A / White
            var inactiveBg = isDark ? new SolidColorBrush(Color.FromArgb(255, 30, 41, 59)) : ThemeManager.BrushLightSubCardBg;                       // #1E293B / #F1F5F9
            var inactiveFg = isDark ? new SolidColorBrush(Color.FromArgb(255, 248, 250, 252)) : ThemeManager.BrushLightTextPrimary;                  // #F8FAFC / #0F172A

            SetButtonActive(BtnFilterAll, _activeGroupFilter == "All", activeBg, activeFg, inactiveBg, inactiveFg);
            SetButtonActive(BtnFilterExif, _activeGroupFilter == "EXIF", activeBg, activeFg, inactiveBg, inactiveFg);
            SetButtonActive(BtnFilterFile, _activeGroupFilter == "File", activeBg, activeFg, inactiveBg, inactiveFg);
            SetButtonActive(BtnFilterGps, _activeGroupFilter == "GPS", activeBg, activeFg, inactiveBg, inactiveFg);
            SetButtonActive(BtnFilterComposite, _activeGroupFilter == "Composite", activeBg, activeFg, inactiveBg, inactiveFg);
            SetButtonActive(BtnFilterMakerNotes, _activeGroupFilter == "MakerNotes", activeBg, activeFg, inactiveBg, inactiveFg);
            SetButtonActive(BtnFilterXmp, _activeGroupFilter == "XMP", activeBg, activeFg, inactiveBg, inactiveFg);

            string label = _activeGroupFilter switch
            {
                "EXIF" => "Camera / EXIF",
                "File" => "File & Image",
                "GPS" => "GPS / Location",
                "Composite" => "Composite",
                "MakerNotes" => "MakerNotes",
                "XMP" => "XMP / IPTC",
                _ => "All Tags"
            };
            if (TxtSelectedCategoryLabel != null)
            {
                TxtSelectedCategoryLabel.Text = label;
            }

            // Sync menu buttons highlighting
            var menuButtons = new[] { BtnMenuAll, BtnMenuExif, BtnMenuFile, BtnMenuGps, BtnMenuComposite, BtnMenuMakerNotes, BtnMenuXmp };
            foreach (var mBtn in menuButtons)
            {
                if (mBtn == null) continue;
                bool isSelected = (mBtn.Tag as string) == _activeGroupFilter;
                mBtn.Background = isSelected 
                    ? (isDark ? new SolidColorBrush(Color.FromArgb(255, 30, 41, 59)) : new SolidColorBrush(Color.FromArgb(255, 226, 232, 240)))
                    : (isDark ? new SolidColorBrush(Color.FromArgb(255, 15, 23, 42)) : new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)));
                if (mBtn.Content is TextBlock tb)
                {
                    tb.Foreground = isSelected 
                        ? (isDark ? new SolidColorBrush(Color.FromArgb(255, 56, 189, 248)) : new SolidColorBrush(Color.FromArgb(255, 2, 132, 199)))
                        : (isDark ? new SolidColorBrush(Color.FromArgb(255, 248, 250, 252)) : ThemeManager.BrushLightTextPrimary);
                    tb.FontWeight = isSelected ? FontWeights.Bold : FontWeights.Normal;
                }
            }
        }

        private void SetButtonActive(Button btn, bool isActive, Brush activeBg, Brush activeFg, Brush inactiveBg, Brush inactiveFg)
        {
            if (btn == null) return;
            btn.Background = isActive ? activeBg : inactiveBg;
            btn.Foreground = isActive ? activeFg : inactiveFg;
            btn.BorderBrush = isActive ? activeBg : (ThemeManager.Current.IsDark ? new SolidColorBrush(Color.FromArgb(255, 71, 85, 105)) : ThemeManager.BrushLightControlBorder);
            UpdateChildTextBlocks(btn.Content as DependencyObject, isActive ? activeFg : inactiveFg);
            if (btn.Content is TextBlock tb)
            {
                tb.FontWeight = isActive ? FontWeights.Bold : FontWeights.SemiBold;
            }
        }

        private readonly Dictionary<DependencyObject, (Brush? Background, Brush? BorderBrush, Brush? Foreground)> _originalDarkBrushes = new();

        private void BtnThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            ThemeManager.Current.CycleTheme();
        }

        private void BtnMobileThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            ThemeManager.Current.CycleTheme();
        }

        private void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            if (sender is HyperlinkButton btn && btn.NavigateUri != null)
            {
                try
                {
                    OpenSilver.Interop.ExecuteJavaScriptVoid($"window.open('{btn.NavigateUri.AbsoluteUri}', '_blank');");
                }
                catch { }
            }
        }

        private void ApplyTheme()
        {
            if (TxtThemeToggle != null)
            {
                TxtThemeToggle.Text = ThemeManager.Current.ThemeButtonLabel;
            }
            if (TxtMobileThemeToggle != null)
            {
                TxtMobileThemeToggle.Text = ThemeManager.Current.ThemeButtonLabel;
            }

            bool isDark = ThemeManager.Current.IsDark;

            this.Background = ThemeManager.Current.PageBackground;

            if (RootLayoutGrid != null)
            {
                RootLayoutGrid.Background = ThemeManager.Current.PageBackground;
            }

            if (MainScrollViewer != null)
            {
                MainScrollViewer.Background = ThemeManager.Current.PageBackground;
            }

            if (TopHeaderBorder != null)
            {
                TopHeaderBorder.Background = ThemeManager.Current.HeaderBackground;
                TopHeaderBorder.BorderBrush = isDark ? ThemeManager.BrushDarkDivider : ThemeManager.BrushLightDivider;
            }

            if (LnkAuthor != null)
            {
                LnkAuthor.Foreground = isDark ? ThemeManager.BrushSkyAccentDark : ThemeManager.BrushSkyAccentLight;
            }
            if (TxtSmallAuthor != null)
            {
                TxtSmallAuthor.Foreground = isDark ? ThemeManager.BrushSkyAccentDark : ThemeManager.BrushSkyAccentLight;
            }

            var root = (DependencyObject?)RootLayoutGrid ?? this.Content as DependencyObject ?? this;
            ApplyThemeToTree(root, isDark);
            if (_allTags != null)
            {
                foreach (var tag in _allTags)
                {
                    tag.RefreshTheme();
                }
            }
            UpdateFilterButtonsVisuals();
            UpdateWindowControlElement?.ApplyTheme(isDark);
        }

        private void ApplyThemeToButton(Button btn, bool isDark)
        {
            if (btn == BtnUpdates || btn == BtnMobileUpdates)
            {
                return;
            }
            // Filter buttons and dropdown menu buttons are fully managed by UpdateFilterButtonsVisuals()
            if (btn == BtnFilterAll || btn == BtnFilterExif || btn == BtnFilterFile ||
                btn == BtnFilterGps || btn == BtnFilterComposite || btn == BtnFilterMakerNotes ||
                btn == BtnFilterXmp || btn == BtnMenuAll || btn == BtnMenuExif ||
                btn == BtnMenuFile || btn == BtnMenuGps || btn == BtnMenuComposite ||
                btn == BtnMenuMakerNotes || btn == BtnMenuXmp)
            {
                return;
            }

            if (btn == BtnOpenFile || btn == BtnMobileOpenFile)
            {
                btn.Background = isDark ? ThemeManager.BrushSkyAccentDark : ThemeManager.BrushSkyAccentLight;
                btn.Foreground = isDark ? new SolidColorBrush(Color.FromArgb(255, 15, 23, 42)) : new SolidColorBrush(Colors.White);
                UpdateChildTextBlocks(btn.Content as DependencyObject, btn.Foreground);
                return;
            }

            if (btn == BtnOpenOsm)
            {
                btn.Background = isDark ? ThemeManager.BrushDarkControlBg : ThemeManager.BrushLightSubCardBg;
                btn.BorderBrush = isDark ? ThemeManager.BrushSkyAccentDark : ThemeManager.BrushSkyAccentLight;
                btn.Foreground = isDark ? ThemeManager.BrushSkyAccentDark : ThemeManager.BrushSkyAccentLight;
                UpdateChildTextBlocks(btn.Content as DependencyObject, btn.Foreground);
                return;
            }

            if (btn == BtnOpenPeakFinder)
            {
                var emerald = isDark ? new SolidColorBrush(Color.FromArgb(255, 16, 185, 129)) : new SolidColorBrush(Color.FromArgb(255, 5, 150, 105));
                btn.Background = isDark ? ThemeManager.BrushDarkControlBg : ThemeManager.BrushLightSubCardBg;
                btn.BorderBrush = emerald;
                btn.Foreground = emerald;
                UpdateChildTextBlocks(btn.Content as DependencyObject, btn.Foreground);
                return;
            }

            // Standard buttons (BtnExportJson, BtnAbout, BtnThemeToggle, BtnHamburgerMenu, BtnMobileExportJson, etc.)
            btn.Background = isDark ? ThemeManager.BrushDarkControlBg : ThemeManager.BrushLightSubCardBg;
            btn.BorderBrush = isDark ? ThemeManager.BrushDarkControlBorder : ThemeManager.BrushLightControlBorder;
            btn.Foreground = isDark ? ThemeManager.BrushDarkControlFg : ThemeManager.BrushLightControlFg;
            UpdateChildTextBlocks(btn.Content as DependencyObject, btn.Foreground);
        }

        private static void UpdateChildTextBlocks(DependencyObject? obj, Brush foreground)
        {
            if (obj == null) return;
            if (obj is TextBlock tb)
            {
                tb.Foreground = foreground;
            }
            int count = VisualTreeHelper.GetChildrenCount(obj);
            if (count > 0)
            {
                for (int i = 0; i < count; i++)
                {
                    UpdateChildTextBlocks(VisualTreeHelper.GetChild(obj, i), foreground);
                }
            }
            else if (obj is Panel panel)
            {
                foreach (UIElement child in panel.Children)
                {
                    UpdateChildTextBlocks(child, foreground);
                }
            }
            else if (obj is ContentControl cc && cc.Content is DependencyObject contentDep)
            {
                UpdateChildTextBlocks(contentDep, foreground);
            }
        }

        private void ApplyThemeToTree(DependencyObject obj, bool isDark)
        {
            // Do not traverse into Leaflet DOM container, data-bound tags control, or dialog overlays
            if (obj == MapContainerBorder || obj == TagsItemsControl || obj == AboutOverlayGrid || obj == UpdateWindowOverlayGrid)
            {
                return;
            }

            // Buttons: style directly and style their content descendants, but DO NOT recurse into control template
            if (obj is Button btn)
            {
                ApplyThemeToButton(btn, isDark);
                return;
            }

            // Explicitly styled root and header containers: do not cache or alter their backgrounds/borders, but recurse to children
            if (obj == TopHeaderBorder || obj == RootLayoutGrid || obj == MainScrollViewer)
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
                        if (IsColor(scbBg, 11, 15, 25))        // #0B0F19
                            b.Background = ThemeManager.BrushLightPageBg;
                        else if (IsColor(scbBg, 17, 24, 39))   // #111827
                            b.Background = ThemeManager.BrushLightHeaderBg;
                        else if (IsColor(scbBg, 22, 31, 48))   // #161F30
                            b.Background = ThemeManager.BrushLightCardBg;
                        else if (IsColor(scbBg, 15, 23, 42))   // #0F172A
                            b.Background = ThemeManager.BrushLightSubCardBg;
                        else if (IsColor(scbBg, 30, 41, 59))   // #1E293B
                            b.Background = ThemeManager.BrushLightSubCardBg;
                        else if (IsColor(scbBg, 11, 17, 32))   // #0B1120
                            b.Background = ThemeManager.BrushLightPageBg;
                        else if (IsColor(scbBg, 12, 74, 110))  // #0C4A6E
                            b.Background = ThemeManager.BrushLightStatusBg;
                    }

                    if (darkBorder is SolidColorBrush scbBd)
                    {
                        if (IsColor(scbBd, 31, 41, 55))        // #1F2937
                            b.BorderBrush = ThemeManager.BrushLightDivider;
                        else if (IsColor(scbBd, 39, 53, 73) || IsColor(scbBd, 51, 65, 85) || IsColor(scbBd, 30, 41, 59)) // #273549, #334155, #1E293B
                            b.BorderBrush = ThemeManager.BrushLightCardBorder;
                        else if (IsColor(scbBd, 71, 85, 105))  // #475569
                            b.BorderBrush = ThemeManager.BrushLightControlBorder;
                        else if (IsColor(scbBd, 2, 132, 199))   // #0284C7
                            b.BorderBrush = ThemeManager.BrushLightStatusBorder;
                    }
                }
                else if (obj is TextBlock tb)
                {
                    if (darkFg is SolidColorBrush scbFg)
                    {
                        if (IsColor(scbFg, 248, 250, 252) || IsColor(scbFg, 226, 232, 240)) // #F8FAFC / #E2E8F0
                            tb.Foreground = ThemeManager.BrushLightTextPrimary;
                        else if (IsColor(scbFg, 241, 245, 249)) // #F1F5F9
                            tb.Foreground = ThemeManager.BrushLightTextHeading;
                        else if (IsColor(scbFg, 203, 213, 225)) // #CBD5E1
                            tb.Foreground = ThemeManager.BrushLightTextSecondary;
                        else if (IsColor(scbFg, 148, 163, 184) || IsColor(scbFg, 100, 116, 139)) // #94A3B8 / #64748B
                            tb.Foreground = ThemeManager.BrushLightTextMuted;
                        else if (IsColor(scbFg, 56, 189, 248)) // #38BDF8
                            tb.Foreground = ThemeManager.BrushSkyAccentLight;
                        else if (IsColor(scbFg, 240, 249, 255)) // #F0F9FF
                            tb.Foreground = new SolidColorBrush(Color.FromArgb(255, 3, 105, 161)); // #0369A1
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
