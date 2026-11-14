using System.Windows;
using System.Windows.Controls;

namespace PointedHorizons.Controls;

public partial class GpsIcon : UserControl
{
    public static readonly DependencyProperty IconSizeProperty =
        DependencyProperty.Register(nameof(IconSize), typeof(double), typeof(GpsIcon),
            new PropertyMetadata(24.0, OnIconSizeChanged));

    public double IconSize
    {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    public GpsIcon()
    {
        InitializeComponent();
        Loaded += (s, e) => ApplySize(IconSize);
    }

    private static void OnIconSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GpsIcon icon && e.NewValue is double s)
        {
            icon.ApplySize(s);
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
}

