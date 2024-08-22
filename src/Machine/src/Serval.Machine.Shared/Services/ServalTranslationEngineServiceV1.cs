using Google.Protobuf.WellKnownTypes;
using Serval.Translation.V1;

namespace Serval.Machine.Shared.Services;

public class ServalTranslationEngineServiceV1(IEnumerable<ITranslationEngineService> engineServices)
    : TranslationEngineApi.TranslationEngineApiBase
{
    private static readonly Empty Empty = new();

    private readonly Dictionary<TranslationEngineType, ITranslationEngineService> _engineServices =
        engineServices.ToDictionary(es => es.Type);

    public override async Task<CreateResponse> Create(CreateRequest request, ServerCallContext context)
    {
        ITranslationEngineService engineService = GetEngineService(request.EngineType);
        TranslationEngine translationEngine = await engineService.CreateAsync(
            request.EngineId,
            request.HasEngineName ? request.EngineName : null,
            request.SourceLanguage,
            request.TargetLanguage,
            request.HasIsModelPersisted ? request.IsModelPersisted : null,
            context.CancellationToken
        );
        return new CreateResponse { IsModelPersisted = translationEngine.IsModelPersisted };
    }

    public override async Task<Empty> Delete(DeleteRequest request, ServerCallContext context)
    {
        ITranslationEngineService engineService = GetEngineService(request.EngineType);
        await engineService.DeleteAsync(request.EngineId, context.CancellationToken);
        return Empty;
    }

    public override async Task<TranslateResponse> Translate(TranslateRequest request, ServerCallContext context)
    {
        ITranslationEngineService engineService = GetEngineService(request.EngineType);
        IEnumerable<SIL.Machine.Translation.TranslationResult> results;
        try
        {
            results = await engineService.TranslateAsync(
                request.EngineId,
                request.N,
                request.Segment,
                context.CancellationToken
            );
        }
        catch (EngineNotBuiltException e)
        {
            throw new RpcException(new Status(StatusCode.Aborted, e.Message, e));
        }

        return new TranslateResponse { Results = { results.Select(Map) } };
    }

    public override async Task<GetWordGraphResponse> GetWordGraph(
        GetWordGraphRequest request,
        ServerCallContext context
    )
    {
        ITranslationEngineService engineService = GetEngineService(request.EngineType);
        SIL.Machine.Translation.WordGraph wordGraph;
        try
        {
            wordGraph = await engineService.GetWordGraphAsync(
                request.EngineId,
                request.Segment,
                context.CancellationToken
            );
        }
        catch (EngineNotBuiltException e)
        {
            throw new RpcException(new Status(StatusCode.Aborted, e.Message, e));
        }
        return new GetWordGraphResponse { WordGraph = Map(wordGraph) };
    }

    public override async Task<Empty> TrainSegmentPair(TrainSegmentPairRequest request, ServerCallContext context)
    {
        ITranslationEngineService engineService = GetEngineService(request.EngineType);
        await engineService.TrainSegmentPairAsync(
            request.EngineId,
            request.SourceSegment,
            request.TargetSegment,
            request.SentenceStart,
            context.CancellationToken
        );
        return Empty;
    }

    public override async Task<Empty> StartBuild(StartBuildRequest request, ServerCallContext context)
    {
        ITranslationEngineService engineService = GetEngineService(request.EngineType);
        Models.Corpus[] corpora = request.Corpora.Select(Map).ToArray();
        try
        {
            await engineService.StartBuildAsync(
                request.EngineId,
                request.BuildId,
                request.HasOptions ? request.Options : null,
                corpora,
                context.CancellationToken
            );
        }
        catch (InvalidOperationException e)
        {
            throw new RpcException(new Status(StatusCode.Aborted, e.Message, e));
        }
        return Empty;
    }

    public override async Task<Empty> CancelBuild(CancelBuildRequest request, ServerCallContext context)
    {
        ITranslationEngineService engineService = GetEngineService(request.EngineType);
        try
        {
            await engineService.CancelBuildAsync(request.EngineId, context.CancellationToken);
        }
        catch (InvalidOperationException e)
        {
            throw new RpcException(new Status(StatusCode.Aborted, e.Message, e));
        }
        return Empty;
    }

    public override async Task<GetModelDownloadUrlResponse> GetModelDownloadUrl(
        GetModelDownloadUrlRequest request,
        ServerCallContext context
    )
    {
        try
        {
            ITranslationEngineService engineService = GetEngineService(request.EngineType);
            ModelDownloadUrl modelDownloadUrl = await engineService.GetModelDownloadUrlAsync(
                request.EngineId,
                context.CancellationToken
            );
            return new GetModelDownloadUrlResponse
            {
                Url = modelDownloadUrl.Url,
                ModelRevision = modelDownloadUrl.ModelRevision,
                ExpiresAt = modelDownloadUrl.ExpiresAt.ToTimestamp()
            };
        }
        catch (InvalidOperationException e)
        {
            throw new RpcException(new Status(StatusCode.Aborted, e.Message));
        }
        catch (FileNotFoundException e)
        {
            throw new RpcException(new Status(StatusCode.NotFound, e.Message));
        }
    }

    public override Task<GetQueueSizeResponse> GetQueueSize(GetQueueSizeRequest request, ServerCallContext context)
    {
        ITranslationEngineService engineService = GetEngineService(request.EngineType);
        return Task.FromResult(new GetQueueSizeResponse { Size = engineService.GetQueueSize() });
    }

    public override Task<GetLanguageInfoResponse> GetLanguageInfo(
        GetLanguageInfoRequest request,
        ServerCallContext context
    )
    {
        ITranslationEngineService engineService = GetEngineService(request.EngineType);
        bool isNative = engineService.IsLanguageNativeToModel(request.Language, out string internalCode);
        return Task.FromResult(new GetLanguageInfoResponse { InternalCode = internalCode, IsNative = isNative, });
    }

    private ITranslationEngineService GetEngineService(string engineTypeStr)
    {
        if (_engineServices.TryGetValue(GetEngineType(engineTypeStr), out ITranslationEngineService? service))
            return service;
        throw new RpcException(new Status(StatusCode.InvalidArgument, "The engine type is invalid."));
    }

    private static TranslationEngineType GetEngineType(string engineTypeStr)
    {
        engineTypeStr = engineTypeStr[0].ToString().ToUpperInvariant() + engineTypeStr[1..];
        if (System.Enum.TryParse(engineTypeStr, out TranslationEngineType engineType))
            return engineType;
        throw new RpcException(new Status(StatusCode.InvalidArgument, "The engine type is invalid."));
    }

    private static Translation.V1.TranslationResult Map(SIL.Machine.Translation.TranslationResult source)
    {
        return new Translation.V1.TranslationResult
        {
            Translation = source.Translation,
            SourceTokens = { source.SourceTokens },
            TargetTokens = { source.TargetTokens },
            Confidences = { source.Confidences },
            Sources = { source.Sources.Select(Map) },
            Alignment = { Map(source.Alignment) },
            Phrases = { source.Phrases.Select(Map) }
        };
    }

    private static Translation.V1.WordGraph Map(SIL.Machine.Translation.WordGraph source)
    {
        return new Translation.V1.WordGraph
        {
            SourceTokens = { source.SourceTokens },
            InitialStateScore = source.InitialStateScore,
            FinalStates = { source.FinalStates },
            Arcs = { source.Arcs.Select(Map) }
        };
    }

    private static Translation.V1.WordGraphArc Map(SIL.Machine.Translation.WordGraphArc source)
    {
        return new Translation.V1.WordGraphArc
        {
            PrevState = source.PrevState,
            NextState = source.NextState,
            Score = source.Score,
            TargetTokens = { source.TargetTokens },
            Alignment = { Map(source.Alignment) },
            Confidences = { source.Confidences },
            SourceSegmentStart = source.SourceSegmentRange.Start,
            SourceSegmentEnd = source.SourceSegmentRange.End,
            Sources = { source.Sources.Select(Map) }
        };
    }

    private static Translation.V1.TranslationSources Map(SIL.Machine.Translation.TranslationSources source)
    {
        return new Translation.V1.TranslationSources
        {
            Values =
            {
                System
                    .Enum.GetValues<SIL.Machine.Translation.TranslationSources>()
                    .Where(s => s != SIL.Machine.Translation.TranslationSources.None && source.HasFlag(s))
                    .Select(s =>
                        s switch
                        {
                            SIL.Machine.Translation.TranslationSources.Smt => TranslationSource.Primary,
                            SIL.Machine.Translation.TranslationSources.Nmt => TranslationSource.Primary,
                            SIL.Machine.Translation.TranslationSources.Transfer => TranslationSource.Secondary,
                            SIL.Machine.Translation.TranslationSources.Prefix => TranslationSource.Human,
                            _ => TranslationSource.Primary
                        }
                    )
            }
        };
    }

    private static IEnumerable<Translation.V1.AlignedWordPair> Map(WordAlignmentMatrix source)
    {
        for (int i = 0; i < source.RowCount; i++)
        {
            for (int j = 0; j < source.ColumnCount; j++)
            {
                if (source[i, j])
                    yield return new Translation.V1.AlignedWordPair { SourceIndex = i, TargetIndex = j };
            }
        }
    }

    private static Translation.V1.Phrase Map(SIL.Machine.Translation.Phrase source)
    {
        return new Translation.V1.Phrase
        {
            SourceSegmentStart = source.SourceSegmentRange.Start,
            SourceSegmentEnd = source.SourceSegmentRange.End,
            TargetSegmentCut = source.TargetSegmentCut
        };
    }

    private static Models.Corpus Map(Translation.V1.Corpus source)
    {
        var pretranslateChapters = source.PretranslateChapters.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Chapters.ToHashSet()
        );
        FilterChoice pretranslateFilter = GetFilterChoice(source.PretranslateAll, pretranslateChapters);

        var trainOnChapters = source.TrainOnChapters.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Chapters.ToHashSet()
        );
        FilterChoice trainingFilter = GetFilterChoice(source.TrainOnAll, trainOnChapters);

        return new Models.Corpus
        {
            Id = source.Id,
            SourceLanguage = source.SourceLanguage,
            TargetLanguage = source.TargetLanguage,
            TrainOnChapters = trainingFilter == FilterChoice.Chapters ? trainOnChapters : null,
            PretranslateChapters = pretranslateFilter == FilterChoice.Chapters ? pretranslateChapters : null,
            TrainOnTextIds = trainingFilter == FilterChoice.TextIds ? source.TrainOnTextIds.ToHashSet() : null,
            PretranslateTextIds =
                pretranslateFilter == FilterChoice.TextIds ? source.PretranslateTextIds.ToHashSet() : null,
            SourceFiles = source.SourceFiles.Select(Map).ToList(),
            TargetFiles = source.TargetFiles.Select(Map).ToList()
        };
    }

    private static Models.CorpusFile Map(Translation.V1.CorpusFile source)
    {
        return new Models.CorpusFile
        {
            Location = source.Location,
            Format = (Models.FileFormat)source.Format,
            TextId = source.TextId
        };
    }

    private enum FilterChoice
    {
        Chapters,
        TextIds,
        None
    }

    private static FilterChoice GetFilterChoice(bool all, IReadOnlyDictionary<string, HashSet<int>> chapters)
    {
        if (all)
            return FilterChoice.None;

        // Only either textIds or Scripture Range will be used at a time
        // TextIds may be an empty array, so prefer that if both are empty (which applies to both scripture and text)
        if (chapters.Count == 0)
            return FilterChoice.TextIds;
        else
            return FilterChoice.Chapters;
    }
}
