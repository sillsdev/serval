namespace Serval.Machine.Shared.Services;

public class NmtPreprocessBuildJob(
    IEnumerable<IPlatformService> platformServices,
    IRepository<TranslationEngine> engines,
    IDataAccessContext dataAccessContext,
    ILogger<NmtPreprocessBuildJob> logger,
    IBuildJobService<TranslationEngine> buildJobService,
    ISharedFileService sharedFileService,
    ILanguageTagService languageTagService,
    IParallelCorpusPreprocessingService parallelCorpusPreprocessingService
)
    : PreprocessBuildJob<TranslationEngine>(
        platformServices.First(ps => ps.EngineGroup == EngineGroup.Translation),
        engines,
        dataAccessContext,
        logger,
        buildJobService,
        sharedFileService,
        parallelCorpusPreprocessingService
    )
{
    private readonly ILanguageTagService _languageTagService = languageTagService;

    protected override bool ResolveLanguageCodeForBaseModel(string languageCode, out string resolvedCode)
    {
        return _languageTagService.ConvertToFlores200Code(languageCode, out resolvedCode);
    }
}
