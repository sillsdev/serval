namespace Serval.Shared.Controllers;

public class FailedPreconditionRpcExceptionFilter : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is RpcException rpcException && rpcException.StatusCode == StatusCode.FailedPrecondition)
        {
            context.Result = new ConflictObjectResult(rpcException.Message);
            context.ExceptionHandled = true;
        }
    }
}
