using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Serval.Translation.Utils;

// Credit to anish-patel post in https://stackoverflow.com/questions/11561597/deserialize-json-recursively-to-idictionarystring-object
public class DictionaryJsonConverter : JsonConverter
{
    public override void WriteJson(JsonWriter writer, object? value, Newtonsoft.Json.JsonSerializer serializer)
    {
        WriteValue(writer, value);
    }

    private void WriteValue(JsonWriter writer, object? value)
    {
        if (value is null)
        {
            writer.WriteNull();
            return;
        }
        var t = JToken.FromObject(value);
        switch (t.Type)
        {
            case JTokenType.Object:
                WriteObject(writer, value);
                break;
            case JTokenType.Array:
                WriteArray(writer, value);
                break;
            default:
                writer.WriteValue(value);
                break;
        }
    }

    private void WriteObject(JsonWriter writer, object value)
    {
        writer.WriteStartObject();
        var obj =
            value as IDictionary<string, object>
            ?? throw new JsonSerializationException("Object must implement IDictionary<string, object>");
        foreach (var kvp in obj)
        {
            writer.WritePropertyName(kvp.Key);
            WriteValue(writer, kvp.Value);
        }
        writer.WriteEndObject();
    }

    private void WriteArray(JsonWriter writer, object value)
    {
        writer.WriteStartArray();
        var array =
            value as IEnumerable<object>
            ?? throw new JsonSerializationException(
                "Unexpected type when converting IDictionary<string, object> to Array."
            );
        foreach (var o in array)
        {
            WriteValue(writer, o);
        }
        writer.WriteEndArray();
    }

    public override object ReadJson(
        JsonReader reader,
        Type objectType,
        object? existingValue,
        Newtonsoft.Json.JsonSerializer serializer
    )
    {
        return ReadValue(reader);
    }

    private object ReadValue(JsonReader reader)
    {
        while (reader.TokenType == JsonToken.Comment)
        {
            if (!reader.Read())
                throw new JsonSerializationException("Unexpected Token when converting IDictionary<string, object>");
        }

        return reader.TokenType switch
        {
            JsonToken.StartObject => ReadObject(reader),
            JsonToken.StartArray => ReadArray(reader),
            JsonToken.Integer
            or JsonToken.Float
            or JsonToken.String
            or JsonToken.Boolean
            or JsonToken.Undefined
            or JsonToken.Null
            or JsonToken.Date
            or JsonToken.Bytes
                => reader.Value
                    ?? throw new JsonSerializationException(
                        "Unexpected token when converting to " + reader.TokenType.ToString()
                    ),
            _
                => throw new JsonSerializationException(
                    string.Format("Unexpected token when converting IDictionary<string, object>: {0}", reader.TokenType)
                ),
        };
    }

    private object ReadArray(JsonReader reader)
    {
        IList<object> list = [];

        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonToken.Comment:
                    break;
                default:
                    var v = ReadValue(reader);

                    list.Add(v);
                    break;
                case JsonToken.EndArray:
                    return list;
            }
        }

        throw new JsonSerializationException("Unexpected end when reading IDictionary<string, object>");
    }

    private object ReadObject(JsonReader reader)
    {
        var obj = new Dictionary<string, object>();

        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonToken.PropertyName:
                    var propertyName =
                        reader.Value!.ToString()
                        ?? throw new JsonSerializationException(
                            "Unexpected Token when converting IDictionary<string, object>"
                        );

                    if (!reader.Read())
                    {
                        throw new JsonSerializationException("Unexpected end when reading IDictionary<string, object>");
                    }

                    var v = ReadValue(reader);

                    obj[propertyName] = v;
                    break;
                case JsonToken.Comment:
                    break;
                case JsonToken.EndObject:
                    return obj;
            }
        }

        throw new JsonSerializationException("Unexpected end when reading IDictionary<string, object>");
    }

    public override bool CanConvert(Type objectType)
    {
        return typeof(IDictionary<string, object>).IsAssignableFrom(objectType);
    }
}
