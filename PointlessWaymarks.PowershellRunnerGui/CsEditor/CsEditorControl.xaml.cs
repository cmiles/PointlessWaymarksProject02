using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PointlessWaymarks.PowerShellRunnerGui.CsEditor;

/// <summary>
///     Interaction logic for CsEditorControl.xaml
/// </summary>
public partial class CsEditorControl
{
    public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(
        nameof(IsReadOnly), typeof(bool), typeof(CsEditorControl),
        new PropertyMetadata(false, IsReadOnlyPropertyChangedCallback));

    public CsEditorControl()
    {
        InitializeComponent();
    }

    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    private static void IsReadOnlyPropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CsEditorControl editControl)
        {
            if (Application.Current.Dispatcher.CheckAccess())
                editControl.CsCodeEditor.IsReadOnly = (bool)e.NewValue;
            else
                Application.Current.Dispatcher.BeginInvoke(() => editControl.CsCodeEditor.IsReadOnly = (bool)e.NewValue);
        }
    }
}