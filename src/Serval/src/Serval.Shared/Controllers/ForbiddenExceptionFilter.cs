namespace Serval.Shared.Controllers;

public class ForbiddenExceptionFilter : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is ForbiddenException)
        {
            context.Result = new ForbidResult();
            context.ExceptionHandled = true;
        }
    }
}
