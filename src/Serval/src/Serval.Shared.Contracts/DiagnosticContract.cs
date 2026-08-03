namespace Serval.Shared.Contracts;

public record DiagnosticContract
{
    public required string Code { get; init; }
    public required string Category { get; init; }
    public required string Message { get; init; }
    public required DiagnosticSeverity Severity { get; init; }
    public required Dictionary<string, object> Data { get; init; }
}

public enum DiagnosticSeverity
{
    Info,
    Warn,
    Error,
}
