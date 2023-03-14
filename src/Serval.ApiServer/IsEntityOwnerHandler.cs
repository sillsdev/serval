﻿using Microsoft.AspNetCore.Authorization;
using Serval.Shared.Entities;

namespace Serval.ApiServer;

public class IsEntityOwnerHandler : AuthorizationHandler<IsOwnerRequirement, IOwnedEntity>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        IsOwnerRequirement requirement,
        IOwnedEntity resource
    )
    {
        if (context.User.Identity?.Name == resource.Owner)
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
