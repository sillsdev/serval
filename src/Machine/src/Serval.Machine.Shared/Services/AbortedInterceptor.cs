namespace Serval.Machine.Shared.Services;

public class AbortedInterceptor : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation
    )
    {
        try
        {
            return await continuation(request, context);
        }
        catch (EngineNotBuiltException e)
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition, e.Message, e));
        }
    }
}
