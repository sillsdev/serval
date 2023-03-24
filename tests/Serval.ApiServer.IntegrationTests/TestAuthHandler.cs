namespace Serval.ApiServer;

public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IConfiguration _configuration;

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock,
        IConfiguration configuration
    )
        : base(options, logger, encoder, clock)
    {
        _configuration = configuration;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string scope = Context.Request.Headers["Scope"][0];
        string authority = $"https://{_configuration["Auth:Domain"]}/";
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "client1", null, authority),
            new Claim("scope", scope, null, authority)
        };
        var identity = new ClaimsIdentity(claims, "Test", ClaimTypes.NameIdentifier, null);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "TestScheme");

        var result = AuthenticateResult.Success(ticket);

        return Task.FromResult(result);
    }
}
