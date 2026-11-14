using System;
using System.Windows;
using System.Windows.Controls;

namespace PointedHorizons.Controls;

public partial class CurrentLocationIcon : UserControl
{
    public static readonly DependencyProperty IconSizeProperty =
        DependencyProperty.Register(nameof(IconSize), typeof(double), typeof(CurrentLocationIcon),
            new PropertyMetadata(18.0, OnIconSizeChanged));

    public double IconSize
    {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    public CurrentLocationIcon()
    {
        InitializeComponent();
        Loaded += (s, e) => ApplySize(IconSize);
    }

    private static void OnIconSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CurrentLocationIcon icon && e.NewValue is double s)
        {
            icon.ApplySize(s);
        }
    }

    public void ApplySize(double s)
    {
        if (RootGrid == null || PulseRing == null || MidRing == null || CenterDot == null) return;

        RootGrid.Width = s;
        RootGrid.Height = s;

        PulseRing.Width = s;
        PulseRing.Height = s;
        PulseRing.CornerRadius = new CornerRadius(s / 2.0);

        double mid = Math.Max(4.0, s * (12.0 / 18.0));
        MidRing.Width = mid;
        MidRing.Height = mid;
        MidRing.CornerRadius = new CornerRadius(mid / 2.0);

        double center = Math.Max(3.0, s * (8.0 / 18.0));
        CenterDot.Width = center;
        CenterDot.Height = center;
        CenterDot.CornerRadius = new CornerRadius(center / 2.0);

        if (CoreDot != null)
        {
            double core = Math.Max(1.0, s * (2.0 / 18.0));
            CoreDot.Width = core;
            CoreDot.Height = core;
            CoreDot.CornerRadius = new CornerRadius(core / 2.0);
        }
    }
}

