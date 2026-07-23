namespace Microsoft.Extensions.DependencyInjection;

public static class IServalConfiguratorExtensions
{
    public static IServalConfigurator AddMachineTranslation(this IServalConfigurator configurator)
    {
        configurator.Services.AddSingleton<ILanguageTagService, LanguageTagService>();
        configurator.Services.AddHostedService<ModelCleanupService>();

        configurator.AddTranslationEngineBuildJobService();
        configurator.AddTranslationEngineDataAccess();
        configurator.AddTranslationEngines();

        return configurator;
    }

    private static IServalConfigurator AddTranslationEngines(this IServalConfigurator configurator)
    {
        configurator.Services.AddKeyedScoped<IPlatformService, ServalTranslationPlatformService>(
            EngineGroup.Translation
        );

        // SMT Transfer Engine
        configurator.Services.AddSingleton<SmtTransferEngineStateService>();
        configurator.Services.AddHostedService<SmtTransferEngineCommitService>();
        configurator.Services.Configure<ThotSmtModelOptions>(
            configurator.Configuration.GetSection(ThotSmtModelOptions.Key)
        );
        configurator.Services.AddSingleton<ISmtModelFactory, ThotSmtModelFactory>();
        configurator.Services.AddSingleton<ITransferEngineFactory, TransferEngineFactory>();
        configurator.Services.AddSingleton<ITruecaserFactory, UnigramTruecaserFactory>();
        configurator.AddTranslationEngine<SmtTransferEngineService>(nameof(EngineType.SmtTransfer));

        // NMT Engine
        configurator.AddTranslationEngine<NmtEngineService>(nameof(EngineType.Nmt));

        // Echo Engine
        configurator.AddTranslationEngine<EchoTranslationEngineService>(nameof(EngineType.Echo));

        return configurator;
    }

    private static IServalConfigurator AddTranslationEngineBuildJobService(this IServalConfigurator configurator)
    {
        configurator.Services.AddScoped<IBuildJobRunner<TranslationEngine>, ClearMLBuildJobRunner<TranslationEngine>>();
        configurator.Services.AddScoped<IBuildJobService<TranslationEngine>, TranslationBuildJobService>();

        configurator.Services.AddScoped<IClearMLBuildJobFactory, NmtClearMLBuildJobFactory>();
        configurator.Services.AddScoped<IClearMLBuildJobFactory, SmtTransferClearMLBuildJobFactory>();

        configurator.Services.AddSingleton<ClearMLMonitorService<TranslationEngine>>();
        configurator.Services.AddSingleton<IClearMLQueueService<TranslationEngine>>(x =>
            x.GetRequiredService<ClearMLMonitorService<TranslationEngine>>()
        );
        configurator.Services.AddHostedService(p => p.GetRequiredService<ClearMLMonitorService<TranslationEngine>>());

        configurator.Services.AddSingleton<TranslationEngineLocalBuildJobRunner>();
        configurator.Services.AddSingleton<IBuildJobRunner<TranslationEngine>>(sp =>
            sp.GetRequiredService<TranslationEngineLocalBuildJobRunner>()
        );
        configurator.Services.AddHostedService(sp => sp.GetRequiredService<TranslationEngineLocalBuildJobRunner>());
        configurator.Services.AddSingleton<ILocalBuildJobFactory, NmtLocalBuildJobFactory>();
        configurator.Services.AddSingleton<ILocalBuildJobFactory, SmtTransferLocalBuildJobFactory>();
        configurator.Services.AddSingleton<ILocalBuildJobFactory, EchoLocalBuildJobFactory>();
        return configurator;
    }

    public static IServalConfigurator AddTranslationEngineDataAccess(this IServalConfigurator configurator)
    {
        configurator.DataAccess.AddRepository<TranslationEngine>(
            "machine.translation_engines",
            init:
            [
                c =>
                    c.Indexes.CreateOrUpdateAsync(
                        new CreateIndexModel<TranslationEngine>(
                            Builders<TranslationEngine>.IndexKeys.Ascending(e => e.EngineId)
                        )
                    ),
                c =>
                    c.Indexes.CreateOrUpdateAsync(
                        new CreateIndexModel<TranslationEngine>(
                            Builders<TranslationEngine>.IndexKeys.Ascending(e => e.CurrentBuild!.BuildJobRunner)
                        )
                    ),
            ]
        );
        configurator.DataAccess.AddRepository<TrainSegmentPair>(
            "machine.train_segment_pairs",
            init:
            [
                c =>
                    c.Indexes.CreateOrUpdateAsync(
                        new CreateIndexModel<TrainSegmentPair>(
                            Builders<TrainSegmentPair>.IndexKeys.Ascending(p => p.TranslationEngineRef)
                        )
                    ),
            ]
        );
        return configurator;
    }
}
