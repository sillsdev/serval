namespace Serval.Shared.Controllers;

[ApiController]
[Produces("application/json")]
[TypeFilter(typeof(OperationCancelledExceptionFilter))]
[TypeFilter(typeof(NotSupportedExceptionFilter))]
[TypeFilter(typeof(ServiceUnavailableExceptionFilter))]
[TypeFilter(typeof(ErrorResultFilter))]
[TypeFilter(typeof(AbortedRpcExceptionFilter))]
[TypeFilter(typeof(NotFoundExceptionFilter))]
[TypeFilter(typeof(ForbiddenExceptionFilter))]
[TypeFilter(typeof(BadRequestExceptionFilter))]
public abstract class ServalControllerBase(IAuthorizationService authService) : Controller
{
    private readonly IAuthorizationService _authService = authService;

    protected string Owner => User.Identity!.Name!;

    protected async Task AuthorizeAsync(IOwnedEntity ownedEntity)
    {
        AuthorizationResult result = await _authService.AuthorizeAsync(User, ownedEntity, "IsOwner");
        if (!result.Succeeded)
            throw new ForbiddenException();
    }
}
