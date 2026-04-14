namespace Serval.Shared.Controllers;

public class OperationCanceledExceptionFilter(ILoggerFactory loggerFactory) : ExceptionFilterAttribute
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<OperationCanceledExceptionFilter>();

    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is OperationCanceledException)
        {
            _logger.LogInformation(
                "Request {RequestMethod}:{RequestPath} was canceled",
                context.HttpContext.Request.Method,
                context.HttpContext.Request.Path
            );
            context.ExceptionHandled = true;
            context.Result = new StatusCodeResult(499);
        }
    }
}
