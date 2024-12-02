namespace EchoTranslationEngine;

public class TranslationEngineServiceV1(
    BackgroundTaskQueue taskQueue,
    IParallelCorpusPreprocessingService parallelCorpusPreprocessingService
) : TranslationEngineApi.TranslationEngineApiBase
{
    private static readonly Empty Empty = new();
    private readonly BackgroundTaskQueue _taskQueue = taskQueue;

    private readonly IParallelCorpusPreprocessingService _parallelCorpusPreprocessingService =
        parallelCorpusPreprocessingService;

    public override Task<CreateResponse> Create(CreateRequest request, ServerCallContext context)
    {
        if (request.SourceLanguage != request.TargetLanguage)
        {
            Status status = new Status(StatusCode.InvalidArgument, "Source and target languages must be the same");
            throw new RpcException(status);
        }
        return Task.FromResult(new CreateResponse { IsModelPersisted = true });
    }

    public override Task<Empty> Delete(DeleteRequest request, ServerCallContext context)
    {
        return Task.FromResult(Empty);
    }

    public override Task<TranslateResponse> Translate(TranslateRequest request, ServerCallContext context)
    {
        string[] tokens = request.Segment.Split();
        var response = new TranslateResponse
        {
            Results =
            {
                new TranslationResult
                {
                    Translation = request.Segment,
                    SourceTokens = { tokens },
                    TargetTokens = { tokens },
                    Confidences = { Enumerable.Repeat(1.0, tokens.Length) },
                    Sources =
                    {
                        Enumerable.Repeat(
                            new TranslationSources { Values = { TranslationSource.Primary } },
                            tokens.Length
                        )
                    },
                    Alignment =
                    {
                        Enumerable
                            .Range(0, tokens.Length)
                            .Select(i => new AlignedWordPair { SourceIndex = i, TargetIndex = i })
                    },
                    Phrases =
                    {
                        new Phrase
                        {
                            SourceSegmentStart = 0,
                            SourceSegmentEnd = tokens.Length,
                            TargetSegmentCut = tokens.Length
                        }
                    }
                }
            }
        };
        return Task.FromResult(response);
    }

    public override async Task<Empty> StartBuild(StartBuildRequest request, ServerCallContext context)
    {
        await _taskQueue.QueueBackgroundWorkItemAsync(
            async (services, cancellationToken) =>
            {
                TranslationPlatformApi.TranslationPlatformApiClient client =
                    services.GetRequiredService<TranslationPlatformApi.TranslationPlatformApiClient>();
                await client.BuildStartedAsync(
                    new BuildStartedRequest { BuildId = request.BuildId },
                    cancellationToken: cancellationToken
                );

                try
                {
                    List<InsertPretranslationsRequest> pretranslationsRequests = [];
                    await _parallelCorpusPreprocessingService.Preprocess(
                        request.Corpora.Select(Map).ToList(),
                        row => Task.CompletedTask,
                        (row, corpus) =>
                        {
                            pretranslationsRequests.Add(
                                new InsertPretranslationsRequest
                                {
                                    EngineId = request.EngineId,
                                    CorpusId = corpus.Id,
                                    TextId = row.TextId,
                                    Refs = { row.Refs.Select(r => r.ToString()) },
                                    Translation = row.SourceSegment
                                }
                            );
                            return Task.CompletedTask;
                        },
                        false
                    );
                    using (
                        AsyncClientStreamingCall<InsertPretranslationsRequest, Empty> call =
                            client.InsertPretranslations(cancellationToken: cancellationToken)
                    )
                    {
                        foreach (InsertPretranslationsRequest request in pretranslationsRequests)
                        {
                            await call.RequestStream.WriteAsync(request, cancellationToken);
                        }
                        await call.RequestStream.CompleteAsync();
                        await call;
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

    public override Task<Empty> TrainSegmentPair(TrainSegmentPairRequest request, ServerCallContext context)
    {
        return Task.FromResult(Empty);
    }

    public override Task<GetWordGraphResponse> GetWordGraph(GetWordGraphRequest request, ServerCallContext context)
    {
        string[] tokens = request.Segment.Split();
        return Task.FromResult(
            new GetWordGraphResponse
            {
                WordGraph = new WordGraph
                {
                    InitialStateScore = 0.0,
                    SourceTokens = { tokens },
                    Arcs =
                    {
                        Enumerable
                            .Range(0, tokens.Length - 1)
                            .Select(index => new WordGraphArc
                            {
                                PrevState = index,
                                NextState = index + 1,
                                Score = 1.0,
                                TargetTokens = { tokens[index] },
                                Confidences = { 1.0 },
                                SourceSegmentStart = index,
                                SourceSegmentEnd = index + 1,
                                Alignment =
                                {
                                    new AlignedWordPair { SourceIndex = 0, TargetIndex = 0 }
                                }
                            })
                    },
                    FinalStates = { tokens.Length }
                }
            }
        );
    }

    public override Task<GetModelDownloadUrlResponse> GetModelDownloadUrl(
        GetModelDownloadUrlRequest request,
        ServerCallContext context
    )
    {
        var response = new GetModelDownloadUrlResponse
        {
            Url = "https://example.com/model",
            ModelRevision = 1,
            ExpiresAt = DateTime.UtcNow.AddHours(1).ToTimestamp()
        };
        return Task.FromResult(response);
    }

    public override Task<GetQueueSizeResponse> GetQueueSize(GetQueueSizeRequest request, ServerCallContext context)
    {
        return Task.FromResult(new GetQueueSizeResponse { Size = 0 });
    }

    public override Task<GetLanguageInfoResponse> GetLanguageInfo(
        GetLanguageInfoRequest request,
        ServerCallContext context
    )
    {
        return Task.FromResult(
            new GetLanguageInfoResponse { InternalCode = request.Language + "_echo", IsNative = true, }
        );
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

        var pretranslateChapters = source.PretranslateChapters.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Chapters.ToHashSet()
        );
        var pretranslateTextIds = source.PretranslateTextIds.ToHashSet();
        FilterChoice pretranslateFilter = GetFilterChoice(
            pretranslateChapters,
            pretranslateTextIds,
            source.PretranslateAll
        );

        return new SIL.ServiceToolkit.Models.MonolingualCorpus
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
