using Serval.WordAlignment.V1;

namespace EchoEngine;

public class WordAlignmentEngineServiceV1(BackgroundTaskQueue taskQueue)
    : WordAlignmentEngineApi.WordAlignmentEngineApiBase
{
    private static readonly Empty Empty = new();
    private readonly BackgroundTaskQueue _taskQueue = taskQueue;

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
                    using (
                        AsyncClientStreamingCall<InsertInferencesRequest, Empty> call = client.InsertInferences(
                            cancellationToken: cancellationToken
                        )
                    )
                    {
                        foreach (ParallelCorpus corpus in request.Corpora)
                        {
                            var sourceFiles = corpus
                                .SourceCorpora.SelectMany(sc =>
                                    sc.Files.Where(f =>
                                        (
                                            sc.WordAlignOnAll
                                            || sc.WordAlignOnTextIds is null
                                            || sc.WordAlignOnTextIds.Contains(f.TextId)
                                        )
                                        && f.Format == FileFormat.Text
                                    )
                                )
                                .ToDictionary(f => f.TextId, f => f.Location);
                            var targetFiles = corpus
                                .TargetCorpora.SelectMany(tc =>
                                    tc.Files.Where(f =>
                                        (
                                            tc.WordAlignOnAll
                                            || tc.WordAlignOnTextIds is null
                                            || tc.WordAlignOnTextIds.Contains(f.TextId)
                                        )
                                        && f.Format == FileFormat.Text
                                    )
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
                                                int minLength = Math.Min(
                                                    sourceLine.Split().Length,
                                                    targetLine.Split().Length
                                                );
                                                await call.RequestStream.WriteAsync(
                                                    new InsertInferencesRequest
                                                    {
                                                        EngineId = request.EngineId,
                                                        CorpusId = corpus.Id,
                                                        TextId = sourceFile.Key,
                                                        Refs = { $"{sourceFile.Key}:{lineNum}" },
                                                        SourceTokens = { sourceLine.Split() },
                                                        TargetTokens = { targetLine.Split() },
                                                        Confidences = { Enumerable.Repeat(1.0, minLength) },
                                                        Alignment = { GenerateAlignedWordPairs(minLength) }
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
                                                int minLength = Math.Min(
                                                    sourceLine.Split().Length,
                                                    targetLine.Split().Length
                                                );
                                                await call.RequestStream.WriteAsync(
                                                    new InsertInferencesRequest
                                                    {
                                                        EngineId = request.EngineId,
                                                        CorpusId = corpus.Id,
                                                        TextId = sourceFile.Key,
                                                        Refs = { $"{sourceFile.Key}:{targetLineKVPair.Key}" },
                                                        SourceTokens = { sourceLine.Split() },
                                                        TargetTokens = { targetLine.Split() },
                                                        Confidences =
                                                        {
                                                            Enumerable.Repeat(1.0, sourceLine.Split().Length)
                                                        },
                                                        Alignment = { GenerateAlignedWordPairs(minLength) }
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
}
