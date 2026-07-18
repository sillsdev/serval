namespace Serval.ApiServer;

public class HasScopeRequirement(string scope, params string[] issuers) : IAuthorizationRequirement
{
    public IReadOnlySet<string> Issuers { get; } = issuers.ToHashSet();
    public string Scope { get; } = scope;
}
