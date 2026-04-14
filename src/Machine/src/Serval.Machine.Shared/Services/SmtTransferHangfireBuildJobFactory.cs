using Serval.Shared.Contracts;
using static Serval.Machine.Shared.Services.HangfireBuildJobRunner;

namespace Serval.Machine.Shared.Services;

public class SmtTransferHangfireBuildJobFactory : IHangfireBuildJobFactory
{
    public EngineType EngineType => EngineType.SmtTransfer;

    public Job CreateJob(string engineId, string buildId, BuildStage stage, object? data, string? buildOptions)
    {
        return stage switch
        {
            BuildStage.Preprocess => CreateJob<
                TranslationEngine,
                SmtTransferPreprocessBuildJob,
                IReadOnlyList<ParallelCorpusContract>
            >(engineId, buildId, BuildJobQueues.SmtTransfer, data, buildOptions),
            BuildStage.Postprocess => CreateJob<TranslationEngine, SmtTransferPostprocessBuildJob, (int, double)>(
                engineId,
                buildId,
                BuildJobQueues.SmtTransfer,
                data,
                buildOptions
            ),
            BuildStage.Train => CreateJob<TranslationEngine, SmtTransferTrainBuildJob>(
                engineId,
                buildId,
                BuildJobQueues.SmtTransfer,
                buildOptions
            ),
            _ => throw new ArgumentException("Unknown build stage.", nameof(stage)),
        };
    }
}
