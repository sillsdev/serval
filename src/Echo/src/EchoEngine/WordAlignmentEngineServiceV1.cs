using Serval.WordAlignment.V1;

namespace EchoWordAlignmentEngine;

public class WordAlignmentEngineServiceV1(
    BackgroundTaskQueue taskQueue,
    IParallelCorpusPreprocessingService parallelCorpusPreprocessingService
) : WordAlignmentEngineApi.WordAlignmentEngineApiBase
{
    private static readonly Empty Empty = new();
    private readonly BackgroundTaskQueue _taskQueue = taskQueue;
    private readonly IParallelCorpusPreprocessingService _parallelCorpusPreprocessingService =
        parallelCorpusPreprocessingService;

    public override Task<Empty> Create(CreateRequest request, ServerCallContext context)
    {
        if (request.SourceLanguage != request.TargetLanguage)
        {
            Status status = new Status(StatusCode.InvalidArgument, "Source and target languages must be the same");
            throw new RpcException(status);
        }
        return Task.FromResult(Empty);
    }

    public override Task<Empty> Delete(DeleteRequest request, ServerCallContext context)
    {
        return Task.FromResult(Empty);
    }

    public static IEnumerable<AlignedWordPair> GenerateAlignedWordPairs(int number)
    {
        if (number < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(number), "Number must be non-negative");
        }
        return Enumerable.Range(0, number).Select(i => new AlignedWordPair { SourceIndex = i, TargetIndex = i });
    }

    public override Task<GetWordAlignmentResponse> GetWordAlignment(
        GetWordAlignmentRequest request,
        ServerCallContext context
    )
    {
        string[] sourceTokens = request.SourceSegment.Split();
        string[] targetTokens = request.TargetSegment.Split();
        int minLength = Math.Min(sourceTokens.Length, targetTokens.Length);

        var response = new GetWordAlignmentResponse
        {
            Result = new WordAlignmentResult
            {
                SourceTokens = { sourceTokens },
                TargetTokens = { targetTokens },
                Confidences = { Enumerable.Repeat(1.0, minLength) },
                Alignment = { GenerateAlignedWordPairs(minLength) }
            }
        };
        return Task.FromResult(response);
    }

    public override async Task<Empty> StartBuild(StartBuildRequest request, ServerCallContext context)
    {
        await _taskQueue.QueueBackgroundWorkItemAsync(
            async (services, cancellationToken) =>
            {
                WordAlignmentPlatformApi.WordAlignmentPlatformApiClient client =
                    services.GetRequiredService<WordAlignmentPlatformApi.WordAlignmentPlatformApiClient>();
                await client.BuildStartedAsync(
                    new BuildStartedRequest { BuildId = request.BuildId },
                    cancellationToken: cancellationToken
                );

                try
                {
                    List<InsertWordAlignmentsRequest> wordAlignmentsRequests = [];
                    await _parallelCorpusPreprocessingService.PreprocessAsync(
                        request.Corpora.Select(Map).ToList(),
                        row => Task.CompletedTask,
                        (row, _, corpus) =>
                        {
                            wordAlignmentsRequests.Add(
                                new InsertWordAlignmentsRequest
                                {
                                    EngineId = request.EngineId,
                                    CorpusId = corpus.Id,
                                    TextId = row.TextId,
                                    Refs = { row.Refs.Select(r => r.ToString()) },
                                    SourceTokens = { row.SourceSegment.Split() },
                                    TargetTokens = { row.TargetSegment.Split() },
                                    Alignment =
                                    {
                                        row.SourceSegment.Split()
                                            .Select(
                                                (_, i) => new AlignedWordPair() { SourceIndex = i, TargetIndex = i }
                                            )
                                    },
                                    Confidences = { row.SourceSegment.Split().Select(_ => 1.0) }
                                }
                            );
                            return Task.CompletedTask;
                        },
                        false
                    );
                    using (
                        AsyncClientStreamingCall<InsertWordAlignmentsRequest, Empty> call = client.InsertWordAlignments(
                            cancellationToken: cancellationToken
                        )
                    )
                    {
                        foreach (InsertWordAlignmentsRequest request in wordAlignmentsRequests)
                        {
                            await call.RequestStream.WriteAsync(request, cancellationToken);
                        }
                    }

                    await client.BuildCompletedAsync(
                        new BuildCompletedRequest { BuildId = request.BuildId, Confidence = 1.0 },
                        cancellationToken: CancellationToken.None
                    );
                }
                catch (OperationCanceledException)
                {
                    await client.BuildCanceledAsync(
                        new BuildCanceledRequest { BuildId = request.BuildId },
                        cancellationToken: CancellationToken.None
                    );
                }
                catch (Exception e)
                {
                    await client.BuildFaultedAsync(
                        new BuildFaultedRequest { BuildId = request.BuildId, Message = e.Message },
                        cancellationToken: CancellationToken.None
                    );
                }
            }
        );

        return Empty;
    }

    private static SIL.ServiceToolkit.Models.ParallelCorpus Map(ParallelCorpus source)
    {
        return new SIL.ServiceToolkit.Models.ParallelCorpus
        {
            Id = source.Id,
            SourceCorpora = source.SourceCorpora.Select(Map).ToList(),
            TargetCorpora = source.TargetCorpora.Select(Map).ToList()
        };
    }

    private static SIL.ServiceToolkit.Models.MonolingualCorpus Map(MonolingualCorpus source)
    {
        var trainOnChapters = source.TrainOnChapters.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Chapters.ToHashSet()
        );
        var trainOnTextIds = source.TrainOnTextIds.ToHashSet();
        FilterChoice trainingFilter = GetFilterChoice(trainOnChapters, trainOnTextIds, source.TrainOnAll);

        var wordAlignChapters = source.WordAlignOnChapters.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Chapters.ToHashSet()
        );
        var wordAlignTextIds = source.WordAlignOnTextIds.ToHashSet();
        FilterChoice wordAlignFilter = GetFilterChoice(wordAlignChapters, wordAlignTextIds, source.WordAlignOnAll);

        return new SIL.ServiceToolkit.Models.MonolingualCorpus
        {
            Id = source.Id,
            Language = source.Language,
            Files = source.Files.Select(Map).ToList(),
            TrainOnChapters = trainingFilter == FilterChoice.Chapters ? trainOnChapters : null,
            TrainOnTextIds = trainingFilter == FilterChoice.TextIds ? trainOnTextIds : null,
            InferenceChapters = wordAlignFilter == FilterChoice.Chapters ? wordAlignChapters : null,
            InferenceTextIds = wordAlignFilter == FilterChoice.TextIds ? wordAlignTextIds : null
        };
    }

    private static SIL.ServiceToolkit.Models.CorpusFile Map(CorpusFile source)
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
