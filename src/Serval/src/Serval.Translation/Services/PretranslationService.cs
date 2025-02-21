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
        PretranslationUsfmTextOrigin textOrigin,
        PretranslationUsfmTemplate template,
        CancellationToken cancellationToken = default
    )
    {
        Engine? engine = await _engines.GetAsync(engineId, cancellationToken);
        Corpus? corpus = engine?.Corpora.SingleOrDefault(c => c.Id == corpusId);
        ParallelCorpus? parallelCorpus = engine?.ParallelCorpora.SingleOrDefault(c => c.Id == corpusId);

        CorpusFile sourceFile;
        CorpusFile targetFile;
        if (corpus is not null)
        {
            sourceFile = corpus.SourceFiles[0];
            targetFile = corpus.TargetFiles[0];
        }
        else if (parallelCorpus is not null)
        {
            sourceFile = parallelCorpus.SourceCorpora[0].Files[0];
            targetFile = parallelCorpus.TargetCorpora[0].Files[0];
        }
        else
        {
            throw new EntityNotFoundException($"Could not find the Corpus '{corpusId}' in Engine '{engineId}'.");
        }
        if (sourceFile.Format is not FileFormat.Paratext || targetFile.Format is not FileFormat.Paratext)
            throw new InvalidOperationException("USFM format is not valid for non-Scripture corpora.");

        ParatextProjectSettings sourceSettings = _scriptureDataFileService.GetParatextProjectSettings(
            sourceFile.Filename
        );
        ParatextProjectSettings targetSettings = _scriptureDataFileService.GetParatextProjectSettings(
            targetFile.Filename
        );

        IEnumerable<(IReadOnlyList<ScriptureRef> Refs, string Translation)> pretranslations = (
            await GetAllAsync(engineId, modelRevision, corpusId, textId, cancellationToken)
        )
            .Select(p =>
                (
                    Refs: (IReadOnlyList<ScriptureRef>)
                        p.Refs.Select(r => ScriptureRef.Parse(r, targetSettings.Versification)).ToArray(),
                    p.Translation
                )
            )
            .OrderBy(p => p.Refs[0]);

        // Update the target book if it exists
        if (template is PretranslationUsfmTemplate.Auto or PretranslationUsfmTemplate.Target)
        {
            // the pretranslations are generated from the source book and inserted into the target book
            // use relaxed references since the USFM structure may not be the same
            pretranslations = pretranslations.Select(p =>
                ((IReadOnlyList<ScriptureRef>)p.Refs.Select(r => r.ToRelaxed()).ToArray(), p.Translation)
            );
            using Shared.Services.ZipParatextProjectTextUpdater updater =
                _scriptureDataFileService.GetZipParatextProjectTextUpdater(targetFile.Filename);
            string usfm = "";
            switch (textOrigin)
            {
                case PretranslationUsfmTextOrigin.PreferExisting:
                    usfm =
                        updater.UpdateUsfm(
                            textId,
                            pretranslations.ToList(),
                            fullName: targetSettings.FullName,
                            textBehavior: UpdateUsfmTextBehavior.PreferExisting
                        ) ?? "";
                    break;
                case PretranslationUsfmTextOrigin.PreferPretranslated:
                    usfm =
                        updater.UpdateUsfm(
                            textId,
                            pretranslations.ToList(),
                            fullName: targetSettings.FullName,
                            textBehavior: UpdateUsfmTextBehavior.PreferNew
                        ) ?? "";
                    break;
                case PretranslationUsfmTextOrigin.OnlyExisting:
                    usfm =
                        updater.UpdateUsfm(
                            textId,
                            [], // don't put any pretranslations, we only want the existing text.
                            fullName: targetSettings.FullName,
                            textBehavior: UpdateUsfmTextBehavior.PreferNew
                        ) ?? "";
                    break;
                case PretranslationUsfmTextOrigin.OnlyPretranslated:
                    usfm =
                        updater.UpdateUsfm(
                            textId,
                            pretranslations.ToList(),
                            fullName: targetSettings.FullName,
                            textBehavior: UpdateUsfmTextBehavior.StripExisting
                        ) ?? "";
                    break;
            }
            // In order to support PretranslationUsfmTemplate.Auto
            if (!string.IsNullOrEmpty(usfm))
                return usfm;
        }

        if (template is PretranslationUsfmTemplate.Auto or PretranslationUsfmTemplate.Source)
        {
            using Shared.Services.ZipParatextProjectTextUpdater updater =
                _scriptureDataFileService.GetZipParatextProjectTextUpdater(sourceFile.Filename);

            // Copy and update the source book if it exists
            switch (textOrigin)
            {
                case PretranslationUsfmTextOrigin.PreferExisting:
                case PretranslationUsfmTextOrigin.PreferPretranslated:
                case PretranslationUsfmTextOrigin.OnlyPretranslated:
                    return updater.UpdateUsfm(
                            textId,
                            pretranslations.ToList(),
                            fullName: targetSettings.FullName,
                            textBehavior: UpdateUsfmTextBehavior.StripExisting
                        ) ?? "";
                case PretranslationUsfmTextOrigin.OnlyExisting:
                    return updater.UpdateUsfm(
                            textId,
                            [], // don't pass the pretranslations, we only want the existing text.
                            fullName: targetSettings.FullName,
                            textBehavior: UpdateUsfmTextBehavior.StripExisting
                        ) ?? "";
            }
        }

        return "";
    }
}
