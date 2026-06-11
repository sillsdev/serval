namespace Serval.WordAlignment.Services;

#pragma warning disable CS0612 // Type or member is obsolete

public class DtoMapper(IUrlService urlService)
{
    public WordAlignmentParallelCorpusDto Map(string engineId, ParallelCorpus source)
    {
        return new WordAlignmentParallelCorpusDto
        {
            Id = source.Id,
            Url = urlService.GetUrl(Endpoints.GetCorpus, new { id = engineId, corpusId = source.Id }),
            Engine = new ResourceLinkDto
            {
                Id = engineId,
                Url = urlService.GetUrl(Endpoints.GetWordAlignmentEngine, new { id = engineId }),
            },
            SourceCorpora = source
                .SourceCorpora.Select(c => new ResourceLinkDto
                {
                    Id = c.Id,
                    Url = urlService.GetUrl(Endpoints.GetCorpus, new { Id = c.Id }),
                })
                .ToList(),
            TargetCorpora = source
                .TargetCorpora.Select(c => new ResourceLinkDto
                {
                    Id = c.Id,
                    Url = urlService.GetUrl(Endpoints.GetCorpus, new { Id = c.Id }),
                })
                .ToList(),
        };
    }

    public WordAlignmentEngineDto Map(Engine source)
    {
        return new WordAlignmentEngineDto
        {
            Id = source.Id,
            Url = urlService.GetUrl(Endpoints.GetWordAlignmentEngine, new { id = source.Id }),
            Name = source.Name,
            SourceLanguage = source.SourceLanguage,
            TargetLanguage = source.TargetLanguage,
            Type = source.Type.ToKebabCase(),
            IsBuilding = source.IsBuilding,
            ModelRevision = source.ModelRevision,
            Confidence = Math.Round(source.Confidence, 8),
            CorpusSize = source.CorpusSize,
            DateCreated = source.DateCreated,
        };
    }

    public WordAlignmentBuildDto Map(Build source)
    {
        return new WordAlignmentBuildDto
        {
            Id = source.Id,
            Url = urlService.GetUrl(
                Endpoints.GetWordAlignmentBuild,
                new { id = source.EngineRef, buildId = source.Id }
            ),
            Revision = source.Revision,
            Name = source.Name,
            Engine = new ResourceLinkDto
            {
                Id = source.EngineRef,
                Url = urlService.GetUrl(
                    Endpoints.GetWordAlignmentBuild,
                    new { id = source.EngineRef, buildId = source.Id }
                ),
            },
            TrainOn = source.TrainOn?.Select(s => Map(source.EngineRef, s)).ToList(),
            WordAlignOn = source.WordAlignOn?.Select(s => Map(source.EngineRef, s)).ToList(),
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
        };
    }

    private TrainingCorpusDto Map(string engineId, TrainingCorpus source)
    {
        return new TrainingCorpusDto
        {
            ParallelCorpus =
                source.ParallelCorpusRef != null
                    ? new ResourceLinkDto
                    {
                        Id = source.ParallelCorpusRef,
                        Url = urlService.GetUrl(
                            Endpoints.GetParallelWordAlignmentCorpus,
                            new { id = engineId, parallelCorpusId = source.ParallelCorpusRef }
                        ),
                    }
                    : null,
            SourceFilters = source.SourceFilters?.Select(Map).ToList(),
            TargetFilters = source.TargetFilters?.Select(Map).ToList(),
        };
    }

    private WordAlignmentCorpusDto Map(string engineId, WordAlignmentCorpus source)
    {
        return new WordAlignmentCorpusDto
        {
            ParallelCorpus =
                source.ParallelCorpusRef != null
                    ? new ResourceLinkDto
                    {
                        Id = source.ParallelCorpusRef,
                        Url = urlService.GetUrl(
                            Endpoints.GetParallelWordAlignmentCorpus,
                            new { id = engineId, parallelCorpusId = source.ParallelCorpusRef }
                        ),
                    }
                    : null,
            SourceFilters = source.SourceFilters?.Select(Map).ToList(),
            TargetFilters = source.TargetFilters?.Select(Map).ToList(),
        };
    }

    private ParallelCorpusFilterDto Map(ParallelCorpusFilter source)
    {
        return new ParallelCorpusFilterDto
        {
            Corpus = new ResourceLinkDto
            {
                Id = source.CorpusRef,
                Url = urlService.GetUrl(Endpoints.GetCorpus, new { id = source.CorpusRef }),
            },
            TextIds = source.TextIds,
            ScriptureRange = source.ScriptureRange,
        };
    }

    public static AlignedWordPairDto Map(AlignedWordPair source)
    {
        return new AlignedWordPairDto()
        {
            SourceIndex = source.SourceIndex,
            TargetIndex = source.TargetIndex,
            Score = source.Score,
        };
    }

    public static WordAlignmentDto Map(Models.WordAlignment source)
    {
        return new WordAlignmentDto
        {
            TextId = source.TextId,
            SourceRefs = source.SourceRefs,
            TargetRefs = source.TargetRefs,
            Refs = source.Refs,
            SourceTokens = source.SourceTokens.ToList(),
            TargetTokens = source.TargetTokens.ToList(),
            Alignment = source
                .Alignment.Select(c => new AlignedWordPairDto()
                {
                    SourceIndex = c.SourceIndex,
                    TargetIndex = c.TargetIndex,
                    Score = c.Score,
                })
                .ToList(),
        };
    }

    private static PhaseDto Map(Phase source)
    {
        return new PhaseDto
        {
            Stage = source.Stage,
            Step = source.Step,
            StepCount = source.StepCount,
            Started = source.Started,
        };
    }

    private static WordAlignmentExecutionDataDto Map(ExecutionData source)
    {
        return new WordAlignmentExecutionDataDto
        {
            TrainCount = source.TrainCount ?? 0,
            WordAlignCount = source.WordAlignCount ?? 0,
            Warnings = source.Warnings ?? [],
            EngineSourceLanguageTag = source.EngineSourceLanguageTag,
            EngineTargetLanguageTag = source.EngineTargetLanguageTag,
        };
    }
}
