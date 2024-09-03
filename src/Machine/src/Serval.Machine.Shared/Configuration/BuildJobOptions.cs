namespace Serval.Machine.Shared.Configuration;

public class BuildJobOptions
{
    public const string Key = "BuildJob";

    public IList<ClearMLBuildQueue> ClearML { get; set; } = new List<ClearMLBuildQueue>();
    public TimeSpan PostProcessLockLifetime { get; set; } = TimeSpan.FromSeconds(120);
}
