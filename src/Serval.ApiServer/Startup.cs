namespace Serval.ApiServer;

public class Startup
{
    public Startup(IConfiguration configuration, IWebHostEnvironment environment)
    {
        Configuration = configuration;
        Environment = environment;
    }

    public IConfiguration Configuration { get; }

    public IWebHostEnvironment Environment { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddRouting(o => o.LowercaseUrls = true);

        services.AddOutputCache();

        services
            .AddControllers()
            .AddJsonOptions(o =>
            {
                o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            });

        string authority = $"https://{Configuration["Auth:Domain"]}/";
        services
            .AddAuthentication(o =>
            {
                o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(o =>
            {
                o.Authority = authority;
                o.Audience = Configuration["Auth:Audience"];
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = ClaimTypes.NameIdentifier
                };
            });
        services.AddHealthChecks().AddIdentityServer(new Uri(authority), name: "Auth0");

        var dataFileOptions = new DataFileOptions();
        Configuration.GetSection(DataFileOptions.Key).Bind(dataFileOptions);
        // find drive letter for DataFilesDir
        string driveLetter = Path.GetPathRoot(dataFileOptions.FilesDirectory)![..1];

        // add health check for disk storage capacity
        services
            .AddHealthChecks()
            .AddDiskStorageHealthCheck(
                x => x.AddDrive(driveLetter, 1_000), // 1GB
                "Data File Storage Capacity",
                HealthStatus.Degraded
            );

        services.AddAuthorization(o =>
        {
            foreach (string scope in Scopes.All)
                o.AddPolicy(scope, policy => policy.Requirements.Add(new HasScopeRequirement(scope, authority)));
            o.AddPolicy("IsOwner", policy => policy.Requirements.Add(new IsOwnerRequirement()));
        });
        services.AddSingleton<IAuthorizationHandler, HasScopeHandler>();
        services.AddSingleton<IAuthorizationHandler, IsEntityOwnerHandler>();

        services.AddGrpc();

        services
            .AddServal(Configuration)
            .AddMongoDataAccess(cfg =>
            {
                cfg.AddTranslationRepositories();
                cfg.AddDataFilesRepositories();
                cfg.AddWebhooksRepositories();
            })
            .AddTranslation()
            .AddDataFiles()
            .AddWebhooks();
        services.AddTransient<IUrlService, UrlService>();

        services.AddHangfire(
            c =>
                c.SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
                    .UseSimpleAssemblyNameTypeSerializer()
                    .UseRecommendedSerializerSettings()
                    .UseMongoStorage(
                        Configuration.GetConnectionString("Hangfire"),
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
        services.AddHangfireServer();

        services.AddMediator(cfg =>
        {
            cfg.AddTranslationConsumers();
            cfg.AddDataFilesConsumers();
            cfg.AddWebhooksConsumers();
        });
        services.AddScoped<IPublishEndpoint>(sp => sp.GetRequiredService<IScopedMediator>());
        services.AddScoped(sp => sp.GetRequiredService<IScopedMediator>().CreateRequestClient<GetDataFile>());

        services
            .AddApiVersioning(o =>
            {
                o.AssumeDefaultVersionWhenUnspecified = false;
                o.DefaultApiVersion = new ApiVersion(1, 0);
                o.ReportApiVersions = true;
                o.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            .AddMvc()
            .AddApiExplorer(o =>
            {
                o.DefaultApiVersion = new ApiVersion(1, 0);
                o.GroupNameFormat = "'v'VVV";
                o.SubstituteApiVersionInUrl = true;
            });

        services.AddEndpointsApiExplorer();
        var versions = new[] { new Version(1, 0) };
        foreach (Version version in versions)
        {
            services.AddSwaggerDocument(o =>
            {
                o.SchemaType = SchemaType.Swagger2;
                o.Title = "Serval API";
                o.Description = "Natural language processing services for minority language Bible translation.";
                o.DocumentName = "v" + version.Major;
                o.ApiGroupNames = new[] { "v" + version.Major };
                o.Version = version.Major + "." + version.Minor;

                o.SchemaNameGenerator = new ServalSchemaNameGenerator();
                o.UseControllerSummaryAsTagDescription = true;
                o.AddSecurity(
                    "bearer",
                    Enumerable.Empty<string>(),
                    new OpenApiSecurityScheme
                    {
                        Type = OpenApiSecuritySchemeType.OAuth2,
                        Description = "Auth0 Client Credentials Flow",
                        Flow = OpenApiOAuth2Flow.Application,
                        Flows = new OpenApiOAuthFlows
                        {
                            ClientCredentials = new OpenApiOAuthFlow
                            {
                                AuthorizationUrl = $"{authority}authorize",
                                TokenUrl = $"{authority}oauth/token"
                            }
                        },
                    }
                );
                o.OperationProcessors.Add(new AspNetCoreOperationSecurityScopeProcessor("bearer"));

                o.AllowReferencesWithProperties = true;
                o.PostProcess = document =>
                {
                    var prefix = "/api/v" + version.Major;
                    document.Servers.Add(new OpenApiServer { Url = prefix });
                    foreach (var pair in document.Paths.ToArray())
                    {
                        document.Paths.Remove(pair.Key);
                        document.Paths[pair.Key[prefix.Length..]] = pair.Value;
                    }
                };
            });
        }
        if (Environment.IsDevelopment())
            services
                .AddOpenTelemetry()
                .WithTracing(builder =>
                {
                    builder
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddGrpcClientInstrumentation()
                        .AddSource("MongoDB.Driver.Core.Extensions.DiagnosticSources")
                        .AddConsoleExporter();
                });
        else
        {
            services
                .AddOpenTelemetry()
                .WithMetrics(opts => opts.AddAspNetCoreInstrumentation().AddPrometheusExporter());
        }
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.UseStaticFiles();

        app.UseAuthentication();

        app.UseRouting();
        app.UseAuthorization();
        app.UseEndpoints(x =>
        {
            x.MapControllers();
            x.MapServalTranslationServices();
            x.MapHangfireDashboard();
            x.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = WriteHealthCheckResponse.Generate });
        });

        app.UseOutputCache();

        app.UseOpenApi(o =>
        {
            o.PostProcess = (document, request) =>
            {
                // Patch server URL for Swagger UI
                var prefix = "/api/v" + document.Info.Version.Split('.')[0];
                document.Servers.First().Url += prefix;
            };
        });
        app.UseSwaggerUi3(settings =>
        {
            settings.OAuth2Client = new OAuth2ClientSettings
            {
                AppName = "Auth0 M2M App",
                AdditionalQueryStringParameters = { { "audience", Configuration["Auth:Audience"] } }
            };
            if (env.IsDevelopment())
            {
                settings.OAuth2Client.ClientId = Configuration["TestClientId"];
                settings.OAuth2Client.ClientSecret = Configuration["TestClientSecret"];
            }

            settings.CustomJavaScriptPath = "js/auth0.js";
        });

        if (!Environment.IsDevelopment())
            app.UseOpenTelemetryPrometheusScrapingEndpoint();
    }
}
