namespace Serval.ApiServer;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddRouting(o => o.LowercaseUrls = true);

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

        services.AddAuthorization(o =>
        {
            foreach (string scope in Scopes.All)
                o.AddPolicy(scope, policy => policy.Requirements.Add(new HasScopeRequirement(scope, authority)));
            o.AddPolicy("IsOwner", policy => policy.Requirements.Add(new IsOwnerRequirement()));
        });
        services.AddSingleton<IAuthorizationHandler, HasScopeHandler>();
        services.AddSingleton<IAuthorizationHandler, IsEntityOwnerHandler>();

        services.AddGrpc();

        services.AddServal(
            serval =>
            {
                serval.AddMongoDataAccess(mongo =>
                {
                    mongo.AddTranslationRepositories();
                    mongo.AddDataFilesRepositories();
                    mongo.AddWebhooksRepositories();
                });
                serval.AddTranslation();
                serval.AddDataFiles();
                serval.AddWebhooks();
            },
            Configuration
        );

        services.AddMediator(cfg =>
        {
            cfg.AddTranslationConsumers();
            cfg.AddDataFilesConsumers();
            cfg.AddWebhooksConsumers();
        });

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
            });
        }
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.UseHttpsRedirection();
        app.UseStaticFiles();

        app.UseAuthentication();

        app.UseRouting();
        app.UseAuthorization();
        app.UseEndpoints(x =>
        {
            x.MapControllers();
            x.MapServalTranslationServices();
        });

        app.UseOpenApi();
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
    }
}
