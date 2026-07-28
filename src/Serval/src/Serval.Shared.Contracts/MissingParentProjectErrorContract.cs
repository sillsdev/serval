namespace Serval.Shared.Contracts;

public record MissingParentProjectErrorContract
{
    public required string ProjectName { get; init; }
    public required string ProjectGuid { get; init; }
    public required string ParentProjectName { get; init; }
    public required string ParentProjectGuid { get; init; }
}
