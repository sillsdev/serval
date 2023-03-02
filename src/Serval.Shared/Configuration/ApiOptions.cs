namespace Serval.Shared.Configuration;

public class ApiOptions
{
    public const string Key = "Api";

    public TimeSpan LongPollTimeout { get; set; } = TimeSpan.FromSeconds(40);
}
