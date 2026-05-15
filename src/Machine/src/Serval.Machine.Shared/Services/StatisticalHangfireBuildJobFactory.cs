using static Serval.Machine.Shared.Services.HangfireBuildJobRunner;

namespace Serval.Machine.Shared.Services;

public class StatisticalHangfireBuildJobFactory : IHangfireBuildJobFactory
{
    public EngineType EngineType => EngineType.Statistical;

    public Job CreateJob(string engineId, string buildId, BuildStage stage, string? buildOptions)
    {
        return stage switch
        {
            BuildStage.Preprocess => CreateJob<WordAlignmentEngine, WordAlignmentPreprocessBuildJob>(
                engineId,
                buildId,
                BuildJobQueues.Statistical,
                buildOptions
            ),
            BuildStage.Postprocess => CreateJob<WordAlignmentEngine, StatisticalPostprocessBuildJob>(
                engineId,
                buildId,
                BuildJobQueues.Statistical,
                buildOptions
            ),
            BuildStage.Train => CreateJob<WordAlignmentEngine, StatisticalTrainBuildJob>(
                engineId,
                buildId,
                BuildJobQueues.Statistical,
                buildOptions
            ),
            _ => throw new ArgumentException("Unknown build stage.", nameof(stage)),
        };
    }
}
