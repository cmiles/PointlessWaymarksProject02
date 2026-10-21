#nullable enable
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PwTrackTrimmer.Controls;

public class LoadingSpinner : Border
{
    public static readonly DependencyProperty SpinnerSizeProperty =
        DependencyProperty.Register(
            nameof(SpinnerSize),
            typeof(double),
            typeof(LoadingSpinner),
            new PropertyMetadata(28.0, OnSizeChanged));

    public static readonly DependencyProperty StrokeThicknessProperty =
        DependencyProperty.Register(
            nameof(StrokeThickness),
            typeof(double),
            typeof(LoadingSpinner),
            new PropertyMetadata(3.0, OnVisualPropertyChanged));

    public static readonly DependencyProperty SpinnerColorProperty =
        DependencyProperty.Register(
            nameof(SpinnerColor),
            typeof(Brush),
            typeof(LoadingSpinner),
            new PropertyMetadata(null, OnVisualPropertyChanged));

    public double SpinnerSize
    {
        get => (double)GetValue(SpinnerSizeProperty);
        set => SetValue(SpinnerSizeProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public Brush? SpinnerColor
    {
        get => (Brush?)GetValue(SpinnerColorProperty);
        set => SetValue(SpinnerColorProperty, value);
    }

    private static bool _stylesInjected;

    public LoadingSpinner()
    {
        Width = SpinnerSize;
        Height = SpinnerSize;
        Background = Brushes.Transparent;
        BorderThickness = new Thickness(0);
        Loaded += LoadingSpinner_Loaded;
        IsVisibleChanged += LoadingSpinner_IsVisibleChanged;
    }

    private static void OnSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LoadingSpinner spinner && e.NewValue is double sz)
        {
            spinner.Width = sz;
            spinner.Height = sz;
            spinner.UpdateDomSpinner();
        }
    }

    private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LoadingSpinner spinner)
        {
            spinner.UpdateDomSpinner();
        }
    }

    private void LoadingSpinner_Loaded(object sender, RoutedEventArgs e)
    {
        EnsureStyles();
        UpdateDomSpinner();
    }

    private void LoadingSpinner_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (Visibility == Visibility.Visible)
        {
            UpdateDomSpinner();
        }
    }

    private static void EnsureStyles()
    {
        if (_stylesInjected) return;
        try
        {
            OpenSilver.Interop.ExecuteJavaScriptVoid(@"
                (function() {
                    var id = 'pw-loading-spinner-styles';
                    if (!document.getElementById(id)) {
                        var style = document.createElement('style');
                        style.id = id;
                        style.textContent = `
                            @keyframes pw-spin {
                                0% { transform: rotate(0deg); }
                                100% { transform: rotate(360deg); }
                            }
                            .pw-spinner-container {
                                display: flex;
                                align-items: center;
                                justify-content: center;
                                width: 100%;
                                height: 100%;
                            }
                            .pw-spinner-ring {
                                border-radius: 50%;
                                animation: pw-spin 0.8s linear infinite;
                                box-sizing: border-box;
                            }
                        `;
                        document.head.appendChild(style);
                    }
                })();
            ");
            _stylesInjected = true;
        }
        catch { }
    }

    public void UpdateDomSpinner()
    {
        if (Visibility != Visibility.Visible) return;

        try
        {
            object div = OpenSilver.Interop.GetDiv(this);
            if (div == null)
            {
                Dispatcher.BeginInvoke(UpdateDomSpinner);
                return;
            }

            EnsureStyles();

            double size = SpinnerSize > 0 ? SpinnerSize : (Width > 0 ? Width : 28);
            double stroke = StrokeThickness > 0 ? StrokeThickness : 3;
            string hexColor = "#38BDF8";

            if (SpinnerColor is SolidColorBrush scb)
            {
                hexColor = $"#{scb.Color.R:X2}{scb.Color.G:X2}{scb.Color.B:X2}";
            }
            else if (Application.Current?.Resources?["SkyAccentBrush"] is SolidColorBrush resBrush)
            {
                hexColor = $"#{resBrush.Color.R:X2}{resBrush.Color.G:X2}{resBrush.Color.B:X2}";
            }

            OpenSilver.Interop.ExecuteJavaScriptVoid(@"
                var host = $0;
                var size = $1;
                var stroke = $2;
                var color = $3;
                if (host) {
                    var trackColor = 'rgba(56, 189, 248, 0.2)';
                    if (color && color.startsWith('#') && color.length === 7) {
                        var r = parseInt(color.slice(1, 3), 16);
                        var g = parseInt(color.slice(3, 5), 16);
                        var b = parseInt(color.slice(5, 7), 16);
                        trackColor = 'rgba(' + r + ',' + g + ',' + b + ',0.25)';
                    }
                    host.innerHTML = '<div class=""pw-spinner-container""><div class=""pw-spinner-ring"" style=""width:' + size + 'px; height:' + size + 'px; border:' + stroke + 'px solid ' + trackColor + '; border-top:' + stroke + 'px solid ' + color + ';""></div></div>';
                }
            ", div, size, stroke, hexColor);
        }
        catch { }
    }
}
