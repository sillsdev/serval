using SIL.Machine.Corpora;
using SIL.Machine.PunctuationAnalysis;
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
        PretranslationNormalizationBehavior quoteNormalizationBehavior,
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

        List<string> remarks = [disclaimerRemark, markerPlacementRemark];

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

        IEnumerable<(
            IReadOnlyList<ScriptureRef> ScriptureRefs,
            Pretranslation Pretranslation,
            PretranslationUsfmMarkerBehavior ParagraphBehavior,
            PretranslationUsfmMarkerBehavior StyleBehavior
        )> pretranslationRows = pretranslations
            .Select(p =>
                (
                    ScriptureRefs: (IReadOnlyList<ScriptureRef>)
                        p.Refs.Select(r =>
                        {
                            bool parsed = ScriptureRef.TryParse(r, targetSettings.Versification, out ScriptureRef sr);
                            return new { Parsed = parsed, ScriptureRef = sr };
                        })
                            .Where(r => r.Parsed)
                            .Select(r => r.ScriptureRef)
                            .ToArray(),
                    p,
                    paragraphMarkerBehavior,
                    styleMarkerBehavior
                )
            )
            .Where(p => p.ScriptureRefs.Any())
            .OrderBy(p => p.ScriptureRefs[0]);

        List<IUsfmUpdateBlockHandler> updateBlockHandlers = [];
        if (
            paragraphMarkerBehavior == PretranslationUsfmMarkerBehavior.PreservePosition
            && template == PretranslationUsfmTemplate.Source
        )
        {
            updateBlockHandlers.Add(new PlaceMarkersUsfmUpdateBlockHandler());
        }

        string usfm = "";
        // Update the target book if it exists
        if (template is PretranslationUsfmTemplate.Auto or PretranslationUsfmTemplate.Target)
        {
            // the pretranslations are generated from the source book and inserted into the target book
            // use relaxed references since the USFM structure may not be the same
            pretranslationRows = pretranslationRows.Select(p =>
                (
                    (IReadOnlyList<ScriptureRef>)p.ScriptureRefs.Select(r => r.ToRelaxed()).ToArray(),
                    p.Pretranslation,
                    p.ParagraphBehavior,
                    p.StyleBehavior
                )
            );
            using Shared.Services.ZipParatextProjectTextUpdater updater =
                _scriptureDataFileService.GetZipParatextProjectTextUpdater(targetFile.Filename);
            switch (textOrigin)
            {
                case PretranslationUsfmTextOrigin.PreferExisting:
                    usfm =
                        updater.UpdateUsfm(
                            textId,
                            pretranslationRows.Select(Map).ToList(),
                            fullName: targetSettings.FullName,
                            textBehavior: UpdateUsfmTextBehavior.PreferExisting,
                            paragraphBehavior: Map(paragraphMarkerBehavior),
                            embedBehavior: Map(embedBehavior),
                            styleBehavior: Map(styleMarkerBehavior),
                            updateBlockHandlers: updateBlockHandlers,
                            remarks: remarks,
                            errorHandler: (_) => true,
                            compareSegments: false
                        ) ?? "";
                    break;
                case PretranslationUsfmTextOrigin.PreferPretranslated:
                    usfm =
                        updater.UpdateUsfm(
                            textId,
                            pretranslationRows.Select(Map).ToList(),
                            fullName: targetSettings.FullName,
                            textBehavior: UpdateUsfmTextBehavior.PreferNew,
                            paragraphBehavior: Map(paragraphMarkerBehavior),
                            embedBehavior: Map(embedBehavior),
                            styleBehavior: Map(styleMarkerBehavior),
                            updateBlockHandlers: updateBlockHandlers,
                            remarks: remarks,
                            errorHandler: (_) => true,
                            compareSegments: false
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
                            updateBlockHandlers: updateBlockHandlers,
                            remarks: remarks,
                            errorHandler: (_) => true,
                            compareSegments: false
                        ) ?? "";
                    break;
                case PretranslationUsfmTextOrigin.OnlyPretranslated:
                    usfm =
                        updater.UpdateUsfm(
                            textId,
                            pretranslationRows.Select(Map).ToList(),
                            fullName: targetSettings.FullName,
                            textBehavior: UpdateUsfmTextBehavior.StripExisting,
                            paragraphBehavior: Map(paragraphMarkerBehavior),
                            embedBehavior: Map(embedBehavior),
                            styleBehavior: Map(styleMarkerBehavior),
                            updateBlockHandlers: updateBlockHandlers,
                            remarks: remarks,
                            errorHandler: (_) => true,
                            compareSegments: false
                        ) ?? "";
                    break;
            }
        }

        if (
            string.IsNullOrEmpty(usfm)
            && (template is PretranslationUsfmTemplate.Auto or PretranslationUsfmTemplate.Source)
        )
        {
            using Shared.Services.ZipParatextProjectTextUpdater updater =
                _scriptureDataFileService.GetZipParatextProjectTextUpdater(sourceFile.Filename);

            // Copy and update the source book if it exists
            switch (textOrigin)
            {
                case PretranslationUsfmTextOrigin.PreferExisting:
                case PretranslationUsfmTextOrigin.PreferPretranslated:
                case PretranslationUsfmTextOrigin.OnlyPretranslated:
                    usfm =
                        updater.UpdateUsfm(
                            textId,
                            pretranslationRows.Select(Map).ToList(),
                            fullName: targetSettings.FullName,
                            textBehavior: UpdateUsfmTextBehavior.StripExisting,
                            paragraphBehavior: Map(paragraphMarkerBehavior),
                            embedBehavior: Map(embedBehavior),
                            styleBehavior: Map(styleMarkerBehavior),
                            updateBlockHandlers: updateBlockHandlers,
                            remarks: remarks,
                            errorHandler: (_) => true,
                            compareSegments: true
                        ) ?? "";
                    break;
                case PretranslationUsfmTextOrigin.OnlyExisting:
                    usfm =
                        updater.UpdateUsfm(
                            textId,
                            [], // don't pass the pretranslations, we only want the existing text.
                            fullName: targetSettings.FullName,
                            textBehavior: UpdateUsfmTextBehavior.StripExisting,
                            paragraphBehavior: Map(paragraphMarkerBehavior),
                            embedBehavior: Map(embedBehavior),
                            styleBehavior: Map(styleMarkerBehavior),
                            updateBlockHandlers: updateBlockHandlers,
                            remarks: remarks,
                            errorHandler: (_) => true,
                            compareSegments: true
                        ) ?? "";
                    break;
            }
        }
        if (
            quoteNormalizationBehavior == PretranslationNormalizationBehavior.Denormalized
            && build.Analysis is not null
            && build.Analysis.Any(a => a.ParallelCorpusRef == corpusId && a.TargetQuoteConvention != "")
        )
        {
            ParallelCorpusAnalysis analysis = build.Analysis.Single(c => c.ParallelCorpusRef == corpusId);
            usfm = DenormalizeQuotationMarks(usfm, analysis);
        }

        return usfm;
    }

    private static string DenormalizeQuotationMarks(string usfm, ParallelCorpusAnalysis analysis)
    {
        QuoteConvention targetQuoteConvention = QuoteConventions.Standard.GetQuoteConventionByName(
            analysis.TargetQuoteConvention
        );
        if (targetQuoteConvention is null)
            return usfm;

        QuotationMarkDenormalizationFirstPass quotationMarkDenormalizationFirstPass = new(targetQuoteConvention);

        UsfmParser.Parse(usfm, quotationMarkDenormalizationFirstPass);
        List<(int ChapterNumber, QuotationMarkUpdateStrategy Strategy)> bestChapterStrategies =
            quotationMarkDenormalizationFirstPass.FindBestChapterStrategies();

        QuotationMarkDenormalizationUsfmUpdateBlockHandler quotationMarkDenormalizer =
            new(
                targetQuoteConvention,
                new QuotationMarkUpdateSettings(
                    chapterStrategies: bestChapterStrategies.Select(tuple => tuple.Strategy).ToList()
                )
            );
        int denormalizableChapterCount = bestChapterStrategies.Count(tup =>
            tup.Strategy != QuotationMarkUpdateStrategy.Skip
        );
        List<string> remarks = [];
        string quotationDenormalizationRemark;
        if (denormalizableChapterCount == bestChapterStrategies.Count)
        {
            quotationDenormalizationRemark =
                "The quote style in all chapters has been automatically adjusted to match the rest of the project.";
        }
        else if (denormalizableChapterCount > 0)
        {
            quotationDenormalizationRemark =
                "The quote style in the following chapters has been automatically adjusted to match the rest of the project: "
                + GetChapterRangesString(
                    bestChapterStrategies
                        .Where(tuple => tuple.Strategy != QuotationMarkUpdateStrategy.Skip)
                        .Select(tuple => tuple.ChapterNumber)
                        .ToList()
                )
                + ".";
        }
        else
        {
            quotationDenormalizationRemark =
                "The quote style was not automatically adjusted to match the rest of your project in any chapters.";
        }
        remarks.Add(quotationDenormalizationRemark);

        var updater = new UpdateUsfmParserHandler(updateBlockHandlers: [quotationMarkDenormalizer], remarks: remarks);
        UsfmParser.Parse(usfm, updater);

        usfm = updater.GetUsfm();
        return usfm;
    }

    public static string GetChapterRangesString(List<int> chapterNumbers)
    {
        chapterNumbers = chapterNumbers.Order().ToList();
        int start = chapterNumbers[0];
        int end = chapterNumbers[0];
        List<string> chapterRangeStrings = [];
        foreach (int chapterNumber in chapterNumbers[1..])
        {
            if (chapterNumber == end + 1)
            {
                end = chapterNumber;
            }
            else
            {
                if (start == end)
                {
                    chapterRangeStrings.Add(start.ToString(CultureInfo.InvariantCulture));
                }
                else
                {
                    chapterRangeStrings.Add($"{start}-{end}");
                }
                start = chapterNumber;
                end = chapterNumber;
            }
        }
        if (start == end)
        {
            chapterRangeStrings.Add(start.ToString(CultureInfo.InvariantCulture));
        }
        else
        {
            chapterRangeStrings.Add($"{start}-{end}");
        }
        return string.Join(", ", chapterRangeStrings);
    }

    /// <summary>
    /// Generate a natural sounding remark/comment describing marker placement.
    /// </summary>
    /// <param name="paragraphMarkerBehavior">The paragraph marker behavior.</param>
    /// <param name="embedBehavior">The embed marker behavior.</param>
    /// <param name="styleMarkerBehavior">The style marker behavior.</param>
    /// <returns>One to three sentences describing the marker placement behavior.</returns>
    /// <remarks>
    /// <para>Remarks are generated in the format:</para>
    /// <list type="bullet">
    /// <item><description>
    /// Paragraph breaks, embed markers, and style markers were moved to the end of the verse.
    /// </description></item>
    /// <item><description>
    /// Paragraph breaks were moved to the end of the verse. Embed markers have positions preserved. Style markers were removed.
    /// </description></item>
    /// <item><description>
    /// Paragraph breaks and style markers were moved to the end of the verse. Embed markers were removed.
    /// </description></item>
    /// </list>
    /// </remarks>
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

        behaviorMap[paragraphMarkerBehavior].Add("paragraph breaks");
        behaviorMap[embedBehavior].Add("embed markers");
        behaviorMap[styleMarkerBehavior].Add("style markers");

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
                return $"{markers} {behavior}.";
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

    private static UpdateUsfmRow Map(
        (
            IReadOnlyList<ScriptureRef> ScriptureRefs,
            Pretranslation Pretranslation,
            PretranslationUsfmMarkerBehavior ParagraphBehavior,
            PretranslationUsfmMarkerBehavior StyleBehavior
        ) pretranslationRow
    )
    {
        return new UpdateUsfmRow(
            pretranslationRow.ScriptureRefs,
            pretranslationRow.Pretranslation.Translation,
            pretranslationRow.Pretranslation.Alignment is not null
                ? new Dictionary<string, object>
                {
                    {
                        PlaceMarkersAlignmentInfo.MetadataKey,
                        new PlaceMarkersAlignmentInfo(
                            pretranslationRow.Pretranslation.SourceTokens?.ToList() ?? [],
                            pretranslationRow.Pretranslation.TranslationTokens?.ToList() ?? [],
                            Map(pretranslationRow.Pretranslation.Alignment),
                            paragraphBehavior: Map(pretranslationRow.ParagraphBehavior),
                            styleBehavior: Map(pretranslationRow.StyleBehavior)
                        )
                    }
                }
                : null
        );
    }
}
