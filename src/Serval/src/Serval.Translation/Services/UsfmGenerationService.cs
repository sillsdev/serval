using SIL.Machine.Corpora;
using SIL.Machine.PunctuationAnalysis;
using SIL.Machine.Translation;
using SIL.Scripture;

namespace Serval.Translation.Services;

public class UsfmGenerationService(
    IRepository<Pretranslation> pretranslations,
    IRepository<Engine> engines,
    IRepository<Build> builds,
    ContractMapper contractMapper
) : IUsfmGenerationService
{
    private readonly IRepository<Pretranslation> _pretranslations = pretranslations;
    private readonly IRepository<Engine> _engines = engines;
    private readonly IRepository<Build> _builds = builds;
    private readonly ContractMapper _contractMapper = contractMapper;
    private const string AIDisclaimerRemark =
        "This draft of {0} was generated using AI on {1}. It should be reviewed and edited carefully.";

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
        if (engine is null)
            throw new EntityNotFoundException($"Could not find the Engine '{engineId}'.");
        Corpus? corpus = engine.Corpora.SingleOrDefault(c => c.Id == corpusId);
        ParallelCorpus? parallelCorpus = engine.ParallelCorpora.SingleOrDefault(c => c.Id == corpusId);
        if (corpus is not null)
        {
            if (corpus.SourceFiles.Count == 0)
                throw new InvalidOperationException($"The corpus {corpus.Id} has no source files.");
            if (corpus.TargetFiles.Count == 0)
                throw new InvalidOperationException($"The corpus {corpus.Id} has no target files.");
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
        }
        else
        {
            throw new EntityNotFoundException($"Could not find the corpus '{corpusId}' in engine '{engineId}'.");
        }

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

        ParallelCorpusContract[] parallelCorpora = _contractMapper.Map(build, engine).ToArray();

        IReadOnlyList<Pretranslation> pretranslations = await _pretranslations.GetAllAsync(
            pt =>
                pt.EngineRef == engineId
                && pt.ModelRevision == modelRevision
                && pt.CorpusRef == corpusId
                && (textId == null || pt.TextId == textId),
            cancellationToken
        );

        string? targetQuoteConvention = null;
        if (quoteNormalizationBehavior == PretranslationNormalizationBehavior.Denormalized)
            targetQuoteConvention = build.TargetQuoteConvention;

        string usfm = "";
        // Update the target book if it exists
        if (template is PretranslationUsfmTemplate.Auto or PretranslationUsfmTemplate.Target)
        {
            UpdateUsfmTextBehavior textBehavior = textOrigin switch
            {
                PretranslationUsfmTextOrigin.PreferExisting => UpdateUsfmTextBehavior.PreferExisting,
                PretranslationUsfmTextOrigin.PreferPretranslated => UpdateUsfmTextBehavior.PreferNew,
                PretranslationUsfmTextOrigin.OnlyExisting => UpdateUsfmTextBehavior.PreferNew,
                PretranslationUsfmTextOrigin.OnlyPretranslated => UpdateUsfmTextBehavior.StripExisting,
                _ => throw new InvalidEnumArgumentException(nameof(textOrigin)),
            };

            usfm = UpdateTargetUsfm(
                parallelCorpora,
                corpusId,
                textId,
                textOrigin == PretranslationUsfmTextOrigin.OnlyExisting ? [] : pretranslations,
                textBehavior,
                Map(paragraphMarkerBehavior),
                Map(embedBehavior),
                Map(styleMarkerBehavior),
                remarks,
                targetQuoteConvention
            );
        }

        if (
            string.IsNullOrEmpty(usfm)
            && (template is PretranslationUsfmTemplate.Auto or PretranslationUsfmTemplate.Source)
        )
        {
            // Copy and update the source book if it exists
            usfm = UpdateSourceUsfm(
                parallelCorpora,
                corpusId,
                textId,
                textOrigin == PretranslationUsfmTextOrigin.OnlyExisting ? [] : pretranslations,
                Map(paragraphMarkerBehavior),
                Map(embedBehavior),
                Map(styleMarkerBehavior),
                placeParagraphMarkers: paragraphMarkerBehavior == PretranslationUsfmMarkerBehavior.PreservePosition,
                remarks,
                targetQuoteConvention
            );
        }

        return usfm;
    }

    private static string UpdateSourceUsfm(
        IReadOnlyList<ParallelCorpusContract> parallelCorpora,
        string corpusId,
        string bookId,
        IReadOnlyList<Pretranslation> pretranslations,
        UpdateUsfmMarkerBehavior paragraphBehavior,
        UpdateUsfmMarkerBehavior embedBehavior,
        UpdateUsfmMarkerBehavior styleBehavior,
        bool placeParagraphMarkers,
        IEnumerable<string>? remarks,
        string? targetQuoteConvention
    )
    {
        return UpdateUsfm(
            parallelCorpora,
            corpusId,
            bookId,
            pretranslations,
            UpdateUsfmTextBehavior.StripExisting,
            paragraphBehavior,
            embedBehavior,
            styleBehavior,
            placeParagraphMarkers ? [new PlaceMarkersUsfmUpdateBlockHandler()] : null,
            remarks,
            targetQuoteConvention,
            isSource: true
        );
    }

    private static string UpdateTargetUsfm(
        IReadOnlyList<ParallelCorpusContract> parallelCorpora,
        string corpusId,
        string bookId,
        IReadOnlyList<Pretranslation> pretranslations,
        UpdateUsfmTextBehavior textBehavior,
        UpdateUsfmMarkerBehavior paragraphBehavior,
        UpdateUsfmMarkerBehavior embedBehavior,
        UpdateUsfmMarkerBehavior styleBehavior,
        IEnumerable<string>? remarks,
        string? targetQuoteConvention
    )
    {
        return UpdateUsfm(
            parallelCorpora,
            corpusId,
            bookId,
            pretranslations,
            textBehavior,
            paragraphBehavior,
            embedBehavior,
            styleBehavior,
            updateBlockHandlers: null,
            remarks,
            targetQuoteConvention,
            isSource: false
        );
    }

    private static string UpdateUsfm(
        IReadOnlyList<ParallelCorpusContract> parallelCorpora,
        string corpusId,
        string bookId,
        IEnumerable<Pretranslation> pretranslations,
        UpdateUsfmTextBehavior textBehavior,
        UpdateUsfmMarkerBehavior paragraphBehavior,
        UpdateUsfmMarkerBehavior embedBehavior,
        UpdateUsfmMarkerBehavior styleBehavior,
        IEnumerable<IUsfmUpdateBlockHandler>? updateBlockHandlers,
        IEnumerable<string>? remarks,
        string? targetQuoteConvention,
        bool isSource
    )
    {
        CorpusBundle corpusBundle = new(parallelCorpora);
        ParallelCorpusContract corpus = corpusBundle.ParallelCorpora.Single(c => c.Id == corpusId);
        CorpusFileContract sourceFile = corpus.SourceCorpora[0].Files[0];
        CorpusFileContract targetFile = corpus.TargetCorpora[0].Files[0];
        ParatextProjectSettings? sourceSettings = corpusBundle.GetSettings(sourceFile.Location);
        ParatextProjectSettings? targetSettings = corpusBundle.GetSettings(targetFile.Location);

        using Shared.Services.ZipParatextProjectTextUpdater updater = corpusBundle.GetTextUpdater(
            isSource ? sourceFile.Location : targetFile.Location
        );
        string usfm =
            updater.UpdateUsfm(
                bookId,
                pretranslations
                    .Select(p =>
                        Map(
                            p,
                            isSource,
                            sourceSettings?.Versification,
                            targetSettings?.Versification,
                            paragraphBehavior,
                            styleBehavior
                        )
                    )
                    .Where(row => row.Refs.Any())
                    .OrderBy(row => row.Refs[0])
                    .ToArray(),
                isSource ? sourceSettings?.FullName : targetSettings?.FullName,
                textBehavior,
                paragraphBehavior,
                embedBehavior,
                styleBehavior,
                updateBlockHandlers: updateBlockHandlers,
                remarks: remarks,
                errorHandler: (_) => true,
                compareSegments: isSource
            ) ?? "";

        if (!string.IsNullOrEmpty(targetQuoteConvention))
            usfm = DenormalizeQuotationMarks(usfm, targetQuoteConvention);
        return usfm;
    }

    private static UpdateUsfmRow Map(
        Pretranslation pretranslation,
        bool isSource,
        ScrVers? sourceVersification,
        ScrVers? targetVersification,
        UpdateUsfmMarkerBehavior paragraphBehavior,
        UpdateUsfmMarkerBehavior styleBehavior
    )
    {
        Dictionary<string, object>? metadata = null;
        if (pretranslation.Alignment is not null)
        {
            metadata = new Dictionary<string, object>
            {
                {
                    PlaceMarkersAlignmentInfo.MetadataKey,
                    new PlaceMarkersAlignmentInfo(
                        pretranslation.SourceTokens,
                        pretranslation.TranslationTokens,
                        CreateWordAlignmentMatrix(pretranslation),
                        paragraphBehavior,
                        styleBehavior
                    )
                },
            };
        }

        ScriptureRef[] refs;
        if (isSource)
        {
            refs =
            [
                .. (
                    pretranslation.SourceRefs?.Any() ?? false
                        ? Map(pretranslation.SourceRefs, sourceVersification)
                        : Map(pretranslation.TargetRefs ?? [], targetVersification)
                ),
            ];
        }
        else
        {
            // the pretranslations are generated from the source book and inserted into the target book
            // use relaxed references since the USFM structure may not be the same
            refs = [.. Map(pretranslation.TargetRefs ?? [], targetVersification).Select(r => r.ToRelaxed())];
        }

        return new UpdateUsfmRow(refs, pretranslation.Translation, metadata);
    }

    private static IEnumerable<ScriptureRef> Map(IEnumerable<string> refs, ScrVers? versification)
    {
        return refs.Select(r =>
            {
                ScriptureRef.TryParse(r, versification, out ScriptureRef sr);
                return sr;
            })
            .Where(r => !r.IsEmpty);
    }

    private static WordAlignmentMatrix? CreateWordAlignmentMatrix(Pretranslation pretranslation)
    {
        if (
            pretranslation.Alignment is null
            || pretranslation.SourceTokens is null
            || pretranslation.TranslationTokens is null
        )
        {
            return null;
        }

        var matrix = new WordAlignmentMatrix(pretranslation.SourceTokens.Count, pretranslation.TranslationTokens.Count);
        foreach (Shared.Models.AlignedWordPair wordPair in pretranslation.Alignment)
            matrix[wordPair.SourceIndex, wordPair.TargetIndex] = true;

        return matrix;
    }

    private static string DenormalizeQuotationMarks(string usfm, string quoteConvention)
    {
        QuoteConvention targetQuoteConvention = QuoteConventions.Standard.GetQuoteConventionByName(quoteConvention);
        if (targetQuoteConvention is null)
            return usfm;

        QuotationMarkDenormalizationFirstPass quotationMarkDenormalizationFirstPass = new(targetQuoteConvention);

        UsfmParser.Parse(usfm, quotationMarkDenormalizationFirstPass);
        List<(int ChapterNumber, QuotationMarkUpdateStrategy Strategy)> bestChapterStrategies =
            quotationMarkDenormalizationFirstPass.FindBestChapterStrategies();

        QuotationMarkDenormalizationUsfmUpdateBlockHandler quotationMarkDenormalizer = new(
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

    internal static string GetChapterRangesString(List<int> chapterNumbers)
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
            _ => throw new InvalidEnumArgumentException(nameof(behavior)),
        };
    }
}
