using System.Diagnostics;

namespace Serval.Shared.Controllers
{
    public class ErrorResultFilter : IAlwaysRunResultFilter
    {
        private readonly ILogger _logger;

        public ErrorResultFilter(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ErrorResultFilter>();
        }

        public void OnResultExecuted(ResultExecutedContext context)
        {
            if (context.HttpContext.Response.StatusCode >= 400)
            {
                _logger.LogInformation(
                    $"Client {((Controller)context.Controller).User.Identity?.Name?.ToString()} made request:\n {JsonSerializer.Serialize(((Controller)context.Controller).ControllerContext.RouteData.Values, new JsonSerializerOptions { WriteIndented = true })}.\n Serval responded with code {context.HttpContext.Response.StatusCode}. Trace: {Activity.Current?.Id}"
                );
            }
        }

        public void OnResultExecuting(ResultExecutingContext context) { }
    }
}
