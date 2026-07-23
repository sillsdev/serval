namespace Serval.Machine.Translation.Services;

public class TranslationEngineLocalBuildJobRunner(
    IEnumerable<ILocalBuildJobFactory> factories,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<TranslationEngineLocalBuildJobRunner> logger
) : LocalBuildJobRunner<TranslationEngine>(factories, serviceScopeFactory, logger)
{
    protected override EngineGroup EngineGroup => EngineGroup.Translation;
}
