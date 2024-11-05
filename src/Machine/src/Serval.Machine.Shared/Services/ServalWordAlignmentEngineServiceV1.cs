using Google.Protobuf.WellKnownTypes;
using Serval.WordAlignment.V1;

namespace Serval.Machine.Shared.Services;

public class ServalWordAlignmentEngineServiceV1(IEnumerable<IWordAlignmentEngineService> engineServices)
    : WordAlignmentEngineApi.WordAlignmentEngineApiBase
{
    private static readonly Empty Empty = new();

    private readonly Dictionary<EngineType, IWordAlignmentEngineService> _engineServices = engineServices.ToDictionary(
        es => es.Type
    );

    public override async Task<Empty> Create(CreateRequest request, ServerCallContext context)
    {
        IWordAlignmentEngineService engineService = GetEngineService(request.EngineType);
        await engineService.CreateAsync(
            request.EngineId,
            request.HasEngineName ? request.EngineName : null,
            request.SourceLanguage,
            request.TargetLanguage,
            cancellationToken: context.CancellationToken
        );
        return Empty;
    }

    public override async Task<Empty> Delete(DeleteRequest request, ServerCallContext context)
    {
        IWordAlignmentEngineService engineService = GetEngineService(request.EngineType);
        await engineService.DeleteAsync(request.EngineId, context.CancellationToken);
        return Empty;
    }

    public override async Task<GetWordAlignmentResponse> GetWordAlignment(
        GetWordAlignmentRequest request,
        ServerCallContext context
    )
    {
        IWordAlignmentEngineService engineService = GetEngineService(request.EngineType);
        TranslationResult result;
        try
        {
            result = await engineService.GetBestPhraseAlignmentAsync(
                request.EngineId,
                request.SourceSegment,
                request.TargetSegment,
                context.CancellationToken
            );
        }
        catch (EngineNotBuiltException e)
        {
            throw new RpcException(new Status(StatusCode.Aborted, e.Message, e));
        }

        return new GetWordAlignmentResponse { Result = Map(result) };
    }

    public override async Task<Empty> StartBuild(StartBuildRequest request, ServerCallContext context)
    {
        IWordAlignmentEngineService engineService = GetEngineService(request.EngineType);
        Models.ParallelCorpus[] corpora = request.Corpora.Select(Map).ToArray();
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
        IWordAlignmentEngineService engineService = GetEngineService(request.EngineType);
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

    public override Task<GetQueueSizeResponse> GetQueueSize(GetQueueSizeRequest request, ServerCallContext context)
    {
        IWordAlignmentEngineService engineService = GetEngineService(request.EngineType);
        return Task.FromResult(new GetQueueSizeResponse { Size = engineService.GetQueueSize() });
    }

    private IWordAlignmentEngineService GetEngineService(string engineTypeStr)
    {
        if (_engineServices.TryGetValue(GetEngineType(engineTypeStr), out IWordAlignmentEngineService? service))
            return service;
        throw new RpcException(new Status(StatusCode.InvalidArgument, "The engine type is invalid."));
    }

    private static EngineType GetEngineType(string engineTypeStr)
    {
        engineTypeStr = engineTypeStr[0].ToString().ToUpperInvariant() + engineTypeStr[1..];
        if (System.Enum.TryParse(engineTypeStr, out EngineType engineType))
            return engineType;
        throw new RpcException(new Status(StatusCode.InvalidArgument, "The engine type is invalid."));
    }

    private static WordAlignmentResult Map(TranslationResult source)
    {
        return new WordAlignmentResult
        {
            SourceTokens = { source.SourceTokens },
            TargetTokens = { source.TargetTokens },
            Confidences = { source.Confidences },
            Alignment = { Map(source.Alignment) },
        };
    }

    private static IEnumerable<WordAlignment.V1.AlignedWordPair> Map(WordAlignmentMatrix source)
    {
        for (int i = 0; i < source.RowCount; i++)
        {
            for (int j = 0; j < source.ColumnCount; j++)
            {
                if (source[i, j])
                    yield return new WordAlignment.V1.AlignedWordPair { SourceIndex = i, TargetIndex = j };
            }
        }
    }

    private static Models.ParallelCorpus Map(WordAlignment.V1.ParallelCorpus source)
    {
        return new Models.ParallelCorpus
        {
            Id = source.Id,
            SourceCorpora = source.SourceCorpora.Select(Map).ToList(),
            TargetCorpora = source.TargetCorpora.Select(Map).ToList()
        };
    }

    private static Models.MonolingualCorpus Map(WordAlignment.V1.MonolingualCorpus source)
    {
        var trainOnChapters = source.TrainOnChapters.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Chapters.ToHashSet()
        );
        var trainOnTextIds = source.TrainOnTextIds.ToHashSet();
        FilterChoice trainingFilter = GetFilterChoice(trainOnChapters, trainOnTextIds);

        var pretranslateChapters = source.WordAlignOnChapters.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Chapters.ToHashSet()
        );
        var pretranslateTextIds = source.WordAlignOnTextIds.ToHashSet();
        FilterChoice pretranslateFilter = GetFilterChoice(pretranslateChapters, pretranslateTextIds);

        return new Models.MonolingualCorpus
        {
            Id = source.Id,
            Language = source.Language,
            Files = source.Files.Select(Map).ToList(),
            TrainOnChapters = trainingFilter == FilterChoice.Chapters ? trainOnChapters : null,
            TrainOnTextIds = trainingFilter == FilterChoice.TextIds ? trainOnTextIds : null,
            PretranslateChapters = pretranslateFilter == FilterChoice.Chapters ? pretranslateChapters : null,
            PretranslateTextIds = pretranslateFilter == FilterChoice.TextIds ? pretranslateTextIds : null
        };
    }

    private static Models.CorpusFile Map(WordAlignment.V1.CorpusFile source)
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

    private static FilterChoice GetFilterChoice(
        IReadOnlyDictionary<string, HashSet<int>> chapters,
        HashSet<string> textIds
    )
    {
        // Only either textIds or Scripture Range will be used at a time
        // TextIds may be an empty array, so prefer that if both are empty (which applies to both scripture and text)
        if (chapters is null && textIds is null)
            return FilterChoice.None;
        if (chapters is null || chapters.Count == 0)
            return FilterChoice.TextIds;
        return FilterChoice.Chapters;
    }
}
