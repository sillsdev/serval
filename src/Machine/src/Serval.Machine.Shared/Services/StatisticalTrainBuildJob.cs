namespace Serval.Machine.Shared.Services;

public class StatisticalTrainBuildJob(
    IEnumerable<IPlatformService> platformServices,
    IRepository<WordAlignmentEngine> engines,
    IDataAccessContext dataAccessContext,
    IBuildJobService<WordAlignmentEngine> buildJobService,
    ILogger<StatisticalTrainBuildJob> logger,
    ISharedFileService sharedFileService,
    IWordAlignmentModelFactory wordAlignmentModelFactory
)
    : HangfireBuildJob<WordAlignmentEngine>(
        platformServices.First(ps => ps.EngineGroup == EngineGroup.WordAlignment),
        engines,
        dataAccessContext,
        buildJobService,
        logger
    )
{
    private static readonly JsonWriterOptions WordAlignmentWriterOptions = new() { Indented = true };
    private static readonly JsonSerializerOptions JsonSerializerOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private const int BatchSize = 128;

    private readonly ISharedFileService _sharedFileService = sharedFileService;
    private readonly IWordAlignmentModelFactory _wordAlignmentFactory = wordAlignmentModelFactory;

    protected override async Task DoWorkAsync(
        string engineId,
        string buildId,
        object? data,
        string? buildOptions,
        CancellationToken cancellationToken
    )
    {
        using TempDirectory tempDir = new(buildId);
        string corpusDir = Path.Combine(tempDir.Path, "corpus");
        await DownloadDataAsync(buildId, corpusDir, cancellationToken);

        // assemble corpus
        ITextCorpus sourceCorpus = new TextFileTextCorpus(Path.Combine(corpusDir, "train.src.txt"));
        ITextCorpus targetCorpus = new TextFileTextCorpus(Path.Combine(corpusDir, "train.trg.txt"));
        IParallelTextCorpus parallelCorpus = sourceCorpus.AlignRows(targetCorpus);

        // train word alignment model
        string engineDir = Path.Combine(tempDir.Path, "engine");
        int trainCount = await TrainAsync(buildId, engineDir, parallelCorpus, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        await GenerateWordAlignmentsAsync(buildId, engineDir, cancellationToken);

        bool canceling = !await BuildJobService.StartBuildJobAsync(
            BuildJobRunnerType.Hangfire,
            EngineType.Statistical,
            engineId,
            buildId,
            BuildStage.Postprocess,
            buildOptions: buildOptions,
            data: (trainCount, 0.0),
            cancellationToken: cancellationToken
        );
        if (canceling)
            throw new OperationCanceledException();
    }

    protected override async Task CleanupAsync(
        string engineId,
        string buildId,
        object? data,
        JobCompletionStatus completionStatus
    )
    {
        if (completionStatus is JobCompletionStatus.Canceled)
        {
            try
            {
                await _sharedFileService.DeleteAsync($"builds/{buildId}/");
            }
            catch (Exception e)
            {
                Logger.LogWarning(e, "Unable to to delete job data for build {BuildId}.", buildId);
            }
        }
    }

    private async Task DownloadDataAsync(string buildId, string corpusDir, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(corpusDir);
        await using Stream srcText = await _sharedFileService.OpenReadAsync(
            $"builds/{buildId}/train.src.txt",
            cancellationToken
        );
        await using FileStream srcFileStream = File.Create(Path.Combine(corpusDir, "train.src.txt"));
        await srcText.CopyToAsync(srcFileStream, cancellationToken);

        await using Stream tgtText = await _sharedFileService.OpenReadAsync(
            $"builds/{buildId}/train.trg.txt",
            cancellationToken
        );
        await using FileStream tgtFileStream = File.Create(Path.Combine(corpusDir, "train.trg.txt"));
        await tgtText.CopyToAsync(tgtFileStream, cancellationToken);
    }

    private async Task<int> TrainAsync(
        string buildId,
        string engineDir,
        IParallelTextCorpus parallelCorpus,
        CancellationToken cancellationToken
    )
    {
        _wordAlignmentFactory.InitNew(engineDir);
        LatinWordTokenizer tokenizer = new();
        using ITrainer wordAlignmentTrainer = _wordAlignmentFactory.CreateTrainer(engineDir, tokenizer, parallelCorpus);
        cancellationToken.ThrowIfCancellationRequested();

        var progress = new BuildProgress(PlatformService, buildId);
        await wordAlignmentTrainer.TrainAsync(progress, cancellationToken);

        int trainCorpusSize = wordAlignmentTrainer.Stats.TrainCorpusSize;

        cancellationToken.ThrowIfCancellationRequested();

        await wordAlignmentTrainer.SaveAsync(cancellationToken);

        await using Stream engineStream = await _sharedFileService.OpenWriteAsync(
            $"builds/{buildId}/model.tar.gz",
            cancellationToken
        );
        await _wordAlignmentFactory.SaveEngineToAsync(engineDir, engineStream, cancellationToken);
        return trainCorpusSize;
    }

    private async Task GenerateWordAlignmentsAsync(
        string buildId,
        string engineDir,
        CancellationToken cancellationToken
    )
    {
        await using Stream sourceStream = await _sharedFileService.OpenReadAsync(
            $"builds/{buildId}/word_alignments.inputs.json",
            cancellationToken
        );

        IAsyncEnumerable<Models.WordAlignment> wordAlignments = JsonSerializer
            .DeserializeAsyncEnumerable<Models.WordAlignment>(sourceStream, JsonSerializerOptions, cancellationToken)
            .OfType<Models.WordAlignment>();

        await using Stream targetStream = await _sharedFileService.OpenWriteAsync(
            $"builds/{buildId}/word_alignments.outputs.json",
            cancellationToken
        );
        await using Utf8JsonWriter targetWriter = new(targetStream, WordAlignmentWriterOptions);

        LatinWordTokenizer tokenizer = new();
        LatinWordDetokenizer detokenizer = new();
        using IWordAlignmentModel wordAlignmentModel = _wordAlignmentFactory.Create(engineDir);
        await foreach (IReadOnlyList<Models.WordAlignment> batch in BatchAsync(wordAlignments))
        {
            (IReadOnlyList<string> Source, IReadOnlyList<string> Target)[] segments = batch
                .Select(p => (p.SourceTokens, p.TargetTokens))
                .ToArray();
            IReadOnlyList<WordAlignmentMatrix> results = wordAlignmentModel.AlignBatch(segments);
            foreach ((Models.WordAlignment wordAlignment, WordAlignmentMatrix result) in batch.Zip(results))
            {
                List<AlignedWordPair> alignedWordPairs = result.ToAlignedWordPairs().ToList();
                wordAlignmentModel.ComputeAlignedWordPairScores(
                    wordAlignment.SourceTokens,
                    wordAlignment.TargetTokens,
                    alignedWordPairs
                );
                JsonSerializer.Serialize(
                    targetWriter,
                    wordAlignment with
                    {
                        Alignment = alignedWordPairs,
                        Confidences = alignedWordPairs.Select(wp => wp.AlignmentScore).ToArray()
                    },
                    JsonSerializerOptions
                );
            }
        }
    }

    public static async IAsyncEnumerable<IReadOnlyList<Models.WordAlignment>> BatchAsync(
        IAsyncEnumerable<Models.WordAlignment> wordAlignments
    )
    {
        List<Models.WordAlignment> batch = [];
        await foreach (Models.WordAlignment item in wordAlignments)
        {
            batch.Add(item);
            if (batch.Count == BatchSize)
            {
                yield return batch;
                batch = [];
            }
        }
        if (batch.Count > 0)
            yield return batch;
    }
}
