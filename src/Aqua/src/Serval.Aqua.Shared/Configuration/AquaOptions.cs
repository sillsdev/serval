namespace Serval.Aqua.Shared.Configuration;

public class AquaOptions
{
    public const string Key = "Aqua";

    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public bool JobPollingEnabled { get; set; } = false;
    public TimeSpan JobPollingTimeout { get; set; } = TimeSpan.FromSeconds(10);
}
