using Microsoft.Extensions.DependencyInjection;

namespace Serval.Shared.Controllers;

public class BugsnagExceptionFilter : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        Bugsnag.IClient? client = context.HttpContext.RequestServices.GetService<Bugsnag.IClient>();
        client?.Notify(context.Exception);
    }
}
