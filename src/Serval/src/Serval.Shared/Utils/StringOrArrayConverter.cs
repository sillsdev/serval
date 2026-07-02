namespace Serval.Shared.Utils;

/// <summary>
/// Allows a string or an array of strings to be serialized and deserialized into a string array.
/// </summary>
public class StringOrArrayConverter : JsonConverter<string[]>
{
    /// <inheritdoc/>
    public override string[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.String when reader.GetString() is { } value => [value],
            JsonTokenType.StartArray => JsonSerializer.Deserialize<string[]>(ref reader, options),
            _ => null,
        };

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, string[]? value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value, options);
}
