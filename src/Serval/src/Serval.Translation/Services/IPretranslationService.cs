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

    Task<string> GetUsfmAsync(
        string engineId,
        int modelRevision,
        string corpusId,
        string textId,
        PretranslationUsfmTextOrigin textOrigin,
        bool useSourceUsfm,
        CancellationToken cancellationToken = default
    );
}
