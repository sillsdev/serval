namespace Serval.Translation.Services;

public interface IPretranslationService
{
    Task<IEnumerable<Pretranslation>> GetAllAsync(
        string engineId,
        int modelRevision,
        string corpusId,
        string? textId = null,
        CancellationToken cancellationToken = default
    );
}
