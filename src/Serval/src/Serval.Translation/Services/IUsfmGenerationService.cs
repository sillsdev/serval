namespace Serval.Translation.Services;

public interface IUsfmGenerationService
{
    Task<string> GetUsfmAsync(
        string engineId,
        int modelRevision,
        string corpusId,
        string textId,
        PretranslationUsfmTextOrigin textOrigin,
        PretranslationUsfmTemplate template,
        PretranslationUsfmMarkerBehavior paragraphMarkerBehavior,
        PretranslationUsfmMarkerBehavior embedBehavior,
        PretranslationUsfmMarkerBehavior styleMarkerBehavior,
        PretranslationNormalizationBehavior quoteNormalizationBehavior,
        CancellationToken cancellationToken = default
    );
}
