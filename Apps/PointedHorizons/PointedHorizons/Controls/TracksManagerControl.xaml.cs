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

    public TracksManagerControl()
    {
        InitializeComponent();
        Loaded += TracksManagerControl_Loaded;

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
    }

    private void TracksManagerControl_Loaded(object sender, RoutedEventArgs e)
    {
        OfflineDataService.EnsureSettingsLoaded();
        UpdateTheme(ThemeManager.Current.IsDark);
        RefreshFolderDisplay();
        RefreshView();

        // Trigger refresh scan when opening to discover newly exported or copied GPX files
        if (!TrackIndexService.IsScanning)
        {
            _ = TrackIndexService.RefreshAsync(forceFullRescan: false);
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

        if (TxtStatsSummary != null)
        {
            if (TrackIndexService.IsScanning)
            {
                TxtStatsSummary.Text = "🔄 Scanning tracks folder...";
            }
            else
            {
                TxtStatsSummary.Text = $"Files: {list.Count} / {allTracks.Count} | On Map: {visibleCount} | Dist: {totalDistKm:F1} km ({totalDistKm * 0.621371:F1} mi)";
            }
        }

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
        var card = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 9, 12, 9),
            Margin = new Thickness(0, 0, 0, 8),
            Background = isDark ? new SolidColorBrush(Color.FromArgb(255, 24, 33, 47)) : new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)),
            BorderBrush = isDark ? new SolidColorBrush(Color.FromArgb(255, 51, 65, 85)) : new SolidColorBrush(Color.FromArgb(255, 226, 232, 240))
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Column 0: CheckBox for Map Visibility
        var chk = new CheckBox
        {
            IsChecked = track.IsVisibleOnMap,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };
        ToolTipService.SetToolTip(chk, "Show or hide this track on the map");
        chk.Click += (s, e) =>
        {
            TrackIndexService.SetTrackVisibility(track.Id, chk.IsChecked == true);
        };
        Grid.SetColumn(chk, 0);
        grid.Children.Add(chk);

        // Column 1: Track Details
        var detailsPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };

        // Top Row: Badges, Color, Track Name, Filename
        var topRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 4)
        };

        // Format Tag Pill (GPX / TCX / FIT)
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
        topRow.Children.Add(formatTag);

        // Color badge indicator
        Color trackColor = ParseColorSafe(track.DisplayColor);
        var colorBadge = new Border
        {
            Width = 12,
            Height = 12,
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(trackColor),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Colors.White)
        };
        topRow.Children.Add(colorBadge);

        // Track Name
        var txtName = new TextBlock
        {
            Text = track.TrackName,
            FontWeight = FontWeights.Bold,
            FontSize = 11.5,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = isDark ? new SolidColorBrush(Color.FromArgb(255, 241, 245, 249)) : new SolidColorBrush(Color.FromArgb(255, 15, 23, 42))
        };
        topRow.Children.Add(txtName);

        // File name and size
        var txtFile = new TextBlock
        {
            Text = $" ({track.FileName} · {track.FormattedSize})",
            FontSize = 9.5,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = isDark ? new SolidColorBrush(Color.FromArgb(255, 148, 163, 184)) : new SolidColorBrush(Color.FromArgb(255, 100, 116, 139))
        };
        topRow.Children.Add(txtFile);
        detailsPanel.Children.Add(topRow);

        // Bottom Row: Distance, Duration, Date, Point Count
        var bottomRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        string distStr = $"📏 {track.TotalDistanceKm:F2} km ({track.TotalDistanceKm * 0.621371:F2} mi)";
        var txtDist = new TextBlock
        {
            Text = distStr,
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 10, 0),
            Foreground = isDark ? new SolidColorBrush(Color.FromArgb(255, 203, 213, 225)) : new SolidColorBrush(Color.FromArgb(255, 51, 65, 85))
        };
        bottomRow.Children.Add(txtDist);

        if (track.TotalClimbM > 0 || track.TotalDescentM > 0)
        {
            var txtEle = new TextBlock
            {
                Text = $"⛰️ +{track.TotalClimbM:F0}m / -{track.TotalDescentM:F0}m",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 10, 0),
                Foreground = isDark ? new SolidColorBrush(Color.FromArgb(255, 16, 185, 129)) : new SolidColorBrush(Color.FromArgb(255, 5, 150, 105))
            };
            bottomRow.Children.Add(txtEle);
        }

        if (!string.IsNullOrEmpty(track.FormattedDuration) && track.FormattedDuration != "00:00")
        {
            var txtDur = new TextBlock
            {
                Text = $"⏱️ {track.FormattedDuration}",
                FontSize = 10,
                Margin = new Thickness(0, 0, 10, 0),
                Foreground = isDark ? new SolidColorBrush(Color.FromArgb(255, 148, 163, 184)) : new SolidColorBrush(Color.FromArgb(255, 100, 116, 139))
            };
            bottomRow.Children.Add(txtDur);
        }

        DateTime dateVal = track.StartTimeUtc ?? track.LastModifiedUtc;
        var txtDate = new TextBlock
        {
            Text = $"📅 {dateVal.ToLocalTime():yyyy-MM-dd HH:mm}",
            FontSize = 10,
            Margin = new Thickness(0, 0, 10, 0),
            Foreground = isDark ? new SolidColorBrush(Color.FromArgb(255, 148, 163, 184)) : new SolidColorBrush(Color.FromArgb(255, 100, 116, 139))
        };
        bottomRow.Children.Add(txtDate);

        var txtPts = new TextBlock
        {
            Text = $"📍 {track.PointCount:N0} pts",
            FontSize = 10,
            Foreground = isDark ? new SolidColorBrush(Color.FromArgb(255, 148, 163, 184)) : new SolidColorBrush(Color.FromArgb(255, 100, 116, 139))
        };
        bottomRow.Children.Add(txtPts);

        detailsPanel.Children.Add(bottomRow);
        Grid.SetColumn(detailsPanel, 1);
        grid.Children.Add(detailsPanel);

        // Column 2: Zoom to Track Button
        var btnZoom = new Button
        {
            Content = "🗺️ Zoom to Track",
            Style = TryFindResource("PillButtonStyle") as Style,
            Padding = new Thickness(10, 5, 10, 5),
            FontSize = 10.5,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        ToolTipService.SetToolTip(btnZoom, "Zoom the main map to this track's bounds and make it visible");
        btnZoom.Click += (s, e) =>
        {
            track.IsVisibleOnMap = true;
            TrackIndexService.SetTrackVisibility(track.Id, true);
            TrackZoomRequested?.Invoke(this, track);
        };
        Grid.SetColumn(btnZoom, 2);
        grid.Children.Add(btnZoom);

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
        if (TxtStatsSummary != null) TxtStatsSummary.Text = "🔄 Scanning tracks folder...";
        await TrackIndexService.RefreshAsync(forceFullRescan: true);
        RebuildTrackCards();
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
