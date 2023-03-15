namespace Serval.Shared.Controllers;

[ApiController]
[Produces("application/json")]
[TypeFilter(typeof(OperationCancelledExceptionFilter))]
[TypeFilter(typeof(NotSupportedExceptionFilter))]
public class ServalControllerBase : Controller
{
    private readonly IAuthorizationService _authService;

    protected ServalControllerBase(IAuthorizationService authService)
    {
        _authService = authService;
    }

    protected string Owner => User.Identity!.Name!;

    protected async Task<bool> AuthorizeIsOwnerAsync(IOwnedEntity ownedEntity)
    {
        AuthorizationResult result = await _authService.AuthorizeAsync(User, ownedEntity, "IsOwner");
        return result.Succeeded;
    }
}
