namespace Serval.Shared.Controllers;

public class ConflictExceptionFilter : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is ConflictException)
        {
            context.Result = new ConflictResult();
            context.ExceptionHandled = true;
        }
    }
}
