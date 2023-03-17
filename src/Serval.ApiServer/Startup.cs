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
                serval.AddMongoDataAccess(
                    Configuration.GetConnectionString("Mongo"),
                    mongo =>
                    {
                        mongo.AddTranslationRepositories();
                        mongo.AddDataFilesRepositories();
                        mongo.AddWebhooksRepositories();
                    }
                );
                serval.AddTranslation();
                serval.AddDataFiles();
                serval.AddWebhooks();
            },
            Configuration
        );
        services.AddScoped<IEventBroker, EventBroker>();
        services.AddScoped<IDataFileRetriever, DataFileRetriever>();

        services.AddEndpointsApiExplorer();
        services.AddSwaggerDocument(doc =>
        {
            doc.SchemaType = SchemaType.OpenApi3;
            doc.Title = "Serval API";
            doc.SchemaNameGenerator = new ServalSchemaNameGenerator();
            doc.UseControllerSummaryAsTagDescription = true;
            doc.AddSecurity(
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
            doc.OperationProcessors.Add(new AspNetCoreOperationSecurityScopeProcessor("bearer"));
        });
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
