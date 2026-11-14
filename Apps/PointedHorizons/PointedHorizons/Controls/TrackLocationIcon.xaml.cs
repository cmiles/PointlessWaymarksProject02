using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PointedHorizons.Services;

namespace PointedHorizons.Controls;

public partial class TrackLocationIcon : UserControl
{
    private static readonly SolidColorBrush BrushActiveBlue = new(Color.FromArgb(255, 14, 165, 233));
    private static readonly SolidColorBrush BrushActiveNav = new(Color.FromArgb(255, 2, 132, 199));
    private static readonly SolidColorBrush BrushWhite = new(Colors.White);

    public static readonly DependencyProperty IconSizeProperty =
        DependencyProperty.Register(nameof(IconSize), typeof(double), typeof(TrackLocationIcon),
            new PropertyMetadata(18.0, OnIconSizeChanged));

    public static readonly DependencyProperty IsTrackingProperty =
        DependencyProperty.Register(nameof(IsTracking), typeof(bool), typeof(TrackLocationIcon),
            new PropertyMetadata(false, OnIsTrackingChanged));

    public double IconSize
    {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    public bool IsTracking
    {
        get => (bool)GetValue(IsTrackingProperty);
        set => SetValue(IsTrackingProperty, value);
    }

    public TrackLocationIcon()
    {
        InitializeComponent();
        Loaded += (s, e) =>
        {
            ApplySize(IconSize);
            UpdateTrackingVisuals(IsTracking);
        };
    }

    private static void OnIconSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TrackLocationIcon icon && e.NewValue is double s)
        {
            icon.ApplySize(s);
        }
    }

    private static void OnIsTrackingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TrackLocationIcon icon && e.NewValue is bool tracking)
        {
            icon.UpdateTrackingVisuals(tracking);
        }
    }

    public void ApplySize(double s)
    {
        if (IconViewbox != null)
        {
            IconViewbox.Width = s;
            IconViewbox.Height = s;
        }
    }

    public void UpdateTrackingVisuals(bool tracking)
    {
        if (CrosshairCircle == null || CrosshairTicks == null || PositionTriangle == null) return;

        var tm = ThemeManager.Current;

        if (tracking)
        {
            if (ActivePulseRing != null) ActivePulseRing.Visibility = Visibility.Visible;
            CrosshairCircle.Stroke = BrushActiveBlue;
            CrosshairTicks.Stroke = BrushActiveBlue;
            PositionTriangle.Fill = BrushActiveNav;
            PositionTriangle.Stroke = BrushWhite;
        }
        else
        {
            if (ActivePulseRing != null) ActivePulseRing.Visibility = Visibility.Collapsed;
            var inactiveBrush = tm.TextSecondary ?? new SolidColorBrush(Color.FromArgb(255, 148, 163, 184));
            CrosshairCircle.Stroke = inactiveBrush;
            CrosshairTicks.Stroke = inactiveBrush;
            PositionTriangle.Fill = inactiveBrush;
            PositionTriangle.Stroke = tm.ButtonInactiveBackground ?? new SolidColorBrush(Color.FromArgb(255, 30, 41, 59));
        }
    }
}

