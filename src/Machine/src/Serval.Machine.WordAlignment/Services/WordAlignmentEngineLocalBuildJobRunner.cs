namespace Serval.Machine.WordAlignment.Services;

public class WordAlignmentEngineLocalBuildJobRunner(
    IEnumerable<ILocalBuildJobFactory> factories,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<WordAlignmentEngineLocalBuildJobRunner> logger
) : LocalBuildJobRunner<WordAlignmentEngine>(factories, serviceScopeFactory, logger, EngineGroup.WordAlignment);
