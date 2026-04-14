namespace Serval.ApiServer.Dtos;

public record DeploymentInfoDto
{
    public required string DeploymentVersion { get; init; }
    public required string AspNetCoreEnvironment { get; init; }
}
