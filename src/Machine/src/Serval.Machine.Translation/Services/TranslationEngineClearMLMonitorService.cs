namespace Serval.Machine.Translation.Services;

public class TranslationEngineClearMLMonitorService(
    IServiceProvider services,
    IClearMLService clearMLService,
    ISharedFileService sharedFileService,
    IOptionsMonitor<ClearMLOptions> clearMLOptions,
    IOptionsMonitor<BuildJobOptions> buildJobOptions,
    ILogger<TranslationEngineClearMLMonitorService> logger
)
    : ClearMLMonitorService<TranslationEngine>(
        services,
        clearMLService,
        sharedFileService,
        clearMLOptions,
        buildJobOptions,
        logger,
        EngineGroup.Translation,
        "Translation Engine ClearML monitor service"
    );
