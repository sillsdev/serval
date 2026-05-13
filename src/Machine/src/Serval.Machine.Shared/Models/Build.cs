namespace Serval.Machine.Shared.Models;

public enum BuildJobState
{
    None,
    Pending,
    Active,
    Canceling,
    Queued,
    Deleting,
}

public enum BuildJobRunnerType
{
    Hangfire,
    ClearML,
}

public enum BuildStage
{
    Preprocess,
    Train,
    Postprocess,
}

public record Build
{
    public required string BuildId { get; init; }
    public required BuildJobState JobState { get; init; }
    public string? JobId { get; init; }
    public required BuildJobRunnerType BuildJobRunner { get; init; }
    public required BuildStage Stage { get; init; }
    public string? Options { get; set; }
    public object? Data { get; init; }
    public required BuildExecutionData ExecutionData { get; init; }
}
