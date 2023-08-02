using Newtonsoft.Json.Linq;

namespace Serval.Shared.Controllers;

public class ServiceUnavailableException : ExceptionFilterAttribute
{
    private readonly ILogger<ServiceUnavailableException> _logger;

    public ServiceUnavailableException(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<ServiceUnavailableException>();
    }

    public override void OnException(ExceptionContext context)
    {
        if (
            (context.Exception is System.TimeoutException)
            || (context.Exception is Grpc.Core.RpcException rpcEx && rpcEx.StatusCode == StatusCode.Unavailable)
        )
        {
            _logger.Log(
                LogLevel.Error,
                "A user tried to access an unavailable service. See health-check logs for more details.",
                context.Exception
            );
            context.Result = new StatusCodeResult(StatusCodes.Status503ServiceUnavailable);
            context.ExceptionHandled = true;
        }
    }
}
