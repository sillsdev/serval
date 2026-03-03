namespace SIL.ServiceToolkit.Models;

public record MissingParentProjectError
{
    public required string ProjectName { get; init; }
    public required string ParentProjectName { get; init; }
    public required string ParallelCorpusId { get; init; }
    public required string MonolingualCorpusId { get; init; }
}
