namespace Serval.Machine.Shared.Services;

public class NmtPreprocessBuildJob(
    [FromKeyedServices(EngineGroup.Translation)] IPlatformService platformService,
    IRepository<TranslationEngine> engines,
    IDataAccessContext dataAccessContext,
    ILogger<NmtPreprocessBuildJob> logger,
    IBuildJobService<TranslationEngine> buildJobService,
    ISharedFileService sharedFileService,
    ILanguageTagService languageTagService,
    IParallelCorpusPreprocessingService parallelCorpusPreprocessingService
)
    : TranslationPreprocessBuildJob(
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

    protected override async Task UpdateParallelCorpusAnalysisAsync(
        string engineId,
        string buildId,
        IReadOnlyList<ParallelCorpus> corpora,
        CancellationToken cancellationToken
    )
    {
        List<ParallelCorpusAnalysis> parallelCorpusAnalysis = [];
        foreach (ParallelCorpus parallelCorpus in corpora)
        {
            (QuoteConventionAnalysis? _, QuoteConventionAnalysis? targetQuotationConvention) =
                ParallelCorpusPreprocessingService.AnalyzeParallelCorpus(parallelCorpus);
            string targetQuotationConventionName = targetQuotationConvention?.BestQuoteConvention.Name ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(targetQuotationConventionName))
            {
                parallelCorpusAnalysis.Add(
                    new ParallelCorpusAnalysis
                    {
                        ParallelCorpusRef = parallelCorpus.Id,
                        TargetQuoteConvention = targetQuotationConventionName,
                    }
                );
            }
        }

        await PlatformService.UpdateParallelCorpusAnalysisAsync(
            engineId,
            buildId,
            parallelCorpusAnalysis,
            cancellationToken
        );
    }
}
