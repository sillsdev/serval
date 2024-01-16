namespace Serval.Shared.Controllers;

public class ServiceUnavailableExceptionFilter : ExceptionFilterAttribute
{
    private readonly ILogger<ServiceUnavailableExceptionFilter> _logger;

    public ServiceUnavailableExceptionFilter(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<ServiceUnavailableExceptionFilter>();
    }

    public override void OnException(ExceptionContext context)
    {
        if (
            (context.Exception is TimeoutException)
            || (context.Exception is RpcException rpcEx && rpcEx.StatusCode == StatusCode.Unavailable)
        )
        {
            _logger.Log(LogLevel.Error, context.Exception, "A user tried to access an unavailable service.");
            context.Result = new StatusCodeResult(StatusCodes.Status503ServiceUnavailable);
            context.ExceptionHandled = true;
        }
    }
}
