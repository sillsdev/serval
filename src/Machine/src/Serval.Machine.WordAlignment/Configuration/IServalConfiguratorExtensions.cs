namespace Microsoft.Extensions.DependencyInjection;

public static class IServalConfiguratorExtensions
{
    public static IServalConfigurator AddMachineWordAlignment(this IServalConfigurator configurator)
    {
        configurator.Services.Configure<StatisticalEngineOptions>(
            configurator.Configuration.GetSection(StatisticalEngineOptions.Key)
        );

        configurator.AddWordAlignmentEngineHealthChecks();
        configurator.AddWordAlignmentEngineBuildJobService();
        configurator.AddWordAlignmentEngineDataAccess();
        configurator.AddWordAlignmentEngines();
        return configurator;
    }

    private static IServalConfigurator AddWordAlignmentEngines(this IServalConfigurator configurator)
    {
        configurator.Services.AddKeyedScoped<IPlatformService, ServalWordAlignmentPlatformService>(
            EngineGroup.WordAlignment
        );

        // Statistical Engine
        configurator.Services.AddSingleton<StatisticalEngineStateService>();
        configurator.Services.Configure<ThotWordAlignmentModelOptions>(
            configurator.Configuration.GetSection(ThotWordAlignmentModelOptions.Key)
        );
        configurator.Services.AddSingleton<IWordAlignmentModelFactory, ThotWordAlignmentModelFactory>();
        configurator.AddWordAlignmentEngine<StatisticalEngineService>(nameof(EngineType.Statistical));
        configurator.Services.AddHostedService<StatisticalEngineCommitService>();

        // Echo Engine
        configurator.AddWordAlignmentEngine<EchoWordAlignmentEngineService>(nameof(EngineType.EchoWordAlignment));

        return configurator;
    }

    private static IServalConfigurator AddWordAlignmentEngineBuildJobService(this IServalConfigurator configurator)
    {
        configurator.Services.AddScoped<
            IBuildJobRunner<WordAlignmentEngine>,
            ClearMLBuildJobRunner<WordAlignmentEngine>
        >();
        configurator.Services.AddScoped<IBuildJobService<WordAlignmentEngine>, BuildJobService<WordAlignmentEngine>>();

        configurator.Services.AddScoped<IClearMLBuildJobFactory, StatisticalClearMLBuildJobFactory>();

        configurator.Services.AddSingleton<WordAlignmentEngineClearMLMonitorService>();
        configurator.Services.AddSingleton<IClearMLQueueService<WordAlignmentEngine>>(x =>
            x.GetRequiredService<WordAlignmentEngineClearMLMonitorService>()
        );
        configurator.Services.AddHostedService(p => p.GetRequiredService<WordAlignmentEngineClearMLMonitorService>());

        configurator.Services.AddSingleton<WordAlignmentEngineLocalBuildJobRunner>();
        configurator.Services.AddSingleton<IBuildJobRunner<WordAlignmentEngine>>(sp =>
            sp.GetRequiredService<WordAlignmentEngineLocalBuildJobRunner>()
        );
        configurator.Services.AddHostedService(sp => sp.GetRequiredService<WordAlignmentEngineLocalBuildJobRunner>());
        configurator.Services.AddSingleton<ILocalBuildJobFactory, StatisticalLocalBuildJobFactory>();
        configurator.Services.AddSingleton<ILocalBuildJobFactory, EchoWordAlignmentLocalBuildJobFactory>();
        return configurator;
    }

    public static IServalConfigurator AddWordAlignmentEngineDataAccess(this IServalConfigurator configurator)
    {
        configurator.DataAccess.AddRepository<WordAlignmentEngine>(
            "machine.word_alignment_engines",
            init:
            [
                c =>
                    c.Indexes.CreateOrUpdateAsync(
                        new CreateIndexModel<WordAlignmentEngine>(
                            Builders<WordAlignmentEngine>.IndexKeys.Ascending(e => e.EngineId)
                        )
                    ),
                c =>
                    c.Indexes.CreateOrUpdateAsync(
                        new CreateIndexModel<WordAlignmentEngine>(
                            Builders<WordAlignmentEngine>.IndexKeys.Ascending(e => e.CurrentBuild!.BuildJobRunner)
                        )
                    ),
            ]
        );
        return configurator;
    }

    private static IServalConfigurator AddWordAlignmentEngineHealthChecks(this IServalConfigurator configurator)
    {
        var statisticalEngineOptions = new StatisticalEngineOptions();
        configurator.Configuration.GetSection(StatisticalEngineOptions.Key).Bind(statisticalEngineOptions);
        string? statisticsDriveLetter = Path.GetPathRoot(statisticalEngineOptions.EnginesDir)?[..1];
        if (statisticsDriveLetter is null)
            throw new InvalidOperationException("Statistical Engine directory is required");
        // add health check for disk storage capacity
        configurator
            .Services.AddHealthChecks()
            .AddDiskStorageHealthCheck(
                x => x.AddDrive(statisticsDriveLetter, 1_000), // 1GB
                "Statistical Engine Storage Capacity",
                HealthStatus.Degraded
            );
        return configurator;
    }
}
