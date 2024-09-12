namespace Serval.Shared.Controllers;

public class ErrorResultFilter(ILoggerFactory loggerFactory) : IAlwaysRunResultFilter
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<ErrorResultFilter>();
    private static readonly JsonSerializerOptions JsonSerializerOptions = new() { WriteIndented = true };

    public void OnResultExecuted(ResultExecutedContext context)
    {
        if (context.HttpContext.Response.StatusCode >= 500)
        {
            _logger.LogInformation(
                "Client {client} made request:\n {request}.\n Serval responded with code {statusCode}. Trace: {activityId}",
                ((Controller)context.Controller).User.Identity?.Name?.ToString(),
                JsonSerializer.Serialize(
                    ((Controller)context.Controller).ControllerContext.RouteData.Values,
                    JsonSerializerOptions
                ),
                context.HttpContext.Response.StatusCode,
                Activity.Current?.Id
            );
        }
    }

    public void OnResultExecuting(ResultExecutingContext context) { }
}
