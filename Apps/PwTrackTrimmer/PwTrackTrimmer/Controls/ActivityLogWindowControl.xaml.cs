using System.Windows.Controls;
using PwTrackTrimmer.Models;
using PwTrackTrimmer.Services;

namespace PwTrackTrimmer.Controls
{
    public partial class ActivityLogWindowControl : UserControl
    {
        public ActivityLogWindowControl()
        {
            this.InitializeComponent();
            ActivityLogService.Instance.MessageAdded += OnMessageAdded;
        }

        private void OnMessageAdded(LogMessage msg)
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (MessagesListBox != null && MessagesListBox.Items.Count > 0)
                {
                    MessagesListBox.ScrollIntoView(MessagesListBox.Items[MessagesListBox.Items.Count - 1]);
                }
            });
        }
    }
}
