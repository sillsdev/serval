namespace Serval.Machine.Shared.Services;

public class NmtPreprocessBuildJob(
    IPlatformService platformService,
    IRepository<TranslationEngine> engines,
    IDataAccessContext dataAccessContext,
    ILogger<NmtPreprocessBuildJob> logger,
    IBuildJobService buildJobService,
    ISharedFileService sharedFileService,
    ILanguageTagService languageTagService,
    IParallelCorpusPreprocessingService parallelCorpusPreprocessingService
)
    : PreprocessBuildJob(
        platformService,
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
