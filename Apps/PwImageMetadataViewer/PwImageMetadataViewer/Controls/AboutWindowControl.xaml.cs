#nullable enable
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PwImageMetadataViewer.Controls
{
    public partial class AboutWindowControl : UserControl
    {
        public event EventHandler? CloseRequested;

        public AboutWindowControl()
        {
            this.InitializeComponent();
            this.AllowScrollOnTouchMove = false;
            this.MouseWheel += AboutWindowControl_MouseWheel;
        }

        private void AboutWindowControl_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (AboutScrollViewer != null)
            {
                double currentOffset = AboutScrollViewer.VerticalOffset;
                double newOffset = Math.Max(0, currentOffset - e.Delta);
                AboutScrollViewer.ScrollToVerticalOffset(newOffset);
            }
            e.Handled = true;
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
