using Newtonsoft.Json.Linq;

namespace Serval.Shared.Controllers;

public class ServiceUnavailableException : ExceptionFilterAttribute
{
    private readonly ILogger _logger;

    public ServiceUnavailableException(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<ServiceUnavailableException>();
    }

    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is System.TimeoutException or Grpc.Core.RpcException)
        {
            _logger.Log(
                LogLevel.Error,
                "A user tried to accesss an unavailable service. See health-check logs for more details."
            );
            context.Result = new StatusCodeResult(StatusCodes.Status503ServiceUnavailable);
            context.ExceptionHandled = true;
        }
    }
}
