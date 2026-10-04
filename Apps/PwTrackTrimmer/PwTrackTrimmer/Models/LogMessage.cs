using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PwTrackTrimmer.Models
{
    public enum LogLevel
    {
        Info,
        Success,
        Warning,
        Error
    }

    public class LogMessage : INotifyPropertyChanged
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public LogLevel Level { get; set; } = LogLevel.Info;
        public string Category { get; set; } = "System";
        public string Message { get; set; } = string.Empty;

        public string TimeFormatted => Timestamp.ToString("HH:mm:ss.fff");

        public string LevelBadge => Level switch
        {
            LogLevel.Info => "INFO",
            LogLevel.Success => "OK",
            LogLevel.Warning => "WARN",
            LogLevel.Error => "ERR",
            _ => "LOG"
        };

        public string LevelColorHex => Level switch
        {
            LogLevel.Info => "#38BDF8",       // Sky blue
            LogLevel.Success => "#34D399",    // Emerald green
            LogLevel.Warning => "#FBBF24",    // Amber yellow
            LogLevel.Error => "#F87171",      // Coral red
            _ => "#94A3B8"
        };

        public string BadgeBackgroundHex => Level switch
        {
            LogLevel.Info => "#0C4A6E",
            LogLevel.Success => "#064E3B",
            LogLevel.Warning => "#78350F",
            LogLevel.Error => "#7F1D1D",
            _ => "#1E293B"
        };

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
