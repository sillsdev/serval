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
        WordAlignmentResult result;
        try
        {
            result = await engineService.AlignAsync(
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

        return new GetWordAlignmentResponse { Result = result };
    }

    public override async Task<Empty> StartBuild(StartBuildRequest request, ServerCallContext context)
    {
        IWordAlignmentEngineService engineService = GetEngineService(request.EngineType);
        SIL.ServiceToolkit.Models.ParallelCorpus[] corpora = request.Corpora.Select(Map).ToArray();
        await engineService.StartBuildAsync(
            request.EngineId,
            request.BuildId,
            request.HasOptions ? request.Options : null,
            corpora,
            context.CancellationToken
        );
        return Empty;
    }

    public override async Task<CancelBuildResponse> CancelBuild(CancelBuildRequest request, ServerCallContext context)
    {
        IWordAlignmentEngineService engineService = GetEngineService(request.EngineType);
        string buildId;
        try
        {
            buildId = await engineService.CancelBuildAsync(request.EngineId, context.CancellationToken);
        }
        catch (InvalidOperationException e)
        {
            throw new RpcException(new Status(StatusCode.Aborted, e.Message, e));
        }
        return new CancelBuildResponse() { BuildId = buildId };
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
        throw new RpcException(
            new Status(StatusCode.InvalidArgument, $"The engine type {engineTypeStr} is not supported.")
        );
    }

    private static EngineType GetEngineType(string engineTypeStr)
    {
        engineTypeStr = engineTypeStr[0].ToString().ToUpperInvariant() + engineTypeStr[1..];
        if (System.Enum.TryParse(engineTypeStr, out EngineType engineType))
            return engineType;
        throw new RpcException(
            new Status(StatusCode.InvalidArgument, $"The engine type {engineTypeStr} is not supported.")
        );
    }

    private static SIL.ServiceToolkit.Models.ParallelCorpus Map(WordAlignment.V1.ParallelCorpus source)
    {
        return new SIL.ServiceToolkit.Models.ParallelCorpus
        {
            Id = source.Id,
            SourceCorpora = source.SourceCorpora.Select(Map).ToList(),
            TargetCorpora = source.TargetCorpora.Select(Map).ToList()
        };
    }

    private static SIL.ServiceToolkit.Models.MonolingualCorpus Map(WordAlignment.V1.MonolingualCorpus source)
    {
        var trainOnChapters = source.TrainOnChapters.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Chapters.ToHashSet()
        );
        var trainOnTextIds = source.TrainOnTextIds.ToHashSet();
        FilterChoice trainingFilter = GetFilterChoice(trainOnChapters, trainOnTextIds, source.TrainOnAll);

        var wordAlignOnChapters = source.WordAlignOnChapters.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Chapters.ToHashSet()
        );
        var wordAlignOnTextIds = source.WordAlignOnTextIds.ToHashSet();
        FilterChoice wordAlignOnFilter = GetFilterChoice(
            wordAlignOnChapters,
            wordAlignOnTextIds,
            source.WordAlignOnAll
        );

        return new SIL.ServiceToolkit.Models.MonolingualCorpus
        {
            Id = source.Id,
            Language = source.Language,
            Files = source.Files.Select(Map).ToList(),
            TrainOnChapters = trainingFilter == FilterChoice.Chapters ? trainOnChapters : null,
            TrainOnTextIds = trainingFilter == FilterChoice.TextIds ? trainOnTextIds : null,
            InferenceChapters = wordAlignOnFilter == FilterChoice.Chapters ? wordAlignOnChapters : null,
            InferenceTextIds = wordAlignOnFilter == FilterChoice.TextIds ? wordAlignOnTextIds : null
        };
    }

    private static SIL.ServiceToolkit.Models.CorpusFile Map(WordAlignment.V1.CorpusFile source)
    {
        return new SIL.ServiceToolkit.Models.CorpusFile
        {
            Location = source.Location,
            Format = (SIL.ServiceToolkit.Models.FileFormat)source.Format,
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
        HashSet<string> textIds,
        bool noFilter
    )
    {
        // Only either textIds or Scripture Range will be used at a time
        // TextIds may be an empty array, so prefer that if both are empty (which applies to both scripture and text)
        if (noFilter || (chapters is null && textIds is null))
            return FilterChoice.None;
        if (chapters is null || chapters.Count == 0)
            return FilterChoice.TextIds;
        return FilterChoice.Chapters;
    }
}
