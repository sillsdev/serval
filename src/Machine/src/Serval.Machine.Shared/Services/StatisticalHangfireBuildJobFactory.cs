using static Serval.Machine.Shared.Services.HangfireBuildJobRunner;

namespace Serval.Machine.Shared.Services;

public class StatisticalHangfireBuildJobFactory : IHangfireBuildJobFactory
{
    public EngineType EngineType => EngineType.Statistical;

    public Job CreateJob(string engineId, string buildId, BuildStage stage, object? data, string? buildOptions)
    {
        return stage switch
        {
            BuildStage.Preprocess
                => CreateJob<WordAlignmentEngine, WordAlignmentPreprocessBuildJob, IReadOnlyList<ParallelCorpus>>(
                    engineId,
                    buildId,
                    "statistical",
                    data,
                    buildOptions
                ),
            BuildStage.Postprocess
                => CreateJob<WordAlignmentEngine, StatisticalPostprocessBuildJob, (int, double)>(
                    engineId,
                    buildId,
                    "statistical",
                    data,
                    buildOptions
                ),
            BuildStage.Train
                => CreateJob<WordAlignmentEngine, StatisticalTrainBuildJob>(
                    engineId,
                    buildId,
                    "statistical",
                    buildOptions
                ),
            _ => throw new ArgumentException("Unknown build stage.", nameof(stage)),
        };
    }
}
