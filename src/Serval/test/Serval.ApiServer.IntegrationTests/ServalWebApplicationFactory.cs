namespace Serval.ApiServer;

public class ServalWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(
            (_, config) =>
            {
                config.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Mongo"] = "mongodb://localhost:27017/serval_test",
                    }
                );
            }
        );

        builder.ConfigureServices(services =>
        {
            services
                .AddAuthentication(o =>
                {
                    o.DefaultAuthenticateScheme = "TestSchemeSelector";
                    o.DefaultChallengeScheme = "TestSchemeSelector";
                })
                .AddPolicyScheme(
                    "TestSchemeSelector",
                    "Test Scheme or API Key",
                    o =>
                    {
                        o.ForwardDefaultSelector = ctx =>
                            ctx.Request.Headers.ContainsKey(ApiKeyDefaults.HeaderName)
                                ? ApiKeyDefaults.AuthenticationScheme
                                : "TestScheme";
                    }
                )
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("TestScheme", options => { });

            services.Configure<ApiOptions>(options => options.LongPollTimeout = TimeSpan.FromSeconds(1));
        });
    }
}
