namespace Serval.ApiServer.Contracts;

public class DeploymentInfoDto
{
    public string DeploymentVersion { get; set; } = "Unknown";
    public string AspNetCoreEnvironment { get; set; } = "Unknown";
}
