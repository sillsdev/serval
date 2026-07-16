namespace Serval.Machine.Shared.Services;

public class EchoEngineService(
    [FromKeyedServices(EngineGroup.Translation)] IPlatformService platformService,
    IRepository<TranslationEngine> engines,
    IBuildJobService<TranslationEngine> buildJobService
) : ITranslationEngineService
{
    public async Task CreateAsync(
        string engineId,
        string sourceLanguage,
        string targetLanguage,
        string? engineName = null,
        bool? isModelPersisted = null,
        CancellationToken cancellationToken = default
    )
    {
        if (sourceLanguage != targetLanguage)
            throw new InvalidOperationException("Source and target languages must be the same");
        try
        {
            var translationEngine = new TranslationEngine
            {
                EngineId = engineId,
                SourceLanguage = sourceLanguage,
                TargetLanguage = targetLanguage,
                Type = EngineType.Echo,
                IsModelPersisted = isModelPersisted ?? false, // Simulate the behavior of NMT for model persistence
            };
            await engines.InsertAsync(translationEngine, cancellationToken);
        }
        catch (DuplicateKeyException)
        {
            // this method is idempotent, so ignore if the engine already exists
        }
    }

    public async Task DeleteAsync(string engineId, CancellationToken cancellationToken = default)
    {
        await CancelBuildJobAsync(engineId, cancellationToken);
        await engines.DeleteAsync(e => e.EngineId == engineId, cancellationToken);
        await buildJobService.DeleteEngineAsync(engineId, cancellationToken);
    }

    public async Task UpdateAsync(
        string engineId,
        string? sourceLanguage,
        string? targetLanguage,
        CancellationToken cancellationToken = default
    )
    {
        if (sourceLanguage != targetLanguage)
            throw new InvalidOperationException("Source and target languages must be the same");

        await CancelBuildJobAsync(engineId, cancellationToken);

        await engines.UpdateAsync(
            e => e.EngineId == engineId,
            u =>
            {
                if (sourceLanguage is not null)
                    u.Set(e => e.SourceLanguage, sourceLanguage);
                if (targetLanguage is not null)
                    u.Set(e => e.TargetLanguage, targetLanguage);
            },
            cancellationToken: cancellationToken
        );
    }

    public async Task StartBuildAsync(
        string engineId,
        string buildId,
        IReadOnlyList<ParallelCorpusContract> corpora,
        string? options = null,
        CancellationToken cancellationToken = default
    )
    {
        bool building = !await buildJobService.StartBuildJobAsync(
            BuildJobRunnerType.Local,
            EngineType.Echo,
            engineId,
            buildId,
            BuildStage.Preprocess,
            corpora,
            options,
            cancellationToken
        );
        // If there is a pending/running build, then no need to start a new one.
        if (building)
            await platformService.BuildCanceledAsync(buildId, CancellationToken.None);
    }

    public Task<string?> CancelBuildAsync(string engineId, CancellationToken cancellationToken = default) =>
        CancelBuildJobAsync(engineId, cancellationToken);

    public async Task<ModelDownloadUrlContract> GetModelDownloadUrlAsync(
        string engineId,
        CancellationToken cancellationToken = default
    ) =>
        new()
        {
            Url = "https://example.com/model",
            ModelRevision = 1,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
        };

    public Task<IReadOnlyList<TranslationResultContract>> TranslateAsync(
        string engineId,
        int n,
        string segment,
        CancellationToken cancellationToken = default
    )
    {
        string[] tokens = segment.Split();
        IReadOnlyList<TranslationResultContract> results =
        [
            new TranslationResultContract
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
                    .Select(i => new AlignedWordPairContract { SourceIndex = i, TargetIndex = i })
                    .ToList(),
                Phrases =
                [
                    new PhraseContract
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

    public Task<WordGraphContract> GetWordGraphAsync(
        string engineId,
        string segment,
        CancellationToken cancellationToken = default
    )
    {
        string[] tokens = segment.Split();
        var wordGraph = new WordGraphContract
        {
            InitialStateScore = 0.0,
            SourceTokens = tokens,
            FinalStates = new HashSet<int> { tokens.Length },
            Arcs = Enumerable
                .Range(0, tokens.Length - 1)
                .Select(index => new WordGraphArcContract
                {
                    PrevState = index,
                    NextState = index + 1,
                    Score = 1.0,
                    TargetTokens = [tokens[index]],
                    Confidences = [1.0],
                    SourceSegmentStart = index,
                    SourceSegmentEnd = index + 1,
                    Alignment = [new AlignedWordPairContract { SourceIndex = 0, TargetIndex = 0 }],
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

    public Task<int> GetQueueSizeAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

    public Task<LanguageInfoContract> GetLanguageInfoAsync(
        string language,
        CancellationToken cancellationToken = default
    ) => Task.FromResult(new LanguageInfoContract { InternalCode = language + "_echo", IsNative = true });

    private async Task<string?> CancelBuildJobAsync(string engineId, CancellationToken cancellationToken)
    {
        (string? buildId, BuildJobState jobState) = await buildJobService.CancelBuildJobAsync(
            engineId,
            cancellationToken
        );
        if (buildId is not null && jobState is BuildJobState.None)
            await platformService.BuildCanceledAsync(buildId, CancellationToken.None);
        return buildId;
    }
}
