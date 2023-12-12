namespace Serval.ApiServer.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/status")]
[OpenApiTag("Status")]
public class StatusController : ServalControllerBase
{
    private readonly HealthCheckService _healthCheckService;
    private readonly IWebHostEnvironment _env;

    public StatusController(
        HealthCheckService healthCheckService,
        IAuthorizationService authService,
        IWebHostEnvironment env
    )
        : base(authService)
    {
        _healthCheckService = healthCheckService;
        _env = env;
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
    [ProducesResponseType(typeof(HealthReportDto), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<HealthReportDto>> GetHealthAsync()
    {
        var report = await _healthCheckService.CheckHealthAsync();
        return Ok(Map(report));
    }

    /// <summary>
    /// Application Version
    /// </summary>
    /// <remarks>Provides the version of the application</remarks>
    /// <response code="200">Application Version</response>
    /// <response code="401">The client is not authenticated</response>
    /// <response code="403">The authenticated client cannot perform the operation</response>
    [Authorize(Scopes.ReadStatus)]
    [HttpGet("deployment_info")]
    [ProducesResponseType(typeof(DeploymentInfoDto), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    public ActionResult<DeploymentInfoDto> GetDeploymentInfo()
    {
        string servalServerVersion = typeof(StatusController).Assembly.GetName().Version?.ToString() ?? "Unknown";
        return Ok(
            new DeploymentInfoDto
            {
                ServalServerVersion = servalServerVersion,
                AspNetCoreEnvironment = _env.EnvironmentName
            }
        );
    }

    private static HealthReportDto Map(HealthReport healthReport)
    {
        return new HealthReportDto
        {
            Status = healthReport.Status.ToString(),
            Entries = healthReport.Entries.ToDictionary(f => f.Key, f => Map(f.Value)),
            TotalDuration = healthReport.TotalDuration.ToString()
        };
    }

    private static HealthReportEntryDto Map(HealthReportEntry healthReportEntry)
    {
        return new HealthReportEntryDto
        {
            Status = healthReportEntry.Status.ToString(),
            Duration = healthReportEntry.Duration.ToString(),
            Description = healthReportEntry.Description ?? string.Empty,
            Exception = healthReportEntry.Exception?.ToString() ?? string.Empty,
            Data = healthReportEntry.Data is null
                ? new Dictionary<string, string>()
                : healthReportEntry.Data.ToDictionary(f => f.Key, f => f.Value.ToString() ?? string.Empty)
        };
    }
}
