namespace Serval.WordAlignment.Dtos;

public record WordAlignmentDiagnosticDto
{
    public required string Code { get; init; }
    public required string Category { get; init; }
    public required string Message { get; init; }
    public required WordAlignmentDiagnosticSeverity Severity { get; init; }
    public required Dictionary<string, object> Data { get; init; }
}

public enum WordAlignmentDiagnosticSeverity
{
    Info,
    Warn,
    Error,
}
