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
                        ["ConnectionStrings:Hangfire"] = "mongodb://localhost:27017/serval_test_jobs",
                    }
                );
            }
        );

        builder.ConfigureServices(services =>
        {
            services
                .AddAuthentication(o =>
                {
                    o.DefaultAuthenticateScheme = "TestScheme";
                    o.DefaultChallengeScheme = "TestScheme";
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("TestScheme", options => { });

            services.Configure<ApiOptions>(options => options.LongPollTimeout = TimeSpan.FromSeconds(1));
        });
    }
}
