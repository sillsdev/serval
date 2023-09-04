namespace Serval.Shared.Controllers;

public class AbortedRpcExceptionFilter : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is RpcException rpcException && rpcException.StatusCode == StatusCode.Aborted)
        {
            context.Result = new StatusCodeResult(StatusCodes.Status409Conflict);
            context.ExceptionHandled = true;
        }
    }
}
