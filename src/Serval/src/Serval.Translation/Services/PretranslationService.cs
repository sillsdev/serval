﻿using SIL.Machine.Corpora;

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
        string? usfm = await _scriptureDataFileService.ReadParatextProjectBookAsync(targetFile.Filename, textId);
        if (usfm is not null)
        {
            // the pretranslations are generated from the source book and inserted into the target book
            // use relaxed references since the USFM structure may not be the same
            pretranslations = pretranslations.Select(p =>
                ((IReadOnlyList<ScriptureRef>)p.Refs.Select(r => r.ToRelaxed()).ToArray(), p.Translation)
            );
            switch (textOrigin)
            {
                case PretranslationUsfmTextOrigin.PreferExisting:
                    return UpdateUsfm(
                        targetSettings,
                        usfm,
                        pretranslations,
                        fullName: targetSettings.FullName,
                        stripAllText: false,
                        preferExistingText: true
                    );
                case PretranslationUsfmTextOrigin.PreferPretranslated:
                    return UpdateUsfm(
                        targetSettings,
                        usfm,
                        pretranslations,
                        fullName: targetSettings.FullName,
                        stripAllText: false,
                        preferExistingText: false
                    );
                case PretranslationUsfmTextOrigin.OnlyExisting:
                    return UpdateUsfm(
                        targetSettings,
                        usfm,
                        pretranslations: [], // don't put any pretranslations, we only want the existing text.
                        fullName: targetSettings.FullName,
                        stripAllText: false,
                        preferExistingText: false
                    );
                case PretranslationUsfmTextOrigin.OnlyPretranslated:
                    return UpdateUsfm(
                        targetSettings,
                        usfm,
                        pretranslations,
                        fullName: targetSettings.FullName,
                        stripAllText: true,
                        preferExistingText: false
                    );
            }
        }

        // Copy and update the source book if it exists
        usfm = await _scriptureDataFileService.ReadParatextProjectBookAsync(sourceFile.Filename, textId);
        if (usfm is not null)
        {
            switch (textOrigin)
            {
                case PretranslationUsfmTextOrigin.PreferExisting:
                case PretranslationUsfmTextOrigin.PreferPretranslated:
                case PretranslationUsfmTextOrigin.OnlyPretranslated:
                    return UpdateUsfm(
                        sourceSettings,
                        usfm,
                        pretranslations,
                        fullName: targetSettings.FullName,
                        stripAllText: true,
                        preferExistingText: true
                    );
                case PretranslationUsfmTextOrigin.OnlyExisting:
                    return UpdateUsfm(
                        sourceSettings,
                        usfm,
                        pretranslations: [], // don't pass the pretranslations, we only want the existing text.
                        fullName: targetSettings.FullName,
                        stripAllText: true,
                        preferExistingText: true
                    );
            }
        }

        return "";
    }

    private static string UpdateUsfm(
        ParatextProjectSettings settings,
        string usfm,
        IEnumerable<(IReadOnlyList<ScriptureRef>, string)> pretranslations,
        string? fullName = null,
        bool stripAllText = false,
        bool preferExistingText = true
    )
    {
        var updater = new UsfmTextUpdater(
            pretranslations.ToArray(),
            fullName is null ? null : $"- {fullName}",
            stripAllText,
            preferExistingText: preferExistingText
        );
        UsfmParser.Parse(usfm, updater, settings.Stylesheet, settings.Versification);
        return updater.GetUsfm(settings.Stylesheet);
    }
}