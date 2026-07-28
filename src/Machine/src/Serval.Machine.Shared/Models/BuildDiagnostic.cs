namespace Serval.Machine.Shared.Models;

public class BuildDiagnostic
{
    public required string Code { get; init; }
    public required string Category { get; init; }
    public required string Message { get; init; }
    public required BuildDiagnosticSeverity Severity { get; init; }
    public required Dictionary<string, object> Data { get; init; }
}

public enum BuildDiagnosticSeverity
{
    Info,
    Warn,
    Error,
}
