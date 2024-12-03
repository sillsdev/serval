namespace SIL.ServiceToolkit.Services;

/// <summary>
/// The Bugsnag AspNetCore middleware.
///
/// See https://github.com/bugsnag/bugsnag-dotnet for original source.
/// </summary>
public class BugsnagMiddleware(RequestDelegate requestDelegate)
{
    public const string HttpContextItemsKey = "Bugsnag.Client";

    private readonly RequestDelegate _next = requestDelegate;

    public async Task Invoke(HttpContext context, Bugsnag.IClient client)
    {
        if (client.Configuration.AutoCaptureSessions)
            client.SessionTracking.CreateSession();

        // capture the request information now as the http context
        // may be changed by other error handlers after an exception
        // has occurred
        Bugsnag.Payload.Request bugsnagRequestInformation = ToRequest(context);

        client.BeforeNotify(report =>
        {
            report.Event.Request = bugsnagRequestInformation;
        });

        context.Items[HttpContextItemsKey] = client;

        if (client.Configuration.AutoNotify)
        {
            try
            {
                await _next(context);
            }
            catch (Exception exception)
            {
                client.Notify(exception, Bugsnag.Payload.HandledState.ForUnhandledException());
                throw;
            }
        }
        else
        {
            await _next(context);
        }
    }

    private static Bugsnag.Payload.Request ToRequest(HttpContext httpContext)
    {
        IPAddress? ip = httpContext.Connection.RemoteIpAddress ?? httpContext.Connection.LocalIpAddress;

        return new Bugsnag.Payload.Request
        {
            ClientIp = ip?.ToString(),
            Headers = httpContext.Request.Headers.ToDictionary(x => x.Key, x => string.Join(",", x.Value!)),
            HttpMethod = httpContext.Request.Method,
            Url = httpContext.Request.GetDisplayUrl(),
            Referer = httpContext.Request.Headers[HeaderNames.Referer],
        };
    }
}
