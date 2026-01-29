namespace Serval.Translation.Controllers;

#pragma warning disable CS0612 // Type or member is obsolete

public abstract class TranslationControllerBase(IAuthorizationService authService, IUrlService urlService)
    : ServalControllerBase(authService)
{
    private readonly IUrlService _urlService = urlService;

    protected TranslationBuildDto Map(Build source)
    {
        string targetQuoteConvention = source.TargetQuoteConvention ?? "";

        return new TranslationBuildDto
        {
            Id = source.Id,
            Url = _urlService.GetUrl(Endpoints.GetTranslationBuild, new { id = source.EngineRef, buildId = source.Id }),
            Revision = source.Revision,
            Name = source.Name,
            Engine = new ResourceLinkDto
            {
                Id = source.EngineRef,
                Url = _urlService.GetUrl(Endpoints.GetTranslationEngine, new { id = source.EngineRef })
            },
            TrainOn = source.TrainOn?.Select(s => Map(source.EngineRef, s)).ToList(),
            Pretranslate = source.Pretranslate?.Select(s => Map(source.EngineRef, s)).ToList(),
            Step = source.Step,
            PercentCompleted = source.Progress,
            Progress = source.Progress,
            Message = source.Message,
            QueueDepth = source.QueueDepth,
            State = source.State,
            DateCreated = source.DateCreated,
            DateStarted = source.DateStarted,
            DateCompleted = source.DateCompleted,
            DateFinished = source.DateFinished,
            Options = source.Options,
            DeploymentVersion = source.DeploymentVersion,
            ExecutionData = Map(source.ExecutionData),
            Phases = source.Phases?.Select(Map).ToList(),
            Analysis = source.Analysis?.Select(a => Map(a, targetQuoteConvention)).ToList(),
            TargetQuoteConvention = targetQuoteConvention,
            CanDenormalizeQuotes = targetQuoteConvention != ""
        };
    }

    private PretranslateCorpusDto Map(string engineId, PretranslateCorpus source) =>
        new PretranslateCorpusDto
        {
            Corpus =
                source.CorpusRef != null
                    ? new ResourceLinkDto
                    {
                        Id = source.CorpusRef,
                        Url = _urlService.GetUrl(
                            Endpoints.GetTranslationCorpus,
                            new { id = engineId, corpusId = source.CorpusRef }
                        )
                    }
                    : null,
            TextIds = source.TextIds,
            ScriptureRange = source.ScriptureRange,
            ParallelCorpus =
                source.ParallelCorpusRef != null
                    ? new ResourceLinkDto
                    {
                        Id = source.ParallelCorpusRef,
                        Url = _urlService.GetUrl(
                            Endpoints.GetParallelTranslationCorpus,
                            new { id = engineId, parallelCorpusId = source.ParallelCorpusRef }
                        )
                    }
                    : null,
            SourceFilters = source.SourceFilters?.Select(Map).ToList()
        };

    private TrainingCorpusDto Map(string engineId, TrainingCorpus source) =>
        new TrainingCorpusDto
        {
            Corpus =
                source.CorpusRef != null
                    ? new ResourceLinkDto
                    {
                        Id = source.CorpusRef,
                        Url = _urlService.GetUrl(
                            Endpoints.GetTranslationCorpus,
                            new { id = engineId, corpusId = source.CorpusRef }
                        )
                    }
                    : null,
            TextIds = source.TextIds,
            ScriptureRange = source.ScriptureRange,
            ParallelCorpus =
                source.ParallelCorpusRef != null
                    ? new ResourceLinkDto
                    {
                        Id = source.ParallelCorpusRef,
                        Url = _urlService.GetUrl(
                            Endpoints.GetParallelTranslationCorpus,
                            new { id = engineId, parallelCorpusId = source.ParallelCorpusRef }
                        )
                    }
                    : null,
            SourceFilters = source.SourceFilters?.Select(Map).ToList(),
            TargetFilters = source.TargetFilters?.Select(Map).ToList()
        };

    private ParallelCorpusFilterDto Map(ParallelCorpusFilter source) =>
        new ParallelCorpusFilterDto
        {
            Corpus = new ResourceLinkDto
            {
                Id = source.CorpusRef,
                Url = _urlService.GetUrl(Endpoints.GetCorpus, new { id = source.CorpusRef })
            },
            TextIds = source.TextIds,
            ScriptureRange = source.ScriptureRange
        };

    private static PhaseDto Map(BuildPhase source) =>
        new PhaseDto
        {
            Stage = (PhaseStage)source.Stage,
            Step = source.Step,
            StepCount = source.StepCount,
            Started = source.Started,
        };

    private static ParallelCorpusAnalysisDto Map(ParallelCorpusAnalysis source, string targetQuoteConvention) =>
        new ParallelCorpusAnalysisDto
        {
            ParallelCorpusRef = source.ParallelCorpusRef,
            TargetQuoteConvention = targetQuoteConvention,
            SourceQuoteConvention = "ignore",
            CanDenormalizeQuotes = targetQuoteConvention != ""
        };

    private static ExecutionDataDto Map(ExecutionData source) =>
        new ExecutionDataDto
        {
            TrainCount = source.TrainCount ?? 0,
            PretranslateCount = source.PretranslateCount ?? 0,
            Warnings = source.Warnings ?? [],
            EngineSourceLanguageTag = source.EngineSourceLanguageTag,
            EngineTargetLanguageTag = source.EngineTargetLanguageTag,
            ResolvedSourceLanguage = source.ResolvedSourceLanguage,
            ResolvedTargetLanguage = source.ResolvedTargetLanguage,
        };
}

#pragma warning restore CS0612 // Type or member is obsolete
