namespace Serval.Shared.Controllers;

public class ErrorResultFilter(ILoggerFactory loggerFactory) : IAlwaysRunResultFilter
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<ErrorResultFilter>();
    private static readonly JsonSerializerOptions s_jsonSerializerOptions = new() { WriteIndented = true };

    public void OnResultExecuted(ResultExecutedContext context)
    {
        if (context.HttpContext.Response.StatusCode >= 400)
        {
            _logger.LogInformation(
                "Client {client} made request:\n {request}.\n Serval responded with code {statusCode}. Trace: {activityId}",
                ((Controller)context.Controller).User.Identity?.Name?.ToString(),
                JsonSerializer.Serialize(
                    ((Controller)context.Controller).ControllerContext.RouteData.Values,
                    s_jsonSerializerOptions
                ),
                context.HttpContext.Response.StatusCode,
                Activity.Current?.Id
            );
        }
    }

    public void OnResultExecuting(ResultExecutingContext context) { }
}
