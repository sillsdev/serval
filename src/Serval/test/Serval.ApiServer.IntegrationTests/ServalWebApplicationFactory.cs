using Serval.Translation.Configuration;
using Serval.WordAlignment.Configuration;

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

            services.Configure<ApiOptions>(options => options.LongPollTimeout = TimeSpan.FromSeconds(1));

            services.Configure<TranslationOptions>(options =>
            {
                options.Engines =
                [
                    new Translation.Configuration.EngineInfo { Type = "Echo" },
                    new Translation.Configuration.EngineInfo { Type = "Nmt" },
                ];
            });

            services.Configure<WordAlignmentOptions>(options =>
            {
                options.Engines =
                [
                    new WordAlignment.Configuration.EngineInfo { Type = "EchoWordAlignment" },
                    new WordAlignment.Configuration.EngineInfo { Type = "Statistical" },
                ];
            });

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
                                BackupStrategy = new CollectionMongoBackupStrategy(),
                            },
                            CheckConnection = true,
                            CheckQueuedJobsStrategy = CheckQueuedJobsStrategy.TailNotificationsCollection,
                        }
                    )
            );
        });
    }
}
