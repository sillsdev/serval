namespace Serval.Machine.Shared.Services;

public class StatisticalLocalBuildJobFactory : ILocalBuildJobFactory
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public EngineType EngineType => EngineType.Statistical;

    public string? Serialize(BuildStage stage, object? data) =>
        stage switch
        {
            BuildStage.Preprocess => JsonSerializer.Serialize(
                (IReadOnlyList<ParallelCorpusContract>)data!,
                SerializerOptions
            ),
            BuildStage.Train => null,
            BuildStage.Postprocess => data is (int tc, double conf)
                ? JsonSerializer.Serialize(new PostprocessData(tc, conf), SerializerOptions)
                : null,
            _ => null,
        };

    public async Task RunAsync(
        IServiceProvider serviceProvider,
        string engineId,
        string buildId,
        BuildStage stage,
        string? jobData,
        string? buildOptions,
        CancellationToken cancellationToken
    )
    {
        switch (stage)
        {
            case BuildStage.Preprocess:
                var preprocessJob = ActivatorUtilities.CreateInstance<WordAlignmentPreprocessBuildJob>(serviceProvider);
                var corpora = JsonSerializer.Deserialize<List<ParallelCorpusContract>>(jobData!, SerializerOptions)!;
                await preprocessJob.RunAsync(engineId, buildId, corpora, buildOptions, cancellationToken);
                break;
            case BuildStage.Postprocess:
                var postprocessJob = ActivatorUtilities.CreateInstance<StatisticalPostprocessBuildJob>(serviceProvider);
                var postData = JsonSerializer.Deserialize<PostprocessData>(jobData!, SerializerOptions)!;
                await postprocessJob.RunAsync(
                    engineId,
                    buildId,
                    (postData.TrainCount, postData.Confidence),
                    buildOptions,
                    cancellationToken
                );
                break;
            default:
                throw new ArgumentException($"Unsupported stage: {stage}", nameof(stage));
        }
    }
}
