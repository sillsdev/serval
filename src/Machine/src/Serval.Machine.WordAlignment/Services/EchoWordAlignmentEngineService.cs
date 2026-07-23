namespace Serval.Machine.WordAlignment.Services;

public class EchoWordAlignmentEngineService(
    [FromKeyedServices(EngineGroup.WordAlignment)] IPlatformService platformService,
    IRepository<WordAlignmentEngine> engines,
    IBuildJobService<WordAlignmentEngine> buildJobService
) : IWordAlignmentEngineService
{
    public async Task CreateAsync(
        string engineId,
        string sourceLanguage,
        string targetLanguage,
        string? engineName = null,
        CancellationToken cancellationToken = default
    )
    {
        if (sourceLanguage != targetLanguage)
            throw new InvalidOperationException("Source and target languages must be the same");

        try
        {
            var waEngine = new WordAlignmentEngine
            {
                EngineId = engineId,
                SourceLanguage = sourceLanguage,
                TargetLanguage = targetLanguage,
                Type = EngineType.EchoWordAlignment,
            };
            await engines.InsertAsync(waEngine, cancellationToken);
        }
        catch (DuplicateKeyException)
        {
            // this method is idempotent, so ignore if the engine already exists
        }
    }

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

    public async Task DeleteAsync(string engineId, CancellationToken cancellationToken = default)
    {
        await CancelBuildJobAsync(engineId, cancellationToken);
        await engines.DeleteAsync(e => e.EngineId == engineId, cancellationToken);
        await buildJobService.DeleteEngineAsync(engineId, cancellationToken);
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
            EngineType.EchoWordAlignment,
            engineId,
            buildId,
            BuildStage.Preprocess,
            corpora,
            options,
            cancellationToken
        );
        // If there is a pending/running build, then no need to start a new one.
        if (building)
            throw new ConflictException();
    }

    public Task<string?> CancelBuildAsync(string engineId, CancellationToken cancellationToken = default) =>
        CancelBuildJobAsync(engineId, cancellationToken);

    public Task<int> GetQueueSizeAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

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
