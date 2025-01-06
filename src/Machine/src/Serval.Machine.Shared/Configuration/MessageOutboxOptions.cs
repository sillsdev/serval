namespace Serval.Machine.Shared.Configuration;

public class MessageOutboxOptions
{
    public const string Key = "MessageOutbox";

    public string OutboxDir { get; set; } = "outbox";
    public TimeSpan MessageExpirationTimeout { get; set; } = TimeSpan.FromHours(48);

    public static JsonSerializerOptions JsonSerializerOptions
    {
        get
        {
            JsonSerializerOptions options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            options.AddProtobufSupport();
            return options;
        }
    }
}
