using static Serval.Machine.Shared.Services.HangfireBuildJobRunner;

namespace Serval.Machine.Shared.Services;

public class NmtHangfireBuildJobFactory : IHangfireBuildJobFactory
{
    public EngineType EngineType => EngineType.Nmt;

    public Job CreateJob(string engineId, string buildId, BuildStage stage, string? buildOptions)
    {
        return stage switch
        {
            BuildStage.Preprocess => CreateJob<TranslationEngine, NmtPreprocessBuildJob>(
                engineId,
                buildId,
                BuildJobQueues.Nmt,
                buildOptions
            ),
            BuildStage.Postprocess => CreateJob<TranslationEngine, TranslationPostprocessBuildJob>(
                engineId,
                buildId,
                BuildJobQueues.Nmt,
                buildOptions
            ),
            _ => throw new ArgumentException("Unknown build stage.", nameof(stage)),
        };
    }
}
