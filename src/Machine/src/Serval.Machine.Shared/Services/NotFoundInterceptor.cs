﻿namespace Serval.Machine.Shared.Services;

public class NotFoundInterceptor : Interceptor
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
        catch (EngineNotFoundException e)
        {
            throw new RpcException(new Status(StatusCode.NotFound, e.Message, e));
        }
    }
}
