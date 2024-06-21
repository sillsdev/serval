namespace SIL.DataAccess;

public class ReadOnlyCollectionConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        if (objectType.IsArray)
            return false;

        if (objectType.IsGenericType && objectType.GetGenericTypeDefinition() == typeof(List<>))
            return false;

        return objectType
            .GetInterfaces()
            .Where(i => i.IsGenericType)
            .Any(i => i.GetGenericTypeDefinition() == typeof(IReadOnlyList<>));
    }

    public override object? ReadJson(
        JsonReader reader,
        Type objectType,
        object? existingValue,
        JsonSerializer serializer
    )
    {
        throw new NotImplementedException();
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        if (value is null)
        {
            writer.WriteNull();
        }
        else
        {
            writer.WriteStartArray();
            foreach (object item in (IEnumerable)value)
                serializer.Serialize(writer, item);
            writer.WriteEndArray();
        }
    }

    public override bool CanRead => false;
}
