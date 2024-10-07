namespace EchoTranslationEngine;

public class TranslationEngineServiceV1(BackgroundTaskQueue taskQueue) : EngineApi.EngineApiBase
{
    private static readonly Empty Empty = new();
    private readonly BackgroundTaskQueue _taskQueue = taskQueue;

    public override Task<CreateResponse> Create(CreateRequest request, ServerCallContext context)
    {
        var parameters = JsonSerializer.Deserialize<CreateEngineParameters>(request.ParametersSerialized)!;
        if (parameters.SourceLanguage != parameters.TargetLanguage)
        {
            Status status = new Status(StatusCode.InvalidArgument, "Source and target languages must be the same");
            throw new RpcException(status);
        }
        return Task.FromResult(
            new CreateResponse
            {
                ResultsSerialized = JsonSerializer.Serialize(new CreateEngineResults() { IsModelPersisted = true })
            }
        );
    }

    public override Task<Empty> Delete(DeleteRequest request, ServerCallContext context)
    {
        return Task.FromResult(Empty);
    }

    public override async Task<StartJobResponse> StartJob(StartJobRequest request, ServerCallContext context)
    {
        await _taskQueue.QueueBackgroundWorkItemAsync(
            async (services, cancellationToken) =>
            {
                EnginePlatformApi.EnginePlatformApiClient client =
                    services.GetRequiredService<EnginePlatformApi.EnginePlatformApiClient>();
                await client.JobStartedAsync(
                    new JobStartedRequest { JobId = request.JobId },
                    cancellationToken: cancellationToken
                );

                try
                {
                    using (
                        AsyncClientStreamingCall<InsertResultsRequest, Empty> call = client.InsertResults(
                            cancellationToken: cancellationToken
                        )
                    )
                    {
                        foreach (
                            TranslationCorpus corpus in JsonSerializer.Deserialize<IEnumerable<TranslationCorpus>>(
                                request.CorporaSerialized
                            )!
                        )
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
                                                    new InsertResultsRequest
                                                    {
                                                        EngineId = request.EngineId,
                                                        ContentSerialized = JsonSerializer.Serialize(
                                                            new TranslationResultContent
                                                            {
                                                                CorpusId = corpus.Id,
                                                                TextId = sourceFile.Key,
                                                                Refs = { $"{sourceFile.Key}:{lineNum}" },
                                                                Translation = sourceLine
                                                            }
                                                        )
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
                                                    new InsertResultsRequest
                                                    {
                                                        EngineId = request.EngineId,
                                                        ContentSerialized = JsonSerializer.Serialize(
                                                            new TranslationResultContent
                                                            {
                                                                CorpusId = corpus.Id,
                                                                TextId = sourceFile.Key,
                                                                Refs = { $"{sourceFile.Key}:{targetLineKVPair.Key}" },
                                                                Translation = sourceLine
                                                            }
                                                        )
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
                                                    new InsertResultsRequest
                                                    {
                                                        EngineId = request.EngineId,
                                                        ContentSerialized = JsonSerializer.Serialize(
                                                            new TranslationResultContent
                                                            {
                                                                CorpusId = corpus.Id,
                                                                TextId = sourceFile.Key,
                                                                Refs = { $"{sourceFile.Key}:{lineNum}" },
                                                                Translation = sourceLine
                                                            }
                                                        )
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
                                                    new InsertResultsRequest
                                                    {
                                                        EngineId = request.EngineId,
                                                        ContentSerialized = JsonSerializer.Serialize(
                                                            new TranslationResultContent
                                                            {
                                                                CorpusId = corpus.Id,
                                                                TextId = sourceFile.Key,
                                                                Refs =
                                                                {
                                                                    $"{sourceFile.Key}:{sourceLine.Split('\t')[0]}"
                                                                },
                                                                Translation = sourceLine.Contains('\t')
                                                                    ? sourceLine.Split('\t')[1].Trim()
                                                                    : string.Empty
                                                            }
                                                        )
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

                    await client.JobCompletedAsync(
                        new JobCompletedRequest
                        {
                            JobId = request.JobId,
                            StatisticsSerialized = JsonSerializer.Serialize(
                                new TranslationEngineCompletedStatistics() { CorpusSize = 0, Confidence = 1.0F }
                            )
                        },
                        cancellationToken: CancellationToken.None
                    );
                }
                catch (OperationCanceledException)
                {
                    await client.JobCanceledAsync(
                        new JobCanceledRequest { JobId = request.JobId },
                        cancellationToken: CancellationToken.None
                    );
                }
                catch (Exception e)
                {
                    await client.JobFaultedAsync(
                        new JobFaultedRequest { JobId = request.JobId, Message = e.Message },
                        cancellationToken: CancellationToken.None
                    );
                }
            }
        );

        return new StartJobResponse();
    }

    public override Task<GetQueueSizeResponse> GetQueueSize(GetQueueSizeRequest request, ServerCallContext context)
    {
        return Task.FromResult(new GetQueueSizeResponse { Size = 0 });
    }
}
