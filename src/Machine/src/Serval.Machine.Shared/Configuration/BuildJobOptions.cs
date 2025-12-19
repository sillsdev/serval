namespace Serval.Machine.Shared.Configuration;

public class BuildJobOptions
{
    public const string Key = "BuildJob";

    public IList<ClearMLBuildQueue> ClearML { get; set; } = new List<ClearMLBuildQueue>();
    public bool PreserveBuildFiles { get; set; } = false;
    public int MaxWarnings { get; set; } = 1000;
}
