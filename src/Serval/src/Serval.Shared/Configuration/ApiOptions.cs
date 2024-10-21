namespace Serval.Shared.Configuration;

public class ApiOptions
{
    public const string Key = "Api";

    public TimeSpan DefaultHttpRequestTimeout { get; set; } = TimeSpan.FromSeconds(58); // must be less than 60 seconds Cloudflare timeout
    public TimeSpan LongPollTimeout { get; set; } = TimeSpan.FromSeconds(40); // must be less than DefaultHttpRequestTimeout
    public string ServalVersion { get; set; } = "1.0.0"; // Default value
}
