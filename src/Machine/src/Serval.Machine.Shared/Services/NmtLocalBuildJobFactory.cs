namespace Serval.Machine.Shared.Services;

public class NmtLocalBuildJobFactory : ILocalBuildJobFactory
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public EngineType EngineType => EngineType.Nmt;

    public string? Serialize(BuildStage stage, object? data) =>
        stage switch
        {
            BuildStage.Preprocess => JsonSerializer.Serialize(
                (IReadOnlyList<ParallelCorpusContract>)data!,
                SerializerOptions
            ),
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
                var preprocessJob = ActivatorUtilities.CreateInstance<NmtPreprocessBuildJob>(serviceProvider);
                var corpora = JsonSerializer.Deserialize<List<ParallelCorpusContract>>(jobData!, SerializerOptions)!;
                await preprocessJob.RunAsync(engineId, buildId, corpora, buildOptions, cancellationToken);
                break;
            case BuildStage.Postprocess:
                var postprocessJob = ActivatorUtilities.CreateInstance<TranslationPostprocessBuildJob>(serviceProvider);
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
                throw new ArgumentException($"NMT does not support local stage: {stage}", nameof(stage));
        }
    }
}
