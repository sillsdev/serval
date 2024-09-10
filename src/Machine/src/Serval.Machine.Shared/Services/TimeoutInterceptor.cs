namespace Serval.Machine.Shared.Services;

public class TimeoutInterceptor(ILogger<TimeoutInterceptor> logger) : Interceptor
{
    private readonly ILogger<TimeoutInterceptor> _logger = logger;

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
        catch (TimeoutException te)
        {
            _logger.LogError(te, "The method {Method} took too long to complete.", context.Method);
            throw new RpcException(new Status(StatusCode.Unavailable, "The method took too long to complete."));
        }
    }
}
