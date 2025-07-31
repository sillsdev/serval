using SIL.Machine.Corpora;
using SIL.Machine.Translation;

namespace Serval.Translation.Services;

public class PretranslationService(
    IRepository<Pretranslation> pretranslations,
    IRepository<Engine> engines,
    IRepository<Build> builds,
    IScriptureDataFileService scriptureDataFileService
) : EntityServiceBase<Pretranslation>(pretranslations), IPretranslationService
{
    private readonly IRepository<Engine> _engines = engines;
    private readonly IRepository<Build> _builds = builds;
    private readonly IScriptureDataFileService _scriptureDataFileService = scriptureDataFileService;
    private const string AIDisclaimerRemark =
        "This draft of {0} was generated using AI on {1}. It should be reviewed and edited carefully.";

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
        PretranslationUsfmMarkerBehavior paragraphMarkerBehavior,
        PretranslationUsfmMarkerBehavior embedBehavior,
        PretranslationUsfmMarkerBehavior styleMarkerBehavior,
        CancellationToken cancellationToken = default
    )
    {
        Engine? engine = await _engines.GetAsync(engineId, cancellationToken);
        Corpus? corpus = engine?.Corpora.SingleOrDefault(c => c.Id == corpusId);
        ParallelCorpus? parallelCorpus = engine?.ParallelCorpora.SingleOrDefault(c => c.Id == corpusId);
        Build? build = (await _builds.GetAllAsync(b => b.EngineRef == engineId, cancellationToken))
            .OrderByDescending(b => b.DateFinished)
            .FirstOrDefault();
        if (build is null || build.DateFinished is null)
            throw new InvalidOperationException($"Could not find any completed builds for engine '{engineId}'.");

        string disclaimerRemark = string.Format(
            CultureInfo.InvariantCulture,
            AIDisclaimerRemark,
            textId,
            build.DateFinished.Value.ToUniversalTime().ToString("u")
        );
        string markerPlacementRemark = GenerateMarkerPlacementRemark(
            paragraphMarkerBehavior,
            embedBehavior,
            styleMarkerBehavior
        );

        CorpusFile sourceFile;
        CorpusFile targetFile;
        if (corpus is not null)
        {
            if (corpus.SourceFiles.Count == 0)
                throw new InvalidOperationException($"The corpus {corpus.Id} has no source files.");
            sourceFile = corpus.SourceFiles[0];
            if (corpus.TargetFiles.Count == 0)
                throw new InvalidOperationException($"The corpus {corpus.Id} has no target files.");
            targetFile = corpus.TargetFiles[0];
        }
        else if (parallelCorpus is not null)
        {
            if (parallelCorpus.SourceCorpora.Count == 0)
            {
                throw new InvalidOperationException($"The parallel corpus {parallelCorpus.Id} has no source corpora.");
            }
            if (parallelCorpus.SourceCorpora[0].Files.Count == 0)
            {
                throw new InvalidOperationException(
                    $"The corpus {parallelCorpus.SourceCorpora[0].Id} referenced in parallel corpus {parallelCorpus.Id} has no files associated with it."
                );
            }
            sourceFile = parallelCorpus.SourceCorpora[0].Files[0];
            if (parallelCorpus.TargetCorpora.Count == 0)
            {
                throw new InvalidOperationException($"The parallel corpus {parallelCorpus.Id} has no target corpora.");
            }
            if (parallelCorpus.TargetCorpora[0].Files.Count == 0)
            {
                throw new InvalidOperationException(
                    $"The corpus {parallelCorpus.TargetCorpora[0].Id} referenced in parallel corpus {parallelCorpus.Id} has no files associated with it."
                );
            }
            targetFile = parallelCorpus.TargetCorpora[0].Files[0];
        }
        else
        {
            throw new EntityNotFoundException($"Could not find the corpus '{corpusId}' in engine '{engineId}'.");
        }
        if (sourceFile.Format is not FileFormat.Paratext || targetFile.Format is not FileFormat.Paratext)
            throw new InvalidOperationException("USFM format is not valid for non-Scripture corpora.");

        ParatextProjectSettings sourceSettings = _scriptureDataFileService.GetParatextProjectSettings(
            sourceFile.Filename
        );
        ParatextProjectSettings targetSettings = _scriptureDataFileService.GetParatextProjectSettings(
            targetFile.Filename
        );

        IEnumerable<Pretranslation> pretranslations = await GetAllAsync(
            engineId,
            modelRevision,
            corpusId,
            textId,
            cancellationToken
        );

        IEnumerable<(IReadOnlyList<ScriptureRef> Refs, string Translation)> pretranslationRows = pretranslations
            .Select(p =>
                (
                    Refs: (IReadOnlyList<ScriptureRef>)
                        p.Refs.Select(r => ScriptureRef.Parse(r, targetSettings.Versification)).ToArray(),
                    p.Translation
                )
            )
            .OrderBy(p => p.Refs[0]);

        IEnumerable<PlaceMarkersAlignmentInfo> alignmentInfo = pretranslations.Select(
            p => new PlaceMarkersAlignmentInfo(
                p.Refs,
                p.SourceTokens?.ToList() ?? [],
                p.TranslationTokens?.ToList() ?? [],
                Map(p.Alignment)
            )
        );

        // Update the target book if it exists
        if (template is PretranslationUsfmTemplate.Auto or PretranslationUsfmTemplate.Target)
        {
            // the pretranslations are generated from the source book and inserted into the target book
            // use relaxed references since the USFM structure may not be the same
            pretranslationRows = pretranslationRows.Select(p =>
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
                            pretranslationRows.ToList(),
                            fullName: targetSettings.FullName,
                            textBehavior: UpdateUsfmTextBehavior.PreferExisting,
                            paragraphBehavior: Map(paragraphMarkerBehavior),
                            embedBehavior: Map(embedBehavior),
                            styleBehavior: Map(styleMarkerBehavior),
                            updateBlockHandlers: paragraphMarkerBehavior
                            == PretranslationUsfmMarkerBehavior.PreservePosition
                                ? [new PlaceMarkersUsfmUpdateBlockHandler(alignmentInfo)]
                                : null,
                            remarks: [disclaimerRemark, markerPlacementRemark]
                        ) ?? "";
                    break;
                case PretranslationUsfmTextOrigin.PreferPretranslated:
                    usfm =
                        updater.UpdateUsfm(
                            textId,
                            pretranslationRows.ToList(),
                            fullName: targetSettings.FullName,
                            textBehavior: UpdateUsfmTextBehavior.PreferNew,
                            paragraphBehavior: Map(paragraphMarkerBehavior),
                            embedBehavior: Map(embedBehavior),
                            styleBehavior: Map(styleMarkerBehavior),
                            updateBlockHandlers: paragraphMarkerBehavior
                            == PretranslationUsfmMarkerBehavior.PreservePosition
                                ? [new PlaceMarkersUsfmUpdateBlockHandler(alignmentInfo)]
                                : null,
                            remarks: [disclaimerRemark, markerPlacementRemark]
                        ) ?? "";
                    break;
                case PretranslationUsfmTextOrigin.OnlyExisting:
                    usfm =
                        updater.UpdateUsfm(
                            textId,
                            [], // don't put any pretranslations, we only want the existing text.
                            fullName: targetSettings.FullName,
                            textBehavior: UpdateUsfmTextBehavior.PreferNew,
                            paragraphBehavior: Map(paragraphMarkerBehavior),
                            embedBehavior: Map(embedBehavior),
                            styleBehavior: Map(styleMarkerBehavior),
                            updateBlockHandlers: paragraphMarkerBehavior
                            == PretranslationUsfmMarkerBehavior.PreservePosition
                                ? [new PlaceMarkersUsfmUpdateBlockHandler(alignmentInfo)]
                                : null,
                            remarks: [disclaimerRemark, markerPlacementRemark]
                        ) ?? "";
                    break;
                case PretranslationUsfmTextOrigin.OnlyPretranslated:
                    usfm =
                        updater.UpdateUsfm(
                            textId,
                            pretranslationRows.ToList(),
                            fullName: targetSettings.FullName,
                            textBehavior: UpdateUsfmTextBehavior.StripExisting,
                            paragraphBehavior: Map(paragraphMarkerBehavior),
                            embedBehavior: Map(embedBehavior),
                            styleBehavior: Map(styleMarkerBehavior),
                            updateBlockHandlers: paragraphMarkerBehavior
                            == PretranslationUsfmMarkerBehavior.PreservePosition
                                ? [new PlaceMarkersUsfmUpdateBlockHandler(alignmentInfo)]
                                : null,
                            remarks: [disclaimerRemark, markerPlacementRemark]
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
                            pretranslationRows.ToList(),
                            fullName: targetSettings.FullName,
                            textBehavior: UpdateUsfmTextBehavior.StripExisting,
                            paragraphBehavior: Map(paragraphMarkerBehavior),
                            embedBehavior: Map(embedBehavior),
                            styleBehavior: Map(styleMarkerBehavior),
                            updateBlockHandlers: paragraphMarkerBehavior
                            == PretranslationUsfmMarkerBehavior.PreservePosition
                                ? [new PlaceMarkersUsfmUpdateBlockHandler(alignmentInfo)]
                                : null,
                            remarks: [disclaimerRemark, markerPlacementRemark]
                        ) ?? "";
                case PretranslationUsfmTextOrigin.OnlyExisting:
                    return updater.UpdateUsfm(
                            textId,
                            [], // don't pass the pretranslations, we only want the existing text.
                            fullName: targetSettings.FullName,
                            textBehavior: UpdateUsfmTextBehavior.StripExisting,
                            paragraphBehavior: Map(paragraphMarkerBehavior),
                            embedBehavior: Map(embedBehavior),
                            styleBehavior: Map(styleMarkerBehavior),
                            updateBlockHandlers: paragraphMarkerBehavior
                            == PretranslationUsfmMarkerBehavior.PreservePosition
                                ? [new PlaceMarkersUsfmUpdateBlockHandler(alignmentInfo)]
                                : null,
                            remarks: [disclaimerRemark, markerPlacementRemark]
                        ) ?? "";
            }
        }

        return "";
    }

    /// <summary>
    /// Generate a natural sounding comment describing marker placement.
    /// </summary>
    /// <param name="paragraphMarkerBehavior">The paragraph marker behavior.</param>
    /// <param name="embedBehavior">The embed marker behavior.</param>
    /// <param name="styleMarkerBehavior">The style marker behavior.</param>
    /// <returns>One to three sentences describing the marker placement behavior.</returns>
    private static string GenerateMarkerPlacementRemark(
        PretranslationUsfmMarkerBehavior paragraphMarkerBehavior,
        PretranslationUsfmMarkerBehavior embedBehavior,
        PretranslationUsfmMarkerBehavior styleMarkerBehavior
    )
    {
        var behaviorMap = new Dictionary<PretranslationUsfmMarkerBehavior, List<string>>
        {
            { PretranslationUsfmMarkerBehavior.Preserve, [] },
            { PretranslationUsfmMarkerBehavior.PreservePosition, [] },
            { PretranslationUsfmMarkerBehavior.Strip, [] },
        };

        behaviorMap[paragraphMarkerBehavior].Add("paragraph");
        behaviorMap[embedBehavior].Add("embed");
        behaviorMap[styleMarkerBehavior].Add("style");

        IEnumerable<string> sentences = behaviorMap
            .Where(kvp => kvp.Value.Count > 0)
            .Select(kvp =>
            {
                string markers =
                    kvp.Value.Count == 1 ? kvp.Value[0] : string.Join(", ", kvp.Value[..^1]) + " and " + kvp.Value[^1];
                markers = char.ToUpperInvariant(markers[0]) + markers[1..];
                string behavior = kvp.Key switch
                {
                    PretranslationUsfmMarkerBehavior.Preserve => "were moved to the end of the verse",
                    PretranslationUsfmMarkerBehavior.PreservePosition => "have positions preserved",
                    PretranslationUsfmMarkerBehavior.Strip => "were removed",
                    _ => "have unknown behavior",
                };
                return $"{markers} markers {behavior}.";
            });

        return string.Join(" ", sentences);
    }

    private static UpdateUsfmMarkerBehavior Map(PretranslationUsfmMarkerBehavior behavior)
    {
        return behavior switch
        {
            PretranslationUsfmMarkerBehavior.Preserve => UpdateUsfmMarkerBehavior.Preserve,
            PretranslationUsfmMarkerBehavior.PreservePosition => UpdateUsfmMarkerBehavior.Preserve,
            PretranslationUsfmMarkerBehavior.Strip => UpdateUsfmMarkerBehavior.Strip,
            _ => throw new InvalidEnumArgumentException(nameof(behavior))
        };
    }

    private static WordAlignmentMatrix Map(IEnumerable<Models.AlignedWordPair>? alignedWordPairs)
    {
        int rowCount = 0;
        int columnCount = 0;
        if (alignedWordPairs is not null)
        {
            foreach (Models.AlignedWordPair pair in alignedWordPairs)
            {
                if (pair.SourceIndex + 1 > rowCount)
                    rowCount = pair.SourceIndex + 1;
                if (pair.TargetIndex + 1 > columnCount)
                    columnCount = pair.TargetIndex + 1;
            }
        }
        return new WordAlignmentMatrix(
            rowCount,
            columnCount,
            alignedWordPairs?.Select(wp => (wp.SourceIndex, wp.TargetIndex))
        );
    }
}
