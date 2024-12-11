using static Serval.Machine.Shared.Services.HangfireBuildJobRunner;

namespace Serval.Machine.Shared.Services;

public class SmtTransferHangfireBuildJobFactory : IHangfireBuildJobFactory
{
    public EngineType EngineType => EngineType.SmtTransfer;

    public Job CreateJob(string engineId, string buildId, BuildStage stage, object? data, string? buildOptions)
    {
        return stage switch
        {
            BuildStage.Preprocess
                => CreateJob<TranslationEngine, SmtTransferPreprocessBuildJob, IReadOnlyList<ParallelCorpus>>(
                    engineId,
                    buildId,
                    "smt_transfer",
                    data,
                    buildOptions
                ),
            BuildStage.Postprocess
                => CreateJob<TranslationEngine, SmtTransferPostprocessBuildJob, (int, double)>(
                    engineId,
                    buildId,
                    "smt_transfer",
                    data,
                    buildOptions
                ),
            BuildStage.Train
                => CreateJob<TranslationEngine, SmtTransferTrainBuildJob>(
                    engineId,
                    buildId,
                    "smt_transfer",
                    buildOptions
                ),
            _ => throw new ArgumentException("Unknown build stage.", nameof(stage)),
        };
    }
}
