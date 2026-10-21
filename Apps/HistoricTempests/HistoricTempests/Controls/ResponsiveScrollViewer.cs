using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace HistoricTempests.Controls;

public class ResponsiveScrollViewer : ScrollViewer
{
    public static readonly DependencyProperty CompactWidthThresholdProperty =
        DependencyProperty.Register(
            nameof(CompactWidthThreshold),
            typeof(double),
            typeof(ResponsiveScrollViewer),
            new PropertyMetadata(720.0, OnResponsivePropertyChanged));

    public static readonly DependencyProperty CompactScrollBarWidthProperty =
        DependencyProperty.Register(
            nameof(CompactScrollBarWidth),
            typeof(double),
            typeof(ResponsiveScrollViewer),
            new PropertyMetadata(14.0, OnResponsivePropertyChanged));

    public static readonly DependencyProperty DesktopScrollBarWidthProperty =
        DependencyProperty.Register(
            nameof(DesktopScrollBarWidth),
            typeof(double),
            typeof(ResponsiveScrollViewer),
            new PropertyMetadata(32.0, OnResponsivePropertyChanged));

    public double CompactWidthThreshold
    {
        get => (double)GetValue(CompactWidthThresholdProperty);
        set => SetValue(CompactWidthThresholdProperty, value);
    }

    public double CompactScrollBarWidth
    {
        get => (double)GetValue(CompactScrollBarWidthProperty);
        set => SetValue(CompactScrollBarWidthProperty, value);
    }

    public double DesktopScrollBarWidth
    {
        get => (double)GetValue(DesktopScrollBarWidthProperty);
        set => SetValue(DesktopScrollBarWidthProperty, value);
    }

    private ScrollBar? _verticalScrollBar;

    public ResponsiveScrollViewer()
    {
        Resources.MergedDictionaries.Add(new ResponsiveScrollBarDictionary());
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
    }

    private static void OnResponsivePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ResponsiveScrollViewer viewer)
        {
            viewer.UpdateLayoutState(viewer.ActualWidth);
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateLayoutState(ActualWidth);
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateLayoutState(e.NewSize.Width);
    }

    public void UpdateLayoutState(double width)
    {
        if (width <= 0) return;
        bool isCompact = width < CompactWidthThreshold;
        ApplyScrollBarSizing(isCompact);
    }

    private void ApplyScrollBarSizing(bool isCompact)
    {
        _verticalScrollBar ??= FindVerticalScrollBar(this);
        if (_verticalScrollBar == null)
        {
            Dispatcher.BeginInvoke(() => ApplyScrollBarSizing(isCompact));
            return;
        }

        double targetWidth = isCompact ? CompactScrollBarWidth : DesktopScrollBarWidth;
        _verticalScrollBar.Width = targetWidth;
        _verticalScrollBar.MinWidth = targetWidth;
        _verticalScrollBar.MaxWidth = targetWidth;

        if (FindVisualChildByName<FrameworkElement>(_verticalScrollBar, "VerticalSmallDecrease") is { } up)
        {
            up.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
            if (isCompact)
            {
                up.Height = 0;
                up.Width = 0;
            }
            else
            {
                up.Height = 26;
                up.Width = 28;
            }
        }

        if (FindVisualChildByName<FrameworkElement>(_verticalScrollBar, "VerticalSmallIncrease") is { } down)
        {
            down.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
            if (isCompact)
            {
                down.Height = 0;
                down.Width = 0;
            }
            else
            {
                down.Height = 26;
                down.Width = 28;
            }
        }

        if (!isCompact)
        {
            if (FindVisualChildByName<FrameworkElement>(_verticalScrollBar, "VerticalSmallDecreaseArrow") is { } upArrow)
            {
                upArrow.Width = 14;
                upArrow.Height = 8;
            }
            if (FindVisualChildByName<FrameworkElement>(_verticalScrollBar, "VerticalSmallIncreaseArrow") is { } downArrow)
            {
                downArrow.Width = 14;
                downArrow.Height = 8;
            }
        }
    }

    private static ScrollBar? FindVerticalScrollBar(DependencyObject? parent)
    {
        if (parent == null) return null;
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is ScrollContentPresenter)
            {
                continue;
            }
            if (child is ScrollBar sb && (sb.Orientation == Orientation.Vertical || sb.Name == "VerticalScrollBar"))
                return sb;
            var found = FindVerticalScrollBar(child);
            if (found != null)
                return found;
        }
        return null;
    }

    private static T? FindVisualChildByName<T>(DependencyObject? parent, string name) where T : FrameworkElement
    {
        if (parent == null) return null;
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T fe && fe.Name == name)
                return fe;
            var found = FindVisualChildByName<T>(child, name);
            if (found != null)
                return found;
        }
        return null;
    }
}
