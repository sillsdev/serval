namespace Serval.Machine.Translation.Services;

public class LlmRemoteLocalBuildJobFactory : ILocalBuildJobFactory
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public EngineType EngineType => EngineType.LlmRemote;

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
        string? model,
        CancellationToken cancellationToken
    )
    {
        switch (stage)
        {
            case BuildStage.Preprocess:
                var preprocessJob = ActivatorUtilities.CreateInstance<LlmRemotePreprocessBuildJob>(serviceProvider);
                var corpora = JsonSerializer.Deserialize<List<ParallelCorpusContract>>(jobData!, SerializerOptions)!;
                await preprocessJob.RunAsync(engineId, buildId, corpora, buildOptions, model, cancellationToken);
                break;
            case BuildStage.Train:
                var trainJob = ActivatorUtilities.CreateInstance<LlmRemoteTrainingBuildJob>(serviceProvider);
                await trainJob.RunAsync(engineId, buildId, null, buildOptions, model, cancellationToken);
                break;
            case BuildStage.Postprocess:
                var postprocessJob = ActivatorUtilities.CreateInstance<LlmRemotePostprocessBuildJob>(serviceProvider);
                var postData = JsonSerializer.Deserialize<PostprocessData>(jobData!, SerializerOptions)!;
                await postprocessJob.RunAsync(
                    engineId,
                    buildId,
                    (postData.TrainCount, postData.Confidence),
                    buildOptions,
                    model,
                    cancellationToken
                );
                break;
            default:
                throw new ArgumentException($"LlmRemote does not support local stage: {stage}", nameof(stage));
        }
    }
}
