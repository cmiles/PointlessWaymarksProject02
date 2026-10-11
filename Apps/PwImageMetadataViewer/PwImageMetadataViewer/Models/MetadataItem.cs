using System.Text.Json.Serialization;
using PwImageMetadataViewer.Services;

namespace PwImageMetadataViewer.Models
{
    public class MetadataItem
    {
        [JsonConverter(typeof(FlexibleStringConverter))]
        public string Group { get; set; } = string.Empty;

        [JsonConverter(typeof(FlexibleStringConverter))]
        public string Tag { get; set; } = string.Empty;

        [JsonConverter(typeof(FlexibleStringConverter))]
        public string Value { get; set; } = string.Empty;

        [JsonConverter(typeof(FlexibleStringConverter))]
        public string RawKey { get; set; } = string.Empty;
    }
}
