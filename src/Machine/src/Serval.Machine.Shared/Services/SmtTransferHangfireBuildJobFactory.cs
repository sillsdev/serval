using static Serval.Machine.Shared.Services.HangfireBuildJobRunner;

namespace Serval.Machine.Shared.Services;

public class SmtTransferHangfireBuildJobFactory : IHangfireBuildJobFactory
{
    public EngineType EngineType => EngineType.SmtTransfer;

    public Job CreateJob(string engineId, string buildId, BuildStage stage, string? buildOptions)
    {
        return stage switch
        {
            BuildStage.Preprocess => CreateJob<TranslationEngine, SmtTransferPreprocessBuildJob>(
                engineId,
                buildId,
                BuildJobQueues.SmtTransfer,
                buildOptions
            ),
            BuildStage.Postprocess => CreateJob<TranslationEngine, SmtTransferPostprocessBuildJob>(
                engineId,
                buildId,
                BuildJobQueues.SmtTransfer,
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
