namespace Serval.Machine.WordAlignment.Services;

public class WordAlignmentEngineClearMLMonitorService(
    IServiceProvider services,
    IClearMLService clearMLService,
    ISharedFileService sharedFileService,
    IOptionsMonitor<ClearMLOptions> clearMLOptions,
    IOptionsMonitor<BuildJobOptions> buildJobOptions,
    ILogger<WordAlignmentEngineClearMLMonitorService> logger
)
    : ClearMLMonitorService<WordAlignmentEngine>(
        services,
        clearMLService,
        sharedFileService,
        clearMLOptions,
        buildJobOptions,
        logger,
        EngineGroup.WordAlignment,
        "Word Alignment Engine ClearML monitor service"
    );
