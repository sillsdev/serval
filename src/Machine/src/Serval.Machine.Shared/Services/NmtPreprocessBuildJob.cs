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

    private bool ResolveLanguageCode(string languageCode, out string resolvedCode)
    {
        return _languageTagService.ConvertToFlores200Code(languageCode, out resolvedCode)
            == Flores200Support.LanguageAndScript;
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
            QuoteConventionAnalysis? targetQuotationConvention =
                ParallelCorpusPreprocessingService.AnalyzeTargetCorpusQuoteConvention(parallelCorpus);
            string targetQuotationConventionName = targetQuotationConvention?.BestQuoteConvention.Name ?? string.Empty;
            parallelCorpusAnalysis.Add(
                new ParallelCorpusAnalysis
                {
                    ParallelCorpusRef = parallelCorpus.Id,
                    TargetQuoteConvention = targetQuotationConventionName,
                }
            );
        }

        await PlatformService.UpdateParallelCorpusAnalysisAsync(
            engineId,
            buildId,
            parallelCorpusAnalysis,
            cancellationToken
        );
    }

    protected override async Task UpdateBuildExecutionData(
        string engineId,
        string buildId,
        int trainCount,
        int pretranslateCount,
        string sourceLanguageTag,
        string targetLanguageTag,
        IReadOnlyList<ParallelCorpus> corpora,
        CancellationToken cancellationToken
    )
    {
        bool sourceLanguageHasNativeSupport = ResolveLanguageCode(sourceLanguageTag, out string resolvedSourceLanguage);
        bool targetLanguageHasNativeSupport = ResolveLanguageCode(targetLanguageTag, out string resolvedTargetLanguage);

        if (trainCount == 0 && (!sourceLanguageHasNativeSupport || !targetLanguageHasNativeSupport))
        {
            throw new InvalidOperationException(
                $"At least one language code in build {buildId} is unknown to the base model, and the data specified for training was empty. Build canceled."
            );
        }

        IReadOnlyList<string> warnings = GetWarnings(
            trainCount,
            pretranslateCount,
            sourceLanguageTag,
            targetLanguageTag,
            corpora
        );

        // Log summary of build data
        JsonObject buildPreprocessSummary =
            new()
            {
                { "Event", "BuildPreprocess" },
                { "EngineId", engineId },
                { "BuildId", buildId },
                { "NumTrainRows", trainCount },
                { "NumPretranslateRows", pretranslateCount },
                { "EngineSourceLanguageTag", sourceLanguageTag },
                { "EngineTargetLanguageTag", targetLanguageTag },
                { "SourceLanguageResolved", resolvedSourceLanguage },
                { "TargetLanguageResolved", resolvedTargetLanguage },
                { "Warnings", new JsonArray(warnings.Select(w => JsonValue.Create(w)).ToArray()) }
            };
        Logger.LogInformation("{summary}", buildPreprocessSummary.ToJsonString());
        var executionData = new Dictionary<string, object>()
        {
            { "trainCount", trainCount },
            { "pretranslateCount", pretranslateCount },
            { "warnings", warnings },
            { "engineSourceLanguageTag", sourceLanguageTag },
            { "engineTargetLanguageTag", targetLanguageTag },
            { "resolvedSourceLanguage", resolvedSourceLanguage },
            { "resolvedTargetLanguage", resolvedTargetLanguage },
        };
        await PlatformService.UpdateBuildExecutionDataAsync(engineId, buildId, executionData, cancellationToken);
    }

    protected override IReadOnlyList<string> GetWarnings(
        int trainCount,
        int inferenceCount,
        string sourceLanguageTag,
        string targetLanguageTag,
        IReadOnlyList<ParallelCorpus> corpora
    )
    {
        List<string> warnings =
        [
            .. base.GetWarnings(trainCount, inferenceCount, sourceLanguageTag, targetLanguageTag, corpora)
        ];

        // Has at least a Gospel of Mark amount of data and not the special case of no data which will be caught elsewhere
        if (trainCount < 600 && trainCount != 0)
        {
            warnings.Add($"Only {trainCount} segments were selected for training.");
        }

        if (
            _languageTagService.ConvertToFlores200Code(sourceLanguageTag, out string resolvedCode)
            == Flores200Support.None
        )
        {
            warnings.Add($"The script for the source language '{resolvedCode}' is not in Flores-200");
        }

        if (_languageTagService.ConvertToFlores200Code(targetLanguageTag, out resolvedCode) == Flores200Support.None)
        {
            warnings.Add($"The script for the target language '{resolvedCode}' is not in Flores-200");
        }

        return warnings;
    }
}
