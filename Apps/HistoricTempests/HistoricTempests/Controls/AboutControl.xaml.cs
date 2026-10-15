#nullable enable
using System;
using System.Windows;
using System.Windows.Controls;

namespace HistoricTempests.Controls;

public partial class AboutControl : UserControl
{
    public AboutControl()
    {
        InitializeComponent();
    }

    private void Hyperlink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is HyperlinkButton btn && btn.NavigateUri != null)
        {
            try
            {
                OpenSilver.Interop.ExecuteJavaScriptVoid($"window.open('{btn.NavigateUri.AbsoluteUri}', '_blank');");
            }
            catch
            {
                // OpenSilver default navigation
            }
        }
    }
}
