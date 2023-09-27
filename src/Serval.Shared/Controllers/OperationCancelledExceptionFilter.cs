namespace Serval.Shared.Controllers;

public class OperationCancelledExceptionFilter : ExceptionFilterAttribute
{
    private readonly ILogger _logger;

    public OperationCancelledExceptionFilter(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<OperationCancelledExceptionFilter>();
    }

    public override void OnException(ExceptionContext context)
    {
        if (
            context.Exception is OperationCanceledException
            || context.Exception is RpcException rpcEx && rpcEx.StatusCode == StatusCode.Cancelled
        )
        {
            _logger.LogInformation("Request was cancelled");
            context.ExceptionHandled = true;
            context.Result = new StatusCodeResult(499);
        }
    }
}
