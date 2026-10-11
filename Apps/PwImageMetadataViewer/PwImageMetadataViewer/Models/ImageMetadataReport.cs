using System.Collections.Generic;
using System.Text.Json.Serialization;
using PwImageMetadataViewer.Services;

namespace PwImageMetadataViewer.Models
{
    public class ImageMetadataReport
    {
        public bool Success { get; set; }

        [JsonConverter(typeof(FlexibleStringConverter))]
        public string Error { get; set; } = string.Empty;

        [JsonConverter(typeof(FlexibleStringConverter))]
        public string FileName { get; set; } = string.Empty;

        public long FileSize { get; set; }

        [JsonConverter(typeof(FlexibleStringConverter))]
        public string FileSizeFormatted { get; set; } = string.Empty;

        [JsonConverter(typeof(FlexibleStringConverter))]
        public string MimeType { get; set; } = string.Empty;

        [JsonConverter(typeof(FlexibleStringConverter))]
        public string FileType { get; set; } = string.Empty;

        [JsonConverter(typeof(FlexibleStringConverter))]
        public string Make { get; set; } = string.Empty;

        [JsonConverter(typeof(FlexibleStringConverter))]
        public string Model { get; set; } = string.Empty;

        [JsonConverter(typeof(FlexibleStringConverter))]
        public string Lens { get; set; } = string.Empty;

        [JsonConverter(typeof(FlexibleStringConverter))]
        public string DateTime { get; set; } = string.Empty;

        [JsonConverter(typeof(FlexibleStringConverter))]
        public string ExposureTime { get; set; } = string.Empty;

        [JsonConverter(typeof(FlexibleStringConverter))]
        public string FNumber { get; set; } = string.Empty;

        [JsonConverter(typeof(FlexibleStringConverter))]
        public string Iso { get; set; } = string.Empty;

        [JsonConverter(typeof(FlexibleStringConverter))]
        public string FocalLength { get; set; } = string.Empty;

        [JsonConverter(typeof(FlexibleStringConverter))]
        public string Width { get; set; } = string.Empty;

        [JsonConverter(typeof(FlexibleStringConverter))]
        public string Height { get; set; } = string.Empty;

        [JsonConverter(typeof(FlexibleStringConverter))]
        public string Megapixels { get; set; } = string.Empty;

        public bool HasGps { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double? Altitude { get; set; }

        [JsonConverter(typeof(FlexibleStringConverter))]
        public string LatitudeFormatted { get; set; } = string.Empty;

        [JsonConverter(typeof(FlexibleStringConverter))]
        public string LongitudeFormatted { get; set; } = string.Empty;

        [JsonConverter(typeof(FlexibleStringConverter))]
        public string AltitudeFormatted { get; set; } = string.Empty;

        [JsonConverter(typeof(FlexibleStringConverter))]
        public string PreviewUrl { get; set; } = string.Empty;

        [JsonConverter(typeof(FlexibleStringConverter))]
        public string Artist { get; set; } = string.Empty;

        [JsonConverter(typeof(FlexibleStringConverter))]
        public string Copyright { get; set; } = string.Empty;

        [JsonConverter(typeof(FlexibleStringConverter))]
        public string Credit { get; set; } = string.Empty;

        [JsonConverter(typeof(FlexibleStringConverter))]
        public string Caption { get; set; } = string.Empty;

        [JsonConverter(typeof(FlexibleStringConverter))]
        public string Title { get; set; } = string.Empty;

        [JsonConverter(typeof(FlexibleStringConverter))]
        public string TagsOrKeywords { get; set; } = string.Empty;

        public List<MetadataItem> Tags { get; set; } = new List<MetadataItem>();
    }
}
