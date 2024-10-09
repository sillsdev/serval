namespace Serval.WordAlignment.Services;

public interface IWordAlignmentService
{
    Task<IEnumerable<Models.WordAlignment>> GetAllAsync(
        string engineId,
        int modelRevision,
        string corpusId,
        string? textId = null,
        CancellationToken cancellationToken = default
    );
}
