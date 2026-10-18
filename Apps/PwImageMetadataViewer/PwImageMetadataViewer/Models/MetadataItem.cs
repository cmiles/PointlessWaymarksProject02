#nullable enable
using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Windows.Media;
using PwImageMetadataViewer.Services;

namespace PwImageMetadataViewer.Models
{
    public class MetadataItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        [JsonConverter(typeof(FlexibleStringConverter))]
        public string Group { get; set; } = string.Empty;

        [JsonConverter(typeof(FlexibleStringConverter))]
        public string Tag { get; set; } = string.Empty;

        [JsonConverter(typeof(FlexibleStringConverter))]
        public string Value { get; set; } = string.Empty;

        [JsonConverter(typeof(FlexibleStringConverter))]
        public string RawKey { get; set; } = string.Empty;

        [JsonIgnore]
        public SolidColorBrush TagForeground => ThemeManager.Current.TextPrimary;

        [JsonIgnore]
        public SolidColorBrush ValueForeground => ThemeManager.Current.IsDark ? ThemeManager.BrushDarkTextSecondary : ThemeManager.BrushLightTextPrimary;

        [JsonIgnore]
        public SolidColorBrush RowBorderBrush => ThemeManager.Current.Divider;

        [JsonIgnore]
        public SolidColorBrush GroupBadgeBackground => ThemeManager.Current.SubCardBackground;

        [JsonIgnore]
        public SolidColorBrush GroupBadgeBorderBrush => ThemeManager.Current.CardBorder;

        [JsonIgnore]
        public SolidColorBrush GroupBadgeForeground => ThemeManager.Current.Accent;

        public void RefreshTheme()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TagForeground)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ValueForeground)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RowBorderBrush)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GroupBadgeBackground)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GroupBadgeBorderBrush)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GroupBadgeForeground)));
        }
    }
}
