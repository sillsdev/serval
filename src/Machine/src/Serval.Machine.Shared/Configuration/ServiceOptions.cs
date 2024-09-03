namespace Serval.Machine.Shared.Configuration;

public class ServiceOptions
{
    public const string Key = "Service";

    public string ServiceId { get; set; } = "machine_api";
    public TimeSpan ReadWriteLockTimeout { get; set; } = TimeSpan.FromSeconds(55);
}
