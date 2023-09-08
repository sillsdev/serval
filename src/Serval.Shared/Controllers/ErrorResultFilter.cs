using System.Diagnostics;

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
                _logger.LogInformation($"Responded with code {r.StatusCode}. Trace: {Activity.Current?.Id}");
            }
            return base.OnResultExecutionAsync(context, next);
        }
    }
}
