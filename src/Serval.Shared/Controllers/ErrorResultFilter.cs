using System.Diagnostics;
using System.Text.Json;

namespace Serval.Shared.Controllers
{
    public class ErrorResultFilter : ResultFilterAttribute
    {
        private readonly ILogger _logger;

        public ErrorResultFilter(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ErrorResultFilter>();
        }

        public override Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
        {
            if ((context.Result is ObjectResult r) && (r.StatusCode >= 400))
            {
                _logger.LogInformation(
                    $"Client {((Controller)context.Controller).User.Identity?.Name?.ToString()} made request:\n {JsonSerializer.Serialize(((Controller)context.Controller).ControllerContext.RouteData.Values, new JsonSerializerOptions { WriteIndented = true })}.\n Serval responded with code {r.StatusCode}. Trace: {Activity.Current?.Id}"
                );
            }
            return base.OnResultExecutionAsync(context, next);
        }
    }
}