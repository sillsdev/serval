namespace Serval.Machine.Shared.Services;

public class NmtPreprocessBuildJob(
    IPlatformService platformService,
    IRepository<TranslationEngine> engines,
    IDataAccessContext dataAccessContext,
    ILogger<NmtPreprocessBuildJob> logger,
    IBuildJobService<TranslationEngine> buildJobService,
    ISharedFileService sharedFileService,
    ICorpusService corpusService,
    ILanguageTagService languageTagService
)
    : PreprocessBuildJob<TranslationEngine>(
        platformService,
        engines,
        dataAccessContext,
        logger,
        buildJobService,
        sharedFileService,
        corpusService
    )
{
    private readonly ILanguageTagService _languageTagService = languageTagService;

    protected override bool ResolveLanguageCodeForBaseModel(string languageCode, out string resolvedCode)
    {
        return _languageTagService.ConvertToFlores200Code(languageCode, out resolvedCode);
    }
}
