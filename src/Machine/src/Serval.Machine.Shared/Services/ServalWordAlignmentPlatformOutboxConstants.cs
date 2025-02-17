namespace Serval.Machine.Shared.Services;

public static class ServalWordAlignmentPlatformOutboxConstants
{
    public const string OutboxId = "ServalWordAlignmentPlatform";

    public const string BuildStarted = "BuildStarted";
    public const string BuildCompleted = "BuildCompleted";
    public const string BuildCanceled = "BuildCanceled";
    public const string BuildFaulted = "BuildFaulted";
    public const string BuildRestarting = "BuildRestarting";
    public const string IncrementTrainEngineCorpusSize = "IncrementTrainEngineCorpusSize";
    public const string InsertInferenceResults = "InsertInferenceResults";
    public const string UpdateBuildExecutionData = "UpdateBuildExecutionData";
}
