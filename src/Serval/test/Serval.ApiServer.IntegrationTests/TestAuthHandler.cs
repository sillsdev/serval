namespace Serval.ApiServer;

public class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration configuration
) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private readonly IConfiguration _configuration = configuration;

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string scope = Context.Request.Headers["Scope"][0]!;
        string authority = $"https://{_configuration["Auth:Domain"]}/";
        Claim[] claims =
        [
            new Claim(ClaimTypes.NameIdentifier, "client1", null, authority),
            new Claim("scope", scope, null, authority)
        ];
        var identity = new ClaimsIdentity(claims, "Test", ClaimTypes.NameIdentifier, null);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "TestScheme");

        var result = AuthenticateResult.Success(ticket);

        return Task.FromResult(result);
    }
}
