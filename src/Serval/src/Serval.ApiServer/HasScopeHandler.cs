namespace Serval.ApiServer;

public class HasScopeHandler : AuthorizationHandler<HasScopeRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, HasScopeRequirement requirement)
    {
        Claim? scopeClaim = context.User.FindFirst(c => c.Type == "scope" && requirement.Issuers.Contains(c.Issuer));
        if (scopeClaim is not null)
        {
            var scopes = scopeClaim.Value.Split(' ').ToHashSet();
            if (scopes.Contains(requirement.Scope))
                context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
