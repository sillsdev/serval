namespace Serval.Translation.Services;

public interface IPretranslationService
{
    Task<IEnumerable<Pretranslation>> GetAllAsync(
        string engineId,
        int modelRevision,
        string corpusId,
        CancellationToken cancellationToken = default
    );
    Task<IEnumerable<Pretranslation>> GetAllAsync(
        string engineId,
        int modelRevision,
        string corpusId,
        string textId,
        CancellationToken cancellationToken = default
    );
}
