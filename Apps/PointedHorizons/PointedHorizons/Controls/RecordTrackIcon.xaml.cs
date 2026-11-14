using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PointedHorizons.Controls;

public partial class RecordTrackIcon : UserControl
{
    private static readonly SolidColorBrush EmeraldBrush = new(Color.FromArgb(255, 16, 185, 129));
    private static readonly SolidColorBrush RedBrush = new(Color.FromArgb(255, 239, 68, 68));
    private static readonly SolidColorBrush WhiteBrush = new(Colors.White);

    public static readonly DependencyProperty IconSizeProperty =
        DependencyProperty.Register(nameof(IconSize), typeof(double), typeof(RecordTrackIcon),
            new PropertyMetadata(20.0, OnIconSizeChanged));

    public static readonly DependencyProperty IsRecordingProperty =
        DependencyProperty.Register(nameof(IsRecording), typeof(bool), typeof(RecordTrackIcon),
            new PropertyMetadata(false, OnIsRecordingChanged));

    public double IconSize
    {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    public bool IsRecording
    {
        get => (bool)GetValue(IsRecordingProperty);
        set => SetValue(IsRecordingProperty, value);
    }

    public RecordTrackIcon()
    {
        InitializeComponent();
        Loaded += (s, e) =>
        {
            ApplySize(IconSize);
            ApplyRecordingState(IsRecording);
        };
    }

    private static void OnIconSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RecordTrackIcon icon && e.NewValue is double s)
        {
            icon.ApplySize(s);
        }
    }

    private static void OnIsRecordingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RecordTrackIcon icon && e.NewValue is bool rec)
        {
            icon.ApplyRecordingState(rec);
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

    public void ApplyRecordingState(bool isRecording)
    {
        if (TrailPath == null || StartDot == null || MidDot == null || TargetRing == null || RecordDot == null) return;

        var brush = isRecording ? WhiteBrush : EmeraldBrush;
        TrailPath.Stroke = brush;
        StartDot.Fill = brush;
        MidDot.Fill = brush;
        TargetRing.Stroke = brush;
        RecordDot.Fill = isRecording ? RedBrush : brush;
    }
}

