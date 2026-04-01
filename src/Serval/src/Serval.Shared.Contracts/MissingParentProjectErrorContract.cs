namespace Serval.Shared.Contracts;

public record MissingParentProjectErrorContract
{
    public required string ProjectName { get; init; }
    public required string ParentProjectName { get; init; }
}
