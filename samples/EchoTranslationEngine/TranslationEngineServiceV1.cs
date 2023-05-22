namespace EchoTranslationEngine;

public class TranslationEngineServiceV1 : TranslationEngineApi.TranslationEngineApiBase
{
    private static readonly Empty Empty = new();
    private readonly BackgroundTaskQueue _taskQueue;

    public TranslationEngineServiceV1(BackgroundTaskQueue taskQueue)
    {
        _taskQueue = taskQueue;
    }

    public override Task<Empty> Create(CreateRequest request, ServerCallContext context)
    {
        return Task.FromResult(Empty);
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

                await client.DeleteAllPretranslationsAsync(
                    new DeleteAllPretranslationsRequest { EngineId = request.EngineId },
                    cancellationToken: CancellationToken.None
                );
                using (var call = client.InsertPretranslations(cancellationToken: CancellationToken.None))
                {
                    foreach (Corpus corpus in request.Corpora)
                    {
                        if (!corpus.PretranslateAll && corpus.PretranslateTextIds.Count == 0)
                            continue;

                        var sourceFiles = corpus.SourceFiles
                            .Where(
                                f =>
                                    (corpus.PretranslateAll || corpus.PretranslateTextIds.Contains(f.TextId))
                                    && f.Format == FileFormat.Text
                            )
                            .ToDictionary(f => f.TextId, f => f.Location);
                        var targetFiles = corpus.TargetFiles
                            .Where(
                                f =>
                                    (corpus.PretranslateAll || corpus.PretranslateTextIds.Contains(f.TextId))
                                    && f.Format == FileFormat.Text
                            )
                            .ToDictionary(f => f.TextId, f => f.Location);

                        foreach (KeyValuePair<string, string> sourceFile in sourceFiles)
                        {
                            string[] sourceLines = await File.ReadAllLinesAsync(
                                sourceFile.Value,
                                CancellationToken.None
                            );
                            if (targetFiles.TryGetValue(sourceFile.Key, out string? targetPath))
                            {
                                string[] targetLines = await File.ReadAllLinesAsync(targetPath, CancellationToken.None);
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
                                            CancellationToken.None
                                        );
                                    }
                                    lineNum++;
                                }
                            }
                            else
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
                                            CancellationToken.None
                                        );
                                    }
                                    lineNum++;
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
        );

        return Empty;
    }
}
