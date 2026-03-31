namespace Serval.ApiServer.Dtos;

public class DeploymentInfoDto
{
    public string DeploymentVersion { get; set; } = "Unknown";
    public string AspNetCoreEnvironment { get; set; } = "Unknown";
}
