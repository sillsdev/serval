namespace Serval.Shared.Configuration;

public class TimeoutOptions
{
    public const string Key = "Api";

    public TimeSpan DefaultHttpRequestTimeout { get; set; } = TimeSpan.FromSeconds(10);
    public TimeSpan LongHttpRequestTimeout { get; set; } = TimeSpan.FromSeconds(58); // must be less than 60 seconds Cloudflare timeout
    public TimeSpan LongPollTimeout { get; set; } = TimeSpan.FromSeconds(40); // must be less than LongHttpRequestTimeout
    public TimeSpan DefaultLockTimeout { get; set; } = TimeSpan.FromSeconds(8); // must be less than DefaultHttpRequestTimeout
    public TimeSpan DefaultLockLifetime { get; set; } = TimeSpan.FromSeconds(6); // must be less than DefaultLockTimeout
    public TimeSpan LongHttpLockLifetime { get; set; } = TimeSpan.FromSeconds(56); // must be less than HttpLongRequestTimeout
    public TimeSpan LongProcessLockLifetime { get; set; } = TimeSpan.FromSeconds(120);
}
