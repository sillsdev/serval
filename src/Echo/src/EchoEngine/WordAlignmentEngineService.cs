using Serval.Shared.Contracts;
using Serval.WordAlignment.Contracts;

namespace EchoEngine;

public class WordAlignmentEngineService(BackgroundTaskQueue taskQueue, IParallelCorpusService parallelCorpusService)
    : IWordAlignmentEngineService
{
    private readonly BackgroundTaskQueue _taskQueue = taskQueue;
    private readonly IParallelCorpusService _parallelCorpusService = parallelCorpusService;

    public Task CreateAsync(
        string engineId,
        string sourceLanguage,
        string targetLanguage,
        string? engineName = null,
        CancellationToken cancellationToken = default
    )
    {
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string engineId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<WordAlignmentResultContract> AlignAsync(
        string engineId,
        string sourceSegment,
        string targetSegment,
        CancellationToken cancellationToken = default
    )
    {
        string[] sourceTokens = sourceSegment.Split();
        string[] targetTokens = targetSegment.Split();
        int minLength = Math.Min(sourceTokens.Length, targetTokens.Length);

        var result = new WordAlignmentResultContract
        {
            SourceTokens = sourceTokens,
            TargetTokens = targetTokens,
            Alignment = Enumerable
                .Range(0, minLength)
                .Select(i => new AlignedWordPairContract
                {
                    SourceIndex = i,
                    TargetIndex = i,
                    Score = 1.0,
                })
                .ToList(),
        };
        return Task.FromResult(result);
    }

    public Task<string?> CancelBuildAsync(string engineId, CancellationToken cancellationToken = default)
    {
        if (_taskQueue.ActiveBuilds.TryRemove(engineId, out (string buildId, CancellationTokenSource cts) build))
        {
            build.cts.Cancel();
            return Task.FromResult<string?>(build.buildId);
        }

        return Task.FromResult<string?>(null);
    }

    public Task<int> GetQueueSizeAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

    public async Task StartBuildAsync(
        string engineId,
        string buildId,
        IReadOnlyList<ParallelCorpusContract> corpora,
        string? options = null,
        CancellationToken cancellationToken = default
    )
    {
        var cts = new CancellationTokenSource();
        if (!_taskQueue.ActiveBuilds.TryAdd(engineId, (buildId, cts)))
        {
            await _taskQueue.QueueBackgroundWorkItemAsync(
                async (services, token) =>
                {
                    IWordAlignmentPlatformService platform =
                        services.GetRequiredService<IWordAlignmentPlatformService>();
                    await platform.BuildCanceledAsync(buildId, CancellationToken.None);
                }
            );
            return;
        }

        await _taskQueue.QueueBackgroundWorkItemAsync(
            async (services, backgroundCt) =>
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(backgroundCt, cts.Token);
                IWordAlignmentPlatformService platform = services.GetRequiredService<IWordAlignmentPlatformService>();

                try
                {
                    await platform.BuildStartedAsync(buildId, linkedCts.Token);

                    int trainCount = 0;
                    int wordAlignCount = 0;

                    List<WordAlignmentContract> wordAlignments = [];
                    await _parallelCorpusService.PreprocessAsync(
                        corpora,
                        (row, _) =>
                        {
                            if (row.SourceSegment.Length > 0 && row.TargetSegment.Length > 0)
                                trainCount++;
                            return Task.CompletedTask;
                        },
                        (row, isInTrainingData, corpusId) =>
                        {
                            string[] sourceTokens = row.SourceSegment.Split();
                            string[] targetTokens = row.TargetSegment.Split();
                            int minLength = Math.Min(sourceTokens.Length, targetTokens.Length);

                            wordAlignments.Add(
                                new WordAlignmentContract
                                {
                                    CorpusId = corpusId,
                                    TextId = row.TextId,
                                    SourceRefs = row.SourceRefs.Select(r => r.ToString()!).ToArray(),
                                    TargetRefs = row.TargetRefs.Select(r => r.ToString()!).ToArray(),
                                    SourceTokens = sourceTokens,
                                    TargetTokens = targetTokens,
                                    Alignment = Enumerable
                                        .Range(0, minLength)
                                        .Select(i => new AlignedWordPairContract { SourceIndex = i, TargetIndex = i })
                                        .ToList(),
                                }
                            );
                            if (row.SourceSegment.Length > 0 && row.TargetSegment.Length > 0 && !isInTrainingData)
                                wordAlignCount++;
                            if (cts.IsCancellationRequested)
                                throw new OperationCanceledException(cts.Token);
                            return Task.CompletedTask;
                        },
                        false
                    );

                    await platform.InsertWordAlignmentsAsync(
                        engineId,
                        ToAsyncEnumerable(wordAlignments),
                        linkedCts.Token
                    );

                    string sourceLanguage =
                        corpora.Count > 0 && corpora[0].SourceCorpora.Count > 0
                            ? corpora[0].SourceCorpora[0].Language
                            : string.Empty;
                    string targetLanguage =
                        corpora.Count > 0 && corpora[0].TargetCorpora.Count > 0
                            ? corpora[0].TargetCorpora[0].Language
                            : string.Empty;

                    await platform.UpdateBuildExecutionDataAsync(
                        engineId,
                        buildId,
                        new Serval.WordAlignment.Contracts.ExecutionDataContract
                        {
                            TrainCount = trainCount,
                            WordAlignCount = wordAlignCount,
                            EngineSourceLanguageTag = sourceLanguage,
                            EngineTargetLanguageTag = targetLanguage,
                        },
                        linkedCts.Token
                    );

                    await platform.BuildCompletedAsync(buildId, 0, 1.0, CancellationToken.None);
                }
                catch (OperationCanceledException)
                {
                    await platform.BuildCanceledAsync(buildId, CancellationToken.None);
                }
                catch (Exception e)
                {
                    if (cts.IsCancellationRequested)
                    {
                        await platform.BuildCanceledAsync(buildId, CancellationToken.None);
                    }
                    else
                    {
                        await platform.BuildFaultedAsync(buildId, e.Message, CancellationToken.None);
                    }
                }
                finally
                {
                    _taskQueue.ActiveBuilds.TryRemove(engineId, out _);
                }
            }
        );
    }

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> source)
    {
        foreach (T item in source)
            yield return item;
    }
}
