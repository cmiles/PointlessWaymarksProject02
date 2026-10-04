using System;
using System.Collections.ObjectModel;
using System.Windows;
using PwTrackTrimmer.Models;

namespace PwTrackTrimmer.Services
{
    public class ActivityLogService
    {
        private static readonly ActivityLogService _instance = new ActivityLogService();
        public static ActivityLogService Instance => _instance;

        private const int MaxMessagesLimit = 500;
        private readonly object _lock = new object();

        public ObservableCollection<LogMessage> Messages { get; } = new ObservableCollection<LogMessage>();

        public event Action<LogMessage> MessageAdded;

        public ActivityLogService()
        {
            Info("Activity log system initialized.", "System");
        }

        public void Log(string message, LogLevel level = LogLevel.Info, string category = "System")
        {
            var log = new LogMessage
            {
                Timestamp = DateTime.Now,
                Level = level,
                Category = string.IsNullOrWhiteSpace(category) ? "System" : category,
                Message = message
            };

            // Ensure collection modifications run on the UI thread for OpenSilver
            if (Application.Current?.RootVisual?.Dispatcher != null)
            {
                Application.Current.RootVisual.Dispatcher.BeginInvoke(() => AddMessageInternal(log));
            }
            else
            {
                AddMessageInternal(log);
            }
        }

        private void AddMessageInternal(LogMessage log)
        {
            lock (_lock)
            {
                if (Messages.Count >= MaxMessagesLimit)
                {
                    // Remove oldest 20 to prevent frequent removals
                    for (int i = 0; i < 20 && Messages.Count > 0; i++)
                    {
                        Messages.RemoveAt(0);
                    }
                }

                Messages.Add(log);
            }

            MessageAdded?.Invoke(log);
        }

        public void Info(string message, string category = "System") => Log(message, LogLevel.Info, category);
        public void Success(string message, string category = "System") => Log(message, LogLevel.Success, category);
        public void Warn(string message, string category = "System") => Log(message, LogLevel.Warning, category);
        public void Error(string message, string category = "System") => Log(message, LogLevel.Error, category);

        public void Clear()
        {
            lock (_lock)
            {
                Messages.Clear();
            }
            Info("Activity log cleared.", "System");
        }
    }
}
