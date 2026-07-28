namespace Serval.Shared.Contracts;

public interface IBuildDiagnosticService
{
    DiagnosticContract CreateDiagnostic(string code, Dictionary<string, object> data);
}
