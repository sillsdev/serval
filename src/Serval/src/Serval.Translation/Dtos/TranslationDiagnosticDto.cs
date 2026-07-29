namespace Serval.Translation.Dtos;

public record TranslationDiagnosticDto
{
    public required string Code { get; init; }
    public required string Category { get; init; }
    public required string Message { get; init; }
    public required TranslationDiagnosticSeverity Severity { get; init; }
    public required Dictionary<string, object> Data { get; init; }
}

public enum TranslationDiagnosticSeverity
{
    Info,
    Warn,
    Error,
}
