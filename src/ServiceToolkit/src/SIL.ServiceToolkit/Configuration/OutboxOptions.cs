namespace SIL.ServiceToolkit.Configuration;

public class OutboxOptions
{
    public const string Key = "MessageOutbox";

    public string OutboxDir { get; set; } = "outbox";
    public TimeSpan MessageExpirationTimeout { get; set; } = TimeSpan.FromHours(48);
    public int HealthyMessageLimit { get; set; } = 25;
}
