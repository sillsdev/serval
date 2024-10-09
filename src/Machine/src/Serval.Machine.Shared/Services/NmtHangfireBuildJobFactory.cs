using static Serval.Machine.Shared.Services.HangfireBuildJobRunner;

namespace Serval.Machine.Shared.Services;

public class NmtHangfireBuildJobFactory : IHangfireBuildJobFactory
{
    public TranslationEngineType EngineType => TranslationEngineType.Nmt;

    public Job CreateJob(string engineId, string buildId, BuildStage stage, object? data, string? buildOptions)
    {
        return stage switch
        {
            BuildStage.Preprocess
                => CreateJob<NmtPreprocessBuildJob, IReadOnlyList<ParallelCorpus>>(
                    engineId,
                    buildId,
                    "nmt",
                    data,
                    buildOptions
                ),
            BuildStage.Postprocess
                => CreateJob<PostprocessBuildJob, (int, double)>(engineId, buildId, "nmt", data, buildOptions),
            _ => throw new ArgumentException("Unknown build stage.", nameof(stage)),
        };
    }
}
