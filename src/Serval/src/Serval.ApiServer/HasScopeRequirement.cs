namespace Serval.ApiServer;

public class HasScopeRequirement(string scope, string issuer) : IAuthorizationRequirement
{
    public string Issuer { get; } = issuer;
    public string Scope { get; } = scope;
}
