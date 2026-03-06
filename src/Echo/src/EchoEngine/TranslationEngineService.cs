using Serval.EngineApi.Translation;
using Serval.Shared.Models;
using SIL.ServiceToolkit.Models;

namespace EchoEngine;

public class TranslationEngineService(
    BackgroundTaskQueue taskQueue,
    IParallelCorpusPreprocessingService parallelCorpusPreprocessingService
) : ITranslationEngine
{
    public string EngineType => "Echo";

    private readonly BackgroundTaskQueue _taskQueue = taskQueue;
    private readonly IParallelCorpusPreprocessingService _parallelCorpusPreprocessingService =
        parallelCorpusPreprocessingService;

    public Task CreateAsync(
        string engineId,
        string sourceLanguage,
        string targetLanguage,
        bool isModelPersisted,
        string? engineName = null,
        CancellationToken cancellationToken = default
    )
    {
        if (sourceLanguage != targetLanguage)
            throw new InvalidOperationException("Source and target languages must be the same");
        return Task.CompletedTask;
    }

    public Task<string?> CancelBuildAsync(string engineId, CancellationToken cancellationToken = default)
    {
        if (_taskQueue.ActiveBuilds.TryRemove(engineId, out (string buildId, CancellationTokenSource cts) build))
        {
            build.cts.Cancel();
            return Task.FromResult<string?>(build.buildId);
        }

        throw new InvalidOperationException("No build running");
    }

    public Task DeleteAsync(string engineId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task UpdateAsync(
        string engineId,
        string? sourceLanguage,
        string? targetLanguage,
        CancellationToken cancellationToken = default
    )
    {
        if (sourceLanguage != targetLanguage)
            throw new InvalidOperationException("Source and target languages must be the same");
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TranslationResult>> TranslateAsync(
        string engineId,
        int n,
        string segment,
        CancellationToken cancellationToken = default
    )
    {
        string[] tokens = segment.Split();
        IReadOnlyList<TranslationResult> results =
        [
            new TranslationResult
            {
                Translation = segment,
                SourceTokens = tokens,
                TargetTokens = tokens,
                Confidences = Enumerable.Repeat(1.0, tokens.Length).ToArray(),
                Sources = Enumerable
                    .Repeat<IReadOnlySet<TranslationSource>>(
                        new HashSet<TranslationSource> { TranslationSource.Primary },
                        tokens.Length
                    )
                    .ToList(),
                Alignment = Enumerable
                    .Range(0, tokens.Length)
                    .Select(i => new AlignedWordPair { SourceIndex = i, TargetIndex = i })
                    .ToList(),
                Phrases =
                [
                    new Phrase
                    {
                        SourceSegmentStart = 0,
                        SourceSegmentEnd = tokens.Length,
                        TargetSegmentCut = tokens.Length,
                    },
                ],
            },
        ];
        return Task.FromResult(results);
    }

    public Task<WordGraph> GetWordGraphAsync(
        string engineId,
        string segment,
        CancellationToken cancellationToken = default
    )
    {
        string[] tokens = segment.Split();
        var wordGraph = new WordGraph
        {
            InitialStateScore = 0.0,
            SourceTokens = tokens,
            FinalStates = new HashSet<int> { tokens.Length },
            Arcs = Enumerable
                .Range(0, tokens.Length - 1)
                .Select(index => new WordGraphArc
                {
                    PrevState = index,
                    NextState = index + 1,
                    Score = 1.0,
                    TargetTokens = [tokens[index]],
                    Confidences = [1.0],
                    SourceSegmentStart = index,
                    SourceSegmentEnd = index + 1,
                    Alignment = [new AlignedWordPair { SourceIndex = 0, TargetIndex = 0 }],
                    Sources = [new HashSet<TranslationSource> { TranslationSource.Primary }],
                })
                .ToList(),
        };
        return Task.FromResult(wordGraph);
    }

    public Task TrainSegmentPairAsync(
        string engineId,
        string sourceSegment,
        string targetSegment,
        bool sentenceStart,
        CancellationToken cancellationToken = default
    ) => Task.CompletedTask;

    public Task<ModelDownloadUrl?> GetModelDownloadUrlAsync(
        string engineId,
        CancellationToken cancellationToken = default
    )
    {
        return Task.FromResult<ModelDownloadUrl?>(
            new ModelDownloadUrl
            {
                Url = "https://example.com/model",
                ModelRevision = 1,
                ExpiresAt = DateTime.UtcNow.AddHours(1),
            }
        );
    }

    public Task<int> GetQueueSizeAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

    public Task<LanguageInfo> GetLanguageInfoAsync(string language, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new LanguageInfo { InternalCode = language + "_echo", IsNative = true });
    }

    public async Task StartBuildAsync(
        string engineId,
        string buildId,
        IReadOnlyList<FilteredParallelCorpus> corpora,
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
                    ITranslationPlatform platform = services.GetRequiredService<ITranslationPlatform>();
                    await platform.BuildCanceledAsync(buildId, CancellationToken.None);
                }
            );
            return;
        }

        await _taskQueue.QueueBackgroundWorkItemAsync(
            async (services, backgroundCt) =>
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(backgroundCt, cts.Token);
                ITranslationPlatform platform = services.GetRequiredService<ITranslationPlatform>();

                try
                {
                    await platform.BuildStartedAsync(buildId, linkedCts.Token);

                    int trainCount = 0;
                    int pretranslateCount = 0;

                    List<PretranslationData> pretranslations = [];
                    await _parallelCorpusPreprocessingService.PreprocessAsync(
                        corpora,
                        (row, _) =>
                        {
                            if (row.SourceSegment.Length > 0 && row.TargetSegment.Length > 0)
                                trainCount++;
                            return Task.CompletedTask;
                        },
                        (row, isInTrainingData, corpus) =>
                        {
                            string[] tokens = row.SourceSegment.Split();
                            pretranslations.Add(
                                new PretranslationData
                                {
                                    CorpusId = corpus.Id,
                                    TextId = row.TextId,
                                    SourceRefs = row.SourceRefs.Select(r => r.ToString()!).ToArray(),
                                    TargetRefs = row.TargetRefs.Select(r => r.ToString()!).ToArray(),
                                    Translation = row.SourceSegment,
                                    SourceTokens = tokens,
                                    TranslationTokens = tokens,
                                    Alignment = tokens
                                        .Select((_, i) => new AlignedWordPair { SourceIndex = i, TargetIndex = i })
                                        .ToList(),
                                }
                            );
                            if (row.SourceSegment.Length > 0 && !isInTrainingData)
                                pretranslateCount++;
                            if (cts.IsCancellationRequested)
                                throw new OperationCanceledException(cts.Token);
                            return Task.CompletedTask;
                        },
                        false
                    );

                    await platform.InsertPretranslationsAsync(
                        engineId,
                        ToAsyncEnumerable(pretranslations),
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
                        new ExecutionData
                        {
                            TrainCount = trainCount,
                            PretranslateCount = pretranslateCount,
                            EngineSourceLanguageTag = sourceLanguage,
                            EngineTargetLanguageTag = targetLanguage,
                            ResolvedSourceLanguage = sourceLanguage,
                            ResolvedTargetLanguage = targetLanguage,
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
