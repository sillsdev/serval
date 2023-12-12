namespace Serval.ApiServer.Contracts;

public class DeploymentInfoDto
{
    public string ServalServerVersion { get; set; } = "Unknown";
    public string AspNetCoreEnvironment { get; set; } = "Unknown";
}
