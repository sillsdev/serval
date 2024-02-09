namespace EchoTranslationEngine;

public class TranslationEngineServiceV1(BackgroundTaskQueue taskQueue, HealthCheckService healthCheckService)
    : TranslationEngineApi.TranslationEngineApiBase
{
    private static readonly Empty Empty = new();
    private readonly BackgroundTaskQueue _taskQueue = taskQueue;

    private readonly HealthCheckService _healthCheckService = healthCheckService;

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
                var client = services.GetRequiredService<TranslationPlatformApi.TranslationPlatformApiClient>();
                await client.BuildStartedAsync(
                    new BuildStartedRequest { BuildId = request.BuildId },
                    cancellationToken: cancellationToken
                );

                try
                {
                    using (var call = client.InsertPretranslations(cancellationToken: cancellationToken))
                    {
                        foreach (Corpus corpus in request.Corpora)
                        {
                            if (!corpus.PretranslateAll && corpus.PretranslateTextIds.Count == 0)
                                continue;

                            var sourceFiles = corpus
                                .SourceFiles.Where(f =>
                                    (corpus.PretranslateAll || corpus.PretranslateTextIds.Contains(f.TextId))
                                    && f.Format == FileFormat.Text
                                )
                                .ToDictionary(f => f.TextId, f => f.Location);
                            var targetFiles = corpus
                                .TargetFiles.Where(f =>
                                    (corpus.PretranslateAll || corpus.PretranslateTextIds.Contains(f.TextId))
                                    && f.Format == FileFormat.Text
                                )
                                .ToDictionary(f => f.TextId, f => f.Location);

                            foreach (KeyValuePair<string, string> sourceFile in sourceFiles)
                            {
                                string[] sourceLines = await File.ReadAllLinesAsync(
                                    sourceFile.Value,
                                    cancellationToken
                                );

                                if (targetFiles.TryGetValue(sourceFile.Key, out string? targetPath))
                                {
                                    string[] targetLines = await File.ReadAllLinesAsync(targetPath, cancellationToken);
                                    bool isTabSeparated = (sourceLines.Length > 0) && sourceLines[0].Contains('/');
                                    if (!isTabSeparated)
                                    {
                                        int lineNum = 1;
                                        foreach (
                                            (string sourceLine, string targetLine) in sourceLines
                                                .Select(l => l.Trim())
                                                .Zip(targetLines.Select(l => l.Trim()))
                                        )
                                        {
                                            if (sourceLine.Length > 0 && targetLine.Length == 0)
                                            {
                                                await call.RequestStream.WriteAsync(
                                                    new InsertPretranslationRequest
                                                    {
                                                        EngineId = request.EngineId,
                                                        CorpusId = corpus.Id,
                                                        TextId = sourceFile.Key,
                                                        Refs = { $"{sourceFile.Key}:{lineNum}" },
                                                        Translation = sourceLine
                                                    },
                                                    cancellationToken
                                                );
                                            }
                                            lineNum++;
                                        }
                                    }
                                    else
                                    {
                                        var sourceLinesDict = sourceLines.ToDictionary(
                                            l => l.Split('\t')[0].Trim(),
                                            l => l.Split('\t')[1].Trim()
                                        );
                                        var targetLinesDict = targetLines.ToDictionary(
                                            l => l.Split('\t')[0].Trim(),
                                            l => l.Contains('\t') ? l.Split('\t')[1].Trim() : string.Empty
                                        );
                                        foreach (KeyValuePair<string, string> targetLineKVPair in targetLinesDict)
                                        {
                                            string? sourceLine = null;
                                            sourceLinesDict.TryGetValue(targetLineKVPair.Key, out sourceLine);
                                            sourceLine ??= string.Empty;
                                            string? targetLine = targetLineKVPair.Value;
                                            if (sourceLine.Length > 0 && targetLine.Length == 0)
                                            {
                                                await call.RequestStream.WriteAsync(
                                                    new InsertPretranslationRequest
                                                    {
                                                        EngineId = request.EngineId,
                                                        CorpusId = corpus.Id,
                                                        TextId = sourceFile.Key,
                                                        Refs = { $"{sourceFile.Key}:{targetLineKVPair.Key}" },
                                                        Translation = sourceLine
                                                    },
                                                    cancellationToken
                                                );
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    bool isTabSeparated = (sourceLines.Length > 0) && sourceLines[0].Contains('/');
                                    if (!isTabSeparated)
                                    {
                                        int lineNum = 1;
                                        foreach (string sourceLine in sourceLines.Select(l => l.Trim()))
                                        {
                                            if (sourceLine.Length > 0)
                                            {
                                                await call.RequestStream.WriteAsync(
                                                    new InsertPretranslationRequest
                                                    {
                                                        EngineId = request.EngineId,
                                                        CorpusId = corpus.Id,
                                                        TextId = sourceFile.Key,
                                                        Refs = { $"{sourceFile.Key}:{lineNum}" },
                                                        Translation = sourceLine
                                                    },
                                                    cancellationToken
                                                );
                                            }
                                            lineNum++;
                                        }
                                    }
                                    else
                                    {
                                        foreach (string sourceLine in sourceLines.Select(l => l.Trim()))
                                        {
                                            if (sourceLine.Length > 0)
                                            {
                                                await call.RequestStream.WriteAsync(
                                                    new InsertPretranslationRequest
                                                    {
                                                        EngineId = request.EngineId,
                                                        CorpusId = corpus.Id,
                                                        TextId = sourceFile.Key,
                                                        Refs = { $"{sourceFile.Key}:{sourceLine.Split('\t')[0]}" },
                                                        Translation = sourceLine.Contains('\t')
                                                            ? sourceLine.Split('\t')[1].Trim()
                                                            : string.Empty
                                                    },
                                                    cancellationToken
                                                );
                                            }
                                        }
                                    }
                                }
                            }
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

    public override Task<Empty> TrainSegmentPair(TrainSegmentPairRequest request, ServerCallContext _)
    {
        return Task.FromResult(Empty);
    }

    public override Task<GetWordGraphResponse> GetWordGraph(GetWordGraphRequest request, ServerCallContext _)
    {
        var tokens = request.Segment.Split();
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
            new GetLanguageInfoResponse { InternalCode = request.Language + "_echo", IsNative = false, }
        );
    }

    public override async Task<HealthCheckResponse> HealthCheck(Empty request, ServerCallContext context)
    {
        HealthReport healthReport = await _healthCheckService.CheckHealthAsync();
        HealthCheckResponse healthCheckResponse = WriteGrpcHealthCheckResponse.Generate(healthReport);
        return healthCheckResponse;
    }
}
