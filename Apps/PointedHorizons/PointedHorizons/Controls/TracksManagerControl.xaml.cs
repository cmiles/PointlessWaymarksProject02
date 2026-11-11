using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PointedHorizons.Models;
using PointedHorizons.Services;

namespace PointedHorizons.Controls;

public partial class TracksManagerControl : UserControl
{
    public event EventHandler<TrackFileInfo>? TrackZoomRequested;

    private string _filterQuery = string.Empty;
    private bool _isSmallScreen;
    private bool _hasSizedOnce;

    public TracksManagerControl()
    {
        InitializeComponent();
        Loaded += TracksManagerControl_Loaded;
        SizeChanged += TracksManagerControl_SizeChanged;

        ThemeManager.Current.ThemeChanged += (s, e) =>
        {
            Dispatcher.BeginInvoke(() => UpdateTheme(ThemeManager.Current.IsDark));
        };

        TrackIndexService.TracksChanged += (s, e) =>
        {
            Dispatcher.BeginInvoke(RefreshView);
        };

        TrackIndexService.ScanCompleted += (s, e) =>
        {
            Dispatcher.BeginInvoke(RefreshView);
        };

        OfflineDataService.ConfigChanged += (s, e) =>
        {
            Dispatcher.BeginInvoke(RefreshFolderDisplay);
        };

        if (TracksScrollViewer != null)
        {
            TracksScrollViewer.MouseWheel += (s, e) =>
            {
                TracksScrollViewer.ScrollToVerticalOffset(TracksScrollViewer.VerticalOffset - (e.Delta / 2.0));
                e.Handled = true;
            };
        }
        if (TracksListPanel != null)
        {
            TracksListPanel.MouseWheel += (s, e) =>
            {
                if (TracksScrollViewer != null)
                {
                    TracksScrollViewer.ScrollToVerticalOffset(TracksScrollViewer.VerticalOffset - (e.Delta / 2.0));
                    e.Handled = true;
                }
            };
        }
    }

    private void TracksManagerControl_Loaded(object sender, RoutedEventArgs e)
    {
        OfflineDataService.EnsureSettingsLoaded();
        UpdateTheme(ThemeManager.Current.IsDark);
        RefreshFolderDisplay();

        if (ActualWidth > 0 && !_hasSizedOnce)
        {
            _hasSizedOnce = true;
            _isSmallScreen = ActualWidth < 680;
            ApplyResponsiveLayout();
        }

        RefreshView();

        // Trigger refresh scan when opening to discover newly exported or copied GPX files
        if (!TrackIndexService.IsScanning)
        {
            _ = TrackIndexService.RefreshAsync(forceFullRescan: false);
        }
    }

    private void TracksManagerControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Width > 0)
        {
            bool newIsSmall = e.NewSize.Width < 680;
            if (newIsSmall != _isSmallScreen || !_hasSizedOnce)
            {
                _hasSizedOnce = true;
                _isSmallScreen = newIsSmall;
                ApplyResponsiveLayout();
                RebuildTrackCards();
            }
        }
    }

    private void ApplyResponsiveLayout()
    {
        if (RootContainer != null)
        {
            RootContainer.Margin = _isSmallScreen ? new Thickness(8, 6, 8, 6) : new Thickness(16, 12, 16, 12);
        }
        if (HeaderCard != null)
        {
            HeaderCard.Padding = _isSmallScreen ? new Thickness(10, 8, 10, 8) : new Thickness(14, 10, 14, 10);
            HeaderCard.Margin = _isSmallScreen ? new Thickness(0, 0, 0, 6) : new Thickness(0, 0, 0, 10);
        }
        if (ToolbarCard != null)
        {
            ToolbarCard.Padding = _isSmallScreen ? new Thickness(10, 8, 10, 8) : new Thickness(12, 8, 12, 8);
            ToolbarCard.Margin = _isSmallScreen ? new Thickness(0, 0, 0, 6) : new Thickness(0, 0, 0, 10);
        }
        if (ListContainerBorder != null)
        {
            ListContainerBorder.Padding = _isSmallScreen ? new Thickness(3) : new Thickness(6);
        }
        if (TxtStatsSummary != null)
        {
            TxtStatsSummary.Visibility = _isSmallScreen ? Visibility.Collapsed : Visibility.Visible;
        }
        if (TxtStatsSummaryMobile != null)
        {
            TxtStatsSummaryMobile.Visibility = _isSmallScreen ? Visibility.Visible : Visibility.Collapsed;
        }
        if (ColStatsInSearch != null)
        {
            ColStatsInSearch.Width = _isSmallScreen ? new GridLength(0) : GridLength.Auto;
        }

        if (FolderInputGrid != null && TxtGpxFolder != null && BtnBrowseFolder != null && BtnApplyFolder != null && BtnResetFolder != null)
        {
            if (_isSmallScreen)
            {
                // Small screen: Folder textbox on Row 0 spanning full width, buttons on Row 1
                Grid.SetRow(TxtGpxFolder, 0);
                Grid.SetColumn(TxtGpxFolder, 0);
                Grid.SetColumnSpan(TxtGpxFolder, 4);
                TxtGpxFolder.Margin = new Thickness(0, 0, 0, 6);

                Grid.SetRow(BtnBrowseFolder, 1);
                Grid.SetColumn(BtnBrowseFolder, 0);
                Grid.SetColumnSpan(BtnBrowseFolder, 1);
                BtnBrowseFolder.Margin = new Thickness(0, 0, 4, 0);

                Grid.SetRow(BtnApplyFolder, 1);
                Grid.SetColumn(BtnApplyFolder, 1);
                Grid.SetColumnSpan(BtnApplyFolder, 1);
                BtnApplyFolder.Margin = new Thickness(2, 0, 4, 0);

                Grid.SetRow(BtnResetFolder, 1);
                Grid.SetColumn(BtnResetFolder, 2);
                Grid.SetColumnSpan(BtnResetFolder, 1);
                BtnResetFolder.Margin = new Thickness(2, 0, 0, 0);
            }
            else
            {
                // Desktop: Single row side-by-side
                Grid.SetRow(TxtGpxFolder, 0);
                Grid.SetColumn(TxtGpxFolder, 0);
                Grid.SetColumnSpan(TxtGpxFolder, 1);
                TxtGpxFolder.Margin = new Thickness(0);

                Grid.SetRow(BtnBrowseFolder, 0);
                Grid.SetColumn(BtnBrowseFolder, 1);
                Grid.SetColumnSpan(BtnBrowseFolder, 1);
                BtnBrowseFolder.Margin = new Thickness(6, 0, 0, 0);

                Grid.SetRow(BtnApplyFolder, 0);
                Grid.SetColumn(BtnApplyFolder, 2);
                Grid.SetColumnSpan(BtnApplyFolder, 1);
                BtnApplyFolder.Margin = new Thickness(4, 0, 0, 0);

                Grid.SetRow(BtnResetFolder, 0);
                Grid.SetColumn(BtnResetFolder, 3);
                Grid.SetColumnSpan(BtnResetFolder, 1);
                BtnResetFolder.Margin = new Thickness(4, 0, 0, 0);
            }
        }
    }

    private void UpdateStatsText(string text)
    {
        if (TxtStatsSummary != null)
        {
            TxtStatsSummary.Text = text;
        }
        if (TxtStatsSummaryMobile != null)
        {
            TxtStatsSummaryMobile.Text = text;
        }
    }

    public void RefreshFolderDisplay()
    {
        if (TxtGpxFolder != null)
        {
            TxtGpxFolder.Text = OfflineDataService.GpxDirectory;
            bool canConfig = OfflineDataService.CanConfigurePaths;
            TxtGpxFolder.IsReadOnly = !canConfig;
            if (BtnResetFolder != null) BtnResetFolder.Visibility = canConfig ? Visibility.Visible : Visibility.Collapsed;
            if (BtnApplyFolder != null) BtnApplyFolder.Visibility = canConfig ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    public void RefreshView()
    {
        RefreshFolderDisplay();
        RebuildTrackCards();
    }

    private void RebuildTrackCards()
    {
        if (TracksListPanel == null) return;
        TracksListPanel.Children.Clear();

        var allTracks = TrackIndexService.Tracks;
        bool isDark = ThemeManager.Current.IsDark;

        // Apply search query filter
        var filtered = allTracks.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(_filterQuery))
        {
            string q = _filterQuery.Trim();
            filtered = filtered.Where(t =>
                (t.TrackName != null && t.TrackName.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                (t.FileName != null && t.FileName.Contains(q, StringComparison.OrdinalIgnoreCase)));
        }

        var list = filtered.ToList();
        int visibleCount = allTracks.Count(t => t.IsVisibleOnMap);
        double totalDistKm = allTracks.Sum(t => t.TotalDistanceKm);

        string statsText = TrackIndexService.IsScanning
            ? "🔄 Scanning tracks folder..."
            : $"Files: {list.Count} / {allTracks.Count} | On Map: {visibleCount} | Dist: {totalDistKm:F1} km ({totalDistKm * 0.621371:F1} mi)";
        UpdateStatsText(statsText);

        if (list.Count == 0)
        {
            var emptyText = new TextBlock
            {
                Text = allTracks.Count == 0
                    ? $"No GPX, TCX, or FIT track files found in:\n{OfflineDataService.GpxDirectory}\n\nAdd track files to this directory and click '🔄 Rescan Folder'."
                    : "No track files matched your search filter.",
                FontSize = 11.5,
                Margin = new Thickness(14, 30, 14, 30),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Foreground = isDark ? new SolidColorBrush(Color.FromArgb(255, 148, 163, 184)) : new SolidColorBrush(Color.FromArgb(255, 100, 116, 139))
            };
            TracksListPanel.Children.Add(emptyText);
            return;
        }

        foreach (var track in list)
        {
            var card = CreateTrackCard(track, isDark);
            TracksListPanel.Children.Add(card);
        }
    }

    private Border CreateTrackCard(TrackFileInfo track, bool isDark)
    {
        var textPrimaryBrush = isDark ? new SolidColorBrush(Color.FromArgb(255, 241, 245, 249)) : new SolidColorBrush(Color.FromArgb(255, 15, 23, 42));
        var textSecondaryBrush = isDark ? new SolidColorBrush(Color.FromArgb(255, 148, 163, 184)) : new SolidColorBrush(Color.FromArgb(255, 100, 116, 139));
        var statsHighlightBrush = isDark ? new SolidColorBrush(Color.FromArgb(255, 203, 213, 225)) : new SolidColorBrush(Color.FromArgb(255, 51, 65, 85));

        var card = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = _isSmallScreen ? new Thickness(10, 8, 10, 8) : new Thickness(12, 9, 12, 9),
            Margin = new Thickness(0, 0, 0, _isSmallScreen ? 6 : 8),
            Background = isDark ? new SolidColorBrush(Color.FromArgb(255, 24, 33, 47)) : new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)),
            BorderBrush = isDark ? new SolidColorBrush(Color.FromArgb(255, 51, 65, 85)) : new SolidColorBrush(Color.FromArgb(255, 226, 232, 240))
        };

        var grid = new Grid();

        // Checkbox: Map Visibility
        var chk = new CheckBox
        {
            IsChecked = track.IsVisibleOnMap,
            VerticalAlignment = _isSmallScreen ? VerticalAlignment.Top : VerticalAlignment.Center,
            Margin = _isSmallScreen ? new Thickness(0, 3, 8, 0) : new Thickness(0, 0, 10, 0)
        };
        ToolTipService.SetToolTip(chk, "Show or hide this track on the map");
        chk.Click += (s, e) =>
        {
            TrackIndexService.SetTrackVisibility(track.Id, chk.IsChecked == true);
        };

        // Track Details Vertical StackPanel
        var detailsPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = _isSmallScreen ? new Thickness(0) : new Thickness(0, 0, 12, 0)
        };

        // Row 1: Badges + Track Name (Grid with Auto and * columns so track name wraps cleanly)
        var titleGrid = new Grid
        {
            Margin = new Thickness(0, 0, 0, 3)
        };
        titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var badgesStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 1, 6, 0)
        };

        var formatTag = new Border
        {
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(5, 1, 5, 1),
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Background = GetFormatTagBrush(track.FileFormat)
        };
        var formatTagText = new TextBlock
        {
            Text = track.FileFormat.ToUpperInvariant(),
            FontSize = 9,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Colors.White)
        };
        formatTag.Child = formatTagText;
        badgesStack.Children.Add(formatTag);

        Color trackColor = ParseColorSafe(track.DisplayColor);
        var colorBadge = new Border
        {
            Width = 12,
            Height = 12,
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(0, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(trackColor),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Colors.White)
        };
        badgesStack.Children.Add(colorBadge);
        Grid.SetColumn(badgesStack, 0);
        titleGrid.Children.Add(badgesStack);

        var txtName = new TextBlock
        {
            Text = track.TrackName,
            FontWeight = FontWeights.Bold,
            FontSize = 11.5,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = textPrimaryBrush
        };
        Grid.SetColumn(txtName, 1);
        titleGrid.Children.Add(txtName);
        detailsPanel.Children.Add(titleGrid);

        // Row 2: Dedicated File Name & Size line with Wrapping
        var txtFile = new TextBlock
        {
            Text = $"📄 {track.FileName} · {track.FormattedSize}",
            FontSize = 9.5,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 4),
            Foreground = textSecondaryBrush
        };
        detailsPanel.Children.Add(txtFile);

        // Zoom button definition
        var btnZoom = new Button
        {
            Content = "🗺️ Zoom to Track",
            Style = TryFindResource("PillButtonStyle") as Style,
            FontSize = 10.5,
            FontWeight = FontWeights.SemiBold
        };
        ToolTipService.SetToolTip(btnZoom, "Zoom the main map to this track's bounds and make it visible");
        btnZoom.Click += (s, e) =>
        {
            track.IsVisibleOnMap = true;
            TrackIndexService.SetTrackVisibility(track.Id, true);
            TrackZoomRequested?.Invoke(this, track);
        };

        if (_isSmallScreen)
        {
            // Small Screen: CheckBox (Col 0) + Details (Col 1)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Grid.SetColumn(chk, 0);
            grid.Children.Add(chk);

            // Row 3: Primary stats (Distance + Elevation) with wrapping
            string primaryStats = $"📏 {track.TotalDistanceKm:F2} km ({track.TotalDistanceKm * 0.621371:F2} mi)";
            if (track.TotalClimbM > 0 || track.TotalDescentM > 0)
            {
                primaryStats += $"  ·  ⛰️ +{track.TotalClimbM:F0}m / -{track.TotalDescentM:F0}m";
            }
            var txtPrimaryStats = new TextBlock
            {
                Text = primaryStats,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 2),
                Foreground = statsHighlightBrush
            };
            detailsPanel.Children.Add(txtPrimaryStats);

            // Row 4: Secondary stats (Duration, Date, Point Count) with wrapping
            var secondaryParts = new List<string>();
            if (!string.IsNullOrEmpty(track.FormattedDuration) && track.FormattedDuration != "00:00")
            {
                secondaryParts.Add($"⏱️ {track.FormattedDuration}");
            }
            DateTime dateVal = track.StartTimeUtc ?? track.LastModifiedUtc;
            secondaryParts.Add($"📅 {dateVal.ToLocalTime():yyyy-MM-dd HH:mm}");
            secondaryParts.Add($"📍 {track.PointCount:N0} pts");

            var txtSecondaryStats = new TextBlock
            {
                Text = string.Join("  ·  ", secondaryParts),
                FontSize = 9.5,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 6),
                Foreground = textSecondaryBrush
            };
            detailsPanel.Children.Add(txtSecondaryStats);

            // Row 5: Action Button stretched across details width with touch-friendly height
            btnZoom.HorizontalAlignment = HorizontalAlignment.Stretch;
            btnZoom.MinHeight = 36;
            btnZoom.Padding = new Thickness(10, 6, 10, 6);
            btnZoom.Margin = new Thickness(0, 2, 0, 0);
            detailsPanel.Children.Add(btnZoom);

            Grid.SetColumn(detailsPanel, 1);
            grid.Children.Add(detailsPanel);
        }
        else
        {
            // Desktop: CheckBox (Col 0) + Details (Col 1) + Zoom Button (Col 2)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Grid.SetColumn(chk, 0);
            grid.Children.Add(chk);

            // Stats row with all details wrapped
            var statItems = new List<string>
            {
                $"📏 {track.TotalDistanceKm:F2} km ({track.TotalDistanceKm * 0.621371:F2} mi)"
            };
            if (track.TotalClimbM > 0 || track.TotalDescentM > 0)
            {
                statItems.Add($"⛰️ +{track.TotalClimbM:F0}m / -{track.TotalDescentM:F0}m");
            }
            if (!string.IsNullOrEmpty(track.FormattedDuration) && track.FormattedDuration != "00:00")
            {
                statItems.Add($"⏱️ {track.FormattedDuration}");
            }
            DateTime dateVal = track.StartTimeUtc ?? track.LastModifiedUtc;
            statItems.Add($"📅 {dateVal.ToLocalTime():yyyy-MM-dd HH:mm}");
            statItems.Add($"📍 {track.PointCount:N0} pts");

            var txtStats = new TextBlock
            {
                Text = string.Join("  ·  ", statItems),
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap,
                Foreground = textSecondaryBrush
            };
            detailsPanel.Children.Add(txtStats);

            Grid.SetColumn(detailsPanel, 1);
            grid.Children.Add(detailsPanel);

            btnZoom.VerticalAlignment = VerticalAlignment.Center;
            btnZoom.Padding = new Thickness(10, 5, 10, 5);
            Grid.SetColumn(btnZoom, 2);
            grid.Children.Add(btnZoom);
        }

        card.Child = grid;
        return card;
    }

    private static Brush GetFormatTagBrush(string format)
    {
        return format?.ToUpperInvariant() switch
        {
            "GPX" => new SolidColorBrush(Color.FromArgb(255, 16, 185, 129)), // Emerald green
            "TCX" => new SolidColorBrush(Color.FromArgb(255, 139, 92, 246)), // Purple
            "FIT" => new SolidColorBrush(Color.FromArgb(255, 14, 165, 233)), // Sky blue
            _ => new SolidColorBrush(Color.FromArgb(255, 100, 116, 139))
        };
    }

    private static Color ParseColorSafe(string hex)
    {
        if (string.IsNullOrEmpty(hex)) return Color.FromArgb(255, 59, 130, 246);
        hex = hex.Trim().TrimStart('#');
        if (hex.Length == 6 &&
            byte.TryParse(hex.Substring(0, 2), NumberStyles.HexNumber, null, out byte r) &&
            byte.TryParse(hex.Substring(2, 2), NumberStyles.HexNumber, null, out byte g) &&
            byte.TryParse(hex.Substring(4, 2), NumberStyles.HexNumber, null, out byte b))
        {
            return Color.FromArgb(255, r, g, b);
        }
        return Color.FromArgb(255, 59, 130, 246);
    }

    #region Event Handlers

    private void TxtSearchFilter_TextChanged(object sender, TextChangedEventArgs e)
    {
        _filterQuery = TxtSearchFilter.Text ?? string.Empty;
        RebuildTrackCards();
    }

    private async void BtnRescan_Click(object sender, RoutedEventArgs e)
    {
        if (TrackIndexService.IsScanning) return;
        UpdateStatsText("🔄 Scanning tracks folder...");
        await TrackIndexService.RefreshAsync(forceFullRescan: true);
        RebuildTrackCards();
    }

    private async void BtnBrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string current = TxtGpxFolder?.Text?.Trim() ?? string.Empty;
            string? chosen = await FolderPickerService.PickFolderAsync(current);
            if (!string.IsNullOrWhiteSpace(chosen))
            {
                if (TxtGpxFolder != null) TxtGpxFolder.Text = chosen;
                OfflineDataService.SetCustomGpxDirectory(chosen);
                await TrackIndexService.RefreshAsync(forceFullRescan: true);
                RebuildTrackCards();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TracksManager] Folder picker error: {ex.Message}");
        }
    }

    private async void BtnApplyFolder_Click(object sender, RoutedEventArgs e)
    {
        string? newDir = TxtGpxFolder?.Text?.Trim();
        if (string.IsNullOrEmpty(newDir)) return;

        OfflineDataService.SetCustomGpxDirectory(newDir);
        await TrackIndexService.RefreshAsync(forceFullRescan: true);
        RebuildTrackCards();
    }

    private async void BtnResetFolder_Click(object sender, RoutedEventArgs e)
    {
        OfflineDataService.SetCustomGpxDirectory(null);
        RefreshFolderDisplay();
        await TrackIndexService.RefreshAsync(forceFullRescan: true);
        RebuildTrackCards();
    }

    private void BtnShowAll_Click(object sender, RoutedEventArgs e)
    {
        TrackIndexService.SetAllVisibility(true);
        RebuildTrackCards();
    }

    private void BtnHideAll_Click(object sender, RoutedEventArgs e)
    {
        TrackIndexService.SetAllVisibility(false);
        RebuildTrackCards();
    }

    #endregion

    public void UpdateTheme(bool isDark)
    {
        SolidColorBrush cardBg = isDark ? new SolidColorBrush(Color.FromArgb(255, 30, 41, 59)) : new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
        SolidColorBrush cardBorder = isDark ? new SolidColorBrush(Color.FromArgb(255, 51, 65, 85)) : new SolidColorBrush(Color.FromArgb(255, 226, 232, 240));
        SolidColorBrush textPrimary = isDark ? new SolidColorBrush(Color.FromArgb(255, 241, 245, 249)) : new SolidColorBrush(Color.FromArgb(255, 15, 23, 42));
        SolidColorBrush textSecondary = isDark ? new SolidColorBrush(Color.FromArgb(255, 148, 163, 184)) : new SolidColorBrush(Color.FromArgb(255, 100, 116, 139));
        SolidColorBrush inputBg = isDark ? new SolidColorBrush(Color.FromArgb(255, 15, 23, 42)) : new SolidColorBrush(Color.FromArgb(255, 248, 250, 252));
        SolidColorBrush inputBorder = isDark ? new SolidColorBrush(Color.FromArgb(255, 71, 85, 105)) : new SolidColorBrush(Color.FromArgb(255, 203, 213, 225));

        if (HeaderCard != null)
        {
            HeaderCard.Background = cardBg;
            HeaderCard.BorderBrush = cardBorder;
        }
        if (ToolbarCard != null)
        {
            ToolbarCard.Background = cardBg;
            ToolbarCard.BorderBrush = cardBorder;
        }
        if (ListContainerBorder != null)
        {
            ListContainerBorder.Background = cardBg;
            ListContainerBorder.BorderBrush = cardBorder;
        }
        if (TxtHeaderTitle != null) TxtHeaderTitle.Foreground = textPrimary;
        if (TxtFolderLabel != null) TxtFolderLabel.Foreground = textSecondary;
        if (TxtFolderNote != null) TxtFolderNote.Foreground = textSecondary;
        if (TxtStatsSummary != null) TxtStatsSummary.Foreground = textPrimary;
        if (TxtStatsSummaryMobile != null) TxtStatsSummaryMobile.Foreground = textPrimary;

        if (TxtGpxFolder != null)
        {
            TxtGpxFolder.Background = inputBg;
            TxtGpxFolder.Foreground = textPrimary;
            TxtGpxFolder.BorderBrush = inputBorder;
        }
        if (TxtSearchFilter != null)
        {
            TxtSearchFilter.Background = inputBg;
            TxtSearchFilter.Foreground = textPrimary;
            TxtSearchFilter.BorderBrush = inputBorder;
        }

        RebuildTrackCards();
    }
}
