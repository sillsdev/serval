namespace Serval.Machine.Shared.Configuration;

public class DistributedReaderWriterLockOptions
{
    public const string Key = "DistributedReaderWriterLock";

    public TimeSpan DefaultLifetime { get; set; } = TimeSpan.FromSeconds(56); // must be less than DefaultHttpRequestTimeout
}
