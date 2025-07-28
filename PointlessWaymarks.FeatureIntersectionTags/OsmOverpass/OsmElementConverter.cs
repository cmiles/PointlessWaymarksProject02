using System.Text.Json;
using System.Text.Json.Serialization;

namespace PointlessWaymarks.FeatureIntersectionTags.OsmOverpass;

public class OsmElementConverter : JsonConverter<OsmElement>
{
    public override OsmElement? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var type = doc.RootElement.GetProperty("type").GetString();

        return type switch
        {
            "node" => doc.RootElement.Deserialize<OsmNode>(options),
            "way" => doc.RootElement.Deserialize<OsmWay>(options),
            "relation" => doc.RootElement.Deserialize<OsmRelation>(options),
            _ => doc.RootElement.Deserialize<OsmOtherElement>(options)
        };
    }

    public override void Write(Utf8JsonWriter writer, OsmElement value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}