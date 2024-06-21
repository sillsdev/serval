namespace Serval.ApiServer;

public class ServalWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services
                .AddAuthentication(o =>
                {
                    o.DefaultAuthenticateScheme = "TestScheme";
                    o.DefaultChallengeScheme = "TestScheme";
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("TestScheme", options => { });

            services.Configure<MongoDataAccessOptions>(options =>
                options.Url = new MongoUrl("mongodb://localhost:27017/serval_test")
            );

            services.AddHangfire(c =>
                c.SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
                    .UseSimpleAssemblyNameTypeSerializer()
                    .UseRecommendedSerializerSettings()
                    .UseMongoStorage(
                        "mongodb://localhost:27017/serval_test_jobs",
                        new MongoStorageOptions
                        {
                            MigrationOptions = new MongoMigrationOptions
                            {
                                MigrationStrategy = new MigrateMongoMigrationStrategy(),
                                BackupStrategy = new CollectionMongoBackupStrategy()
                            },
                            CheckConnection = true,
                            CheckQueuedJobsStrategy = CheckQueuedJobsStrategy.TailNotificationsCollection
                        }
                    )
            );
        });
    }
}
