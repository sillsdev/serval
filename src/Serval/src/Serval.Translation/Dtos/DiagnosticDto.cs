namespace Serval.Translation.Dtos;

public record DiagnosticDto
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
