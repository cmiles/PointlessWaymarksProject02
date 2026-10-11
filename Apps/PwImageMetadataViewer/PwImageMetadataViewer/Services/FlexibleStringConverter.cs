#nullable enable
using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PwImageMetadataViewer.Services
{
    /// <summary>
    /// Custom JsonConverter that converts JSON numbers, booleans, or strings seamlessly into System.String.
    /// This prevents System.Text.Json deserialization exceptions when metadata providers emit numeric values
    /// (e.g. width, height, iso, fNumber) for string model properties.
    /// </summary>
    public class FlexibleStringConverter : JsonConverter<string>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.String:
                    return reader.GetString();

                case JsonTokenType.Number:
                    if (reader.TryGetInt64(out long lVal))
                    {
                        return lVal.ToString(CultureInfo.InvariantCulture);
                    }
                    if (reader.TryGetDouble(out double dVal))
                    {
                        return dVal.ToString(CultureInfo.InvariantCulture);
                    }
                    using (var doc = JsonDocument.ParseValue(ref reader))
                    {
                        return doc.RootElement.GetRawText();
                    }

                case JsonTokenType.True:
                    return "true";

                case JsonTokenType.False:
                    return "false";

                case JsonTokenType.Null:
                    return string.Empty;

                default:
                    using (var doc = JsonDocument.ParseValue(ref reader))
                    {
                        return doc.RootElement.GetRawText();
                    }
            }
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value);
        }
    }
}
