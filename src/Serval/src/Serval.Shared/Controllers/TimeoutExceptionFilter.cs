namespace Serval.Shared.Controllers;

public class TimeoutExceptionFilter(ILoggerFactory loggerFactory) : ExceptionFilterAttribute
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<TimeoutExceptionFilter>();

    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is TimeoutException)
        {
            _logger.LogError(
                context.Exception,
                "Request {RequestMethod}:{RequestPath} timed out",
                context.HttpContext.Request.Method,
                context.HttpContext.Request.Path
            );
        }
    }
}
