#nullable enable
using System;
using System.Windows;
using System.Windows.Controls;

namespace PwImageMetadataViewer.Controls
{
    public partial class AboutWindowControl : UserControl
    {
        public event EventHandler? CloseRequested;

        public AboutWindowControl()
        {
            this.InitializeComponent();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
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
                    // OpenSilver default handler
                }
            }
        }
    }
}
