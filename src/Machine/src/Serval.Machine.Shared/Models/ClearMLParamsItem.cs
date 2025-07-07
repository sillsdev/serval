namespace Serval.Machine.Shared.Models;

public record ClearMLParamsItem
{
    public string? Section { get; init; }
    public string? Name { get; init; }
    public string? Value { get; init; }
    public string? Type { get; init; }
    public string? Description { get; init; }
}
