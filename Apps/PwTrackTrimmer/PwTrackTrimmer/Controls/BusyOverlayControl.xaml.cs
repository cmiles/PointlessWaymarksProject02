using System.Windows.Controls;
using PwTrackTrimmer.Models;
using PwTrackTrimmer.Services;

namespace PwTrackTrimmer.Controls
{
    public partial class BusyOverlayControl : UserControl
    {
        public BusyOverlayControl()
        {
            this.InitializeComponent();
            ActivityLogService.Instance.MessageAdded += OnMessageAdded;
        }

        private void OnMessageAdded(LogMessage msg)
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (OverlayLogListBox != null && OverlayLogListBox.Items.Count > 0)
                {
                    OverlayLogListBox.ScrollIntoView(OverlayLogListBox.Items[OverlayLogListBox.Items.Count - 1]);
                }
            });
        }
    }
}
