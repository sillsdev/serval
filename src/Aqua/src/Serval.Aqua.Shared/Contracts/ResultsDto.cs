namespace Serval.Aqua.Shared.Contracts;

public record ResultsDto
{
    public required IReadOnlyList<ResultDto> Results { get; init; }
    public required int TotalCount { get; init; }
}
