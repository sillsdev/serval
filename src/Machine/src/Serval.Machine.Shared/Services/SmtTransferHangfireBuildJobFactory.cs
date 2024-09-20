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
                => CreateJob<SmtTransferPreprocessBuildJob, IReadOnlyList<TranslationCorpus>>(
                    engineId,
                    buildId,
                    "smt_transfer",
                    data,
                    buildOptions
                ),
            BuildStage.Postprocess
                => CreateJob<SmtTransferPostprocessBuildJob, (int, double)>(
                    engineId,
                    buildId,
                    "smt_transfer",
                    data,
                    buildOptions
                ),
            BuildStage.Process => CreateJob<SmtTransferTrainBuildJob>(engineId, buildId, "smt_transfer", buildOptions),
            _ => throw new ArgumentException("Unknown build stage.", nameof(stage)),
        };
    }
}
