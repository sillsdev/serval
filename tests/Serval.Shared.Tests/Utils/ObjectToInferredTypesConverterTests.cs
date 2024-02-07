namespace Serval.Shared.Utils;

[TestFixture]
public class ObjectToInferredTypesConverterTest
{
    private readonly JsonSerializerOptions jsonSerializerOptions;

    public ObjectToInferredTypesConverterTest()
    {
        jsonSerializerOptions = new JsonSerializerOptions { Converters = { new ObjectToInferredTypesConverter() } };
    }

    [Test]
    public void Convert()
    {
        var obj = new Dictionary<string, object>
        {
            ["String"] = "mystring",
            ["Int"] = 12345,
            ["Float"] = 123.45,
            ["Bool"] = true,
            ["Array"] = new List<object> { "one", 2, 3.0, false },
            ["Object"] = new Dictionary<string, object>
            {
                ["String"] = "mystring",
                ["Int"] = 12345,
                ["Float"] = 123.45,
                ["Bool"] = true,
                ["Array"] = new List<object> { "one", 2, 3.0, false }
            }
        };

        string objectAsString = JsonSerializer.Serialize(obj);
        var result = JsonSerializer.Deserialize<IDictionary<string, object>>(objectAsString, jsonSerializerOptions);

        Assert.That(((IList<object>?)((IDictionary<string, object>?)result?["Object"])?["Array"])?[2], Is.EqualTo(3));
    }
}
