using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PointedHorizons.Services;

namespace PointedHorizons.Controls;

public partial class GeoNamesSearchControl : UserControl
{
    public event EventHandler<GeoNamesResult>? LocationSelected;
    public event EventHandler? CloseRequested;

    private readonly List<Border> _resultCards = new();
    private bool _isSearching;

    public GeoNamesSearchControl()
    {
        InitializeComponent();
        Loaded += GeoNamesSearchControl_Loaded;
    }

    private void GeoNamesSearchControl_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateTheme(ThemeManager.Current.IsDark);
        UpdateAccountInfo();
    }

    public void UpdateAccountInfo()
    {
        if (TxtGeoNamesAccount != null)
        {
            if (GeoNamesService.IsConfigured)
            {
                TxtGeoNamesAccount.Text = $"Account: {GeoNamesService.Username}";
            }
            else
            {
                TxtGeoNamesAccount.Text = "No GeoNames username set (configure in Options)";
            }
        }
    }

    public void FocusSearchInput()
    {
        UpdateAccountInfo();
        TxtSearchQuery?.Focus();
        TxtSearchQuery?.SelectAll();
    }

    public void UpdateTheme(bool isDark)
    {
        var tm = ThemeManager.Current;

        if (CardBorder != null)
        {
            CardBorder.Background = tm.HeaderBackground;
            CardBorder.BorderBrush = tm.HeaderBorder;
        }

        if (Divider1 != null) Divider1.Background = tm.HeaderBorder;
        if (TxtDialogTitle != null) TxtDialogTitle.Foreground = tm.TextPrimary;
        if (TxtHelp != null) TxtHelp.Foreground = tm.TextSecondary;
        if (TxtSearchStatus != null) TxtSearchStatus.Foreground = tm.TextMuted;
        if (TxtGeoNamesAccount != null) TxtGeoNamesAccount.Foreground = tm.TextMuted;

        if (TxtSearchQuery != null)
        {
            TxtSearchQuery.Background = tm.ButtonInactiveBackground;
            TxtSearchQuery.Foreground = tm.TextPrimary;
            TxtSearchQuery.BorderBrush = tm.ButtonInactiveBorder;
        }

        if (BtnCloseDialog != null)
        {
            BtnCloseDialog.Background = tm.ButtonInactiveBackground;
            BtnCloseDialog.BorderBrush = tm.ButtonInactiveBorder;
            BtnCloseDialog.Foreground = tm.TextPrimary;
        }

        if (BtnSearch != null)
        {
            BtnSearch.Background = tm.ButtonActiveBackground;
            BtnSearch.BorderBrush = tm.ButtonActiveBorder;
            BtnSearch.Foreground = tm.ButtonActiveForeground;
        }

        if (BtnCancelBottom != null)
        {
            BtnCancelBottom.Background = tm.ButtonInactiveBackground;
            BtnCancelBottom.BorderBrush = tm.ButtonInactiveBorder;
            BtnCancelBottom.Foreground = tm.TextPrimary;
        }

        // Re-style existing result cards
        foreach (var card in _resultCards)
        {
            card.Background = tm.ButtonInactiveBackground;
            card.BorderBrush = tm.ButtonInactiveBorder;
        }
    }

    private void TxtSearchQuery_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            BtnSearch_Click(sender, new RoutedEventArgs());
        }
    }

    private async void BtnSearch_Click(object sender, RoutedEventArgs e)
    {
        if (_isSearching) return;

        string query = TxtSearchQuery?.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(query))
        {
            if (TxtSearchStatus != null)
            {
                TxtSearchStatus.Text = "Please enter a location name to search.";
                TxtSearchStatus.Foreground = new SolidColorBrush(Colors.Orange);
            }
            return;
        }

        if (!GeoNamesService.IsConfigured)
        {
            if (TxtSearchStatus != null)
            {
                TxtSearchStatus.Text = "GeoNames username is not set. Please enter your free username in Settings / Options.";
                TxtSearchStatus.Foreground = new SolidColorBrush(Colors.Orange);
            }
            return;
        }

        _isSearching = true;
        if (SearchProgressBar != null) SearchProgressBar.Visibility = Visibility.Visible;
        if (BtnSearch != null) BtnSearch.IsEnabled = false;
        if (TxtSearchStatus != null)
        {
            TxtSearchStatus.Text = $"Searching GeoNames for \"{query}\"...";
            TxtSearchStatus.Foreground = ThemeManager.Current.TextSecondary;
        }

        try
        {
            var (results, errorMessage) = await GeoNamesService.SearchAsync(query);

            if (!string.IsNullOrWhiteSpace(errorMessage))
            {
                if (TxtSearchStatus != null)
                {
                    TxtSearchStatus.Text = $"⚠️ {errorMessage}";
                    TxtSearchStatus.Foreground = new SolidColorBrush(Colors.Orange);
                }
                return;
            }

            RenderResults(results, query);
        }
        catch (Exception ex)
        {
            if (TxtSearchStatus != null)
            {
                TxtSearchStatus.Text = $"Search failed: {ex.Message}";
                TxtSearchStatus.Foreground = new SolidColorBrush(Colors.Red);
            }
        }
        finally
        {
            _isSearching = false;
            if (SearchProgressBar != null) SearchProgressBar.Visibility = Visibility.Collapsed;
            if (BtnSearch != null) BtnSearch.IsEnabled = true;
        }
    }

    private void RenderResults(List<GeoNamesResult> results, string query)
    {
        if (ResultsPanel == null) return;

        ResultsPanel.Children.Clear();
        _resultCards.Clear();

        if (results == null || results.Count == 0)
        {
            if (TxtSearchStatus != null)
            {
                TxtSearchStatus.Text = $"No locations found matching \"{query}\". Try a broader term.";
                TxtSearchStatus.Foreground = ThemeManager.Current.TextMuted;
            }
            return;
        }

        if (TxtSearchStatus != null)
        {
            TxtSearchStatus.Text = $"Found {results.Count} result{(results.Count == 1 ? "" : "s")}. Tap a location to select:";
            TxtSearchStatus.Foreground = ThemeManager.Current.Accent;
        }

        var tm = ThemeManager.Current;

        foreach (var item in results)
        {
            var card = new Border
            {
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                Background = tm.ButtonInactiveBackground,
                BorderBrush = tm.ButtonInactiveBorder,
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 2, 0, 4),
                Cursor = Cursors.Hand
            };

            var stack = new StackPanel { Orientation = Orientation.Vertical };

            var txtTitle = new TextBlock
            {
                Text = item.TitleDisplay,
                FontSize = 11.5,
                FontWeight = FontWeights.Bold,
                Foreground = tm.TextPrimary,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 2)
            };

            var txtSubtitle = new TextBlock
            {
                Text = item.SubtitleDisplay,
                FontSize = 10,
                Foreground = tm.TextSecondary,
                TextWrapping = TextWrapping.Wrap
            };

            stack.Children.Add(txtTitle);
            stack.Children.Add(txtSubtitle);
            card.Child = stack;

            // Hover styling
            card.MouseEnter += (s, e) =>
            {
                card.Background = tm.ButtonActiveBackground;
                card.BorderBrush = tm.ButtonActiveBorder;
                txtTitle.Foreground = tm.ButtonActiveForeground;
                txtSubtitle.Foreground = tm.ButtonActiveForeground;
            };

            card.MouseLeave += (s, e) =>
            {
                card.Background = tm.ButtonInactiveBackground;
                card.BorderBrush = tm.ButtonInactiveBorder;
                txtTitle.Foreground = tm.TextPrimary;
                txtSubtitle.Foreground = tm.TextSecondary;
            };

            // Selection action
            card.MouseLeftButtonDown += (s, e) =>
            {
                LocationSelected?.Invoke(this, item);
                CloseRequested?.Invoke(this, EventArgs.Empty);
            };

            _resultCards.Add(card);
            ResultsPanel.Children.Add(card);
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}

