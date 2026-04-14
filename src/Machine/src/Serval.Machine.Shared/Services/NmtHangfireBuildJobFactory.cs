using Serval.Shared.Contracts;
using static Serval.Machine.Shared.Services.HangfireBuildJobRunner;

namespace Serval.Machine.Shared.Services;

public class NmtHangfireBuildJobFactory : IHangfireBuildJobFactory
{
    public EngineType EngineType => EngineType.Nmt;

    public Job CreateJob(string engineId, string buildId, BuildStage stage, object? data, string? buildOptions)
    {
        return stage switch
        {
            BuildStage.Preprocess => CreateJob<
                TranslationEngine,
                NmtPreprocessBuildJob,
                IReadOnlyList<ParallelCorpusContract>
            >(engineId, buildId, BuildJobQueues.Nmt, data, buildOptions),
            BuildStage.Postprocess => CreateJob<TranslationEngine, TranslationPostprocessBuildJob, (int, double)>(
                engineId,
                buildId,
                BuildJobQueues.Nmt,
                data,
                buildOptions
            ),
            _ => throw new ArgumentException("Unknown build stage.", nameof(stage)),
        };
    }
}
