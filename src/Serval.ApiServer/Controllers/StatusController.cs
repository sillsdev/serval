namespace Serval.ApiServer.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/status")]
[OpenApiTag("Status")]
public class StatusController : ServalControllerBase
{
    private readonly HealthCheckService _healthCheckService;
    private readonly IWebHostEnvironment _env;

    private readonly IConfiguration _configuration;

    public StatusController(
        HealthCheckService healthCheckService,
        IAuthorizationService authService,
        IWebHostEnvironment env,
        IConfiguration configuration
    )
        : base(authService)
    {
        _healthCheckService = healthCheckService;
        _env = env;
        _configuration = configuration;
    }

    /// <summary>
    /// Get Health
    /// </summary>
    /// <remarks>Provides an indication about the health of the API</remarks>
    /// <response code="200">The API health status</response>
    /// <response code="401">The client is not authenticated</response>
    /// <response code="403">The authenticated client cannot perform the operation</response>
    [Authorize(Scopes.ReadStatus)]
    [HttpGet("health")]
    [OutputCache(PolicyName = "CacheHealthStatus")]
    [ProducesResponseType(typeof(HealthReportDto), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<HealthReportDto>> GetHealthAsync()
    {
        var report = await _healthCheckService.CheckHealthAsync();
        return Ok(Map(report));
    }

    /// <summary>
    /// Get Summary of Health on Publically available endpoint
    /// </summary>
    /// <remarks>Provides an indication about the health of the API</remarks>
    /// <response code="200">The API health status</response>
    [HttpGet("health-public")]
    [OutputCache(PolicyName = "CacheHealthStatus")]
    [ProducesResponseType(typeof(HealthReportDto), (int)HttpStatusCode.OK)]
    public async Task<ActionResult<HealthReportDto>> GetPublicHealthAsync()
    {
        var report = await _healthCheckService.CheckHealthAsync();
        HealthReportDto reportDto = Map(report);

        // remove results as this is a public endpoint
        reportDto.Results = new Dictionary<string, HealthReportEntryDto>();

        return Ok(reportDto);
    }

    /// <summary>
    /// Application Version
    /// </summary>
    /// <remarks>Provides the version of the application</remarks>
    /// <response code="200">Application Version</response>
    /// <response code="401">The client is not authenticated</response>
    /// <response code="403">The authenticated client cannot perform the operation</response>
    [Authorize(Scopes.ReadStatus)]
    [HttpGet("deployment-info")]
    [ProducesResponseType(typeof(DeploymentInfoDto), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    public ActionResult<DeploymentInfoDto> GetDeploymentInfo()
    {
        string deploymentVersion = _configuration.GetValue<string>("DeploymentVersion") ?? "Unknown";
        return Ok(
            new DeploymentInfoDto
            {
                DeploymentVersion = deploymentVersion,
                AspNetCoreEnvironment = _env.EnvironmentName
            }
        );
    }

    private static HealthReportDto Map(HealthReport healthReport)
    {
        return new HealthReportDto
        {
            Status = healthReport.Status.ToString(),
            Results = healthReport.Entries.ToDictionary(f => f.Key, f => Map(f.Value)),
            TotalDuration = healthReport.TotalDuration.ToString()
        };
    }

    private static HealthReportEntryDto Map(HealthReportEntry healthReportEntry)
    {
        return new HealthReportEntryDto
        {
            Status = healthReportEntry.Status.ToString(),
            Duration = healthReportEntry.Duration.ToString(),
            Description = healthReportEntry.Description,
            Exception = healthReportEntry.Exception?.ToString(),
            Data =
                healthReportEntry.Data.Count == 0
                    ? null
                    : healthReportEntry.Data.ToDictionary(f => f.Key, f => f.Value.ToString() ?? string.Empty)
        };
    }
}
