using SIL.Machine.Corpora;

namespace Serval.Translation.Services;

public class PretranslationService(
    IRepository<Pretranslation> pretranslations,
    IRepository<Engine> engines,
    IScriptureDataFileService scriptureDataFileService
) : EntityServiceBase<Pretranslation>(pretranslations), IPretranslationService
{
    private readonly IRepository<Engine> _engines = engines;
    private readonly IScriptureDataFileService _scriptureDataFileService = scriptureDataFileService;

    public async Task<IEnumerable<Pretranslation>> GetAllAsync(
        string engineId,
        int modelRevision,
        string corpusId,
        string? textId = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Entities.GetAllAsync(
            pt =>
                pt.EngineRef == engineId
                && pt.ModelRevision == modelRevision
                && pt.CorpusRef == corpusId
                && (textId == null || pt.TextId == textId),
            cancellationToken
        );
    }

    public async Task<string> GetUsfmAsync(
        string engineId,
        int modelRevision,
        string corpusId,
        string textId,
        CancellationToken cancellationToken = default
    )
    {
        Engine? engine = await _engines.GetAsync(engineId, cancellationToken);
        Corpus? corpus = engine?.Corpora.SingleOrDefault(c => c.Id == corpusId);
        if (corpus is null)
            throw new EntityNotFoundException($"Could not find the Corpus '{corpusId}' in Engine '{engineId}'.");

        CorpusFile sourceFile = corpus.SourceFiles[0];
        CorpusFile targetFile = corpus.TargetFiles[0];
        if (sourceFile.Format is not FileFormat.Paratext || targetFile.Format is not FileFormat.Paratext)
            throw new InvalidOperationException("USFM format is not valid for non-Scripture corpora.");

        ParatextProjectSettings sourceSettings = _scriptureDataFileService.GetParatextProjectSettings(
            sourceFile.Filename
        );
        ParatextProjectSettings targetSettings = _scriptureDataFileService.GetParatextProjectSettings(
            targetFile.Filename
        );

        IReadOnlyList<(IReadOnlyList<ScriptureRef>, string)> pretranslations = (
            await GetAllAsync(engineId, modelRevision, corpusId, textId, cancellationToken)
        )
            .Select(p =>
                (
                    (IReadOnlyList<ScriptureRef>)
                        p.Refs.Select(r => ScriptureRef.Parse(r, targetSettings.Versification)).ToList(),
                    p.Translation
                )
            )
            .OrderBy(p => p.Item1[0])
            .ToList();

        // Update the target book if it exists
        string? usfm = await _scriptureDataFileService.ReadParatextProjectBookAsync(targetFile.Filename, textId);
        if (usfm is not null)
            return UpdateUsfm(targetSettings, usfm, pretranslations);

        // Copy and update the source book if it exists
        usfm = await _scriptureDataFileService.ReadParatextProjectBookAsync(sourceFile.Filename, textId);
        if (usfm is not null)
            return UpdateUsfm(sourceSettings, usfm, pretranslations, targetSettings.FullName, stripAllText: true);

        return "";
    }

    private static string UpdateUsfm(
        ParatextProjectSettings settings,
        string usfm,
        IReadOnlyList<(IReadOnlyList<ScriptureRef>, string)> pretranslations,
        string? fullName = null,
        bool stripAllText = false
    )
    {
        var updater = new UsfmTextUpdater(
            pretranslations,
            fullName is null ? null : $"- {fullName}",
            stripAllText,
            strictComparison: false
        );
        UsfmParser.Parse(usfm, updater, settings.Stylesheet, settings.Versification);
        return updater.GetUsfm(settings.Stylesheet);
    }
}
