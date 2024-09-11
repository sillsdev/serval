using Microsoft.Extensions.DependencyInjection;

namespace Serval.Shared.Controllers;

public class BugsnagExceptionFilter : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        context.HttpContext.RequestServices.GetService<Bugsnag.IClient>()?.Notify(context.Exception);
    }
}
