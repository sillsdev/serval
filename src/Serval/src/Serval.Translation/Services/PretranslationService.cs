using SIL.Machine.Corpora;

namespace Serval.Translation.Services;

public class PretranslationService(
    IRepository<Pretranslation> pretranslations,
    IRepository<Engine> engines,
    IRepository<Build> builds,
    ICorpusMappingService corpusMappingService,
    IParallelCorpusService parallelCorpusService
) : EntityServiceBase<Pretranslation>(pretranslations), IPretranslationService
{
    private readonly IRepository<Engine> _engines = engines;
    private readonly IRepository<Build> _builds = builds;
    private readonly IParallelCorpusService _parallelCorpusService = parallelCorpusService;
    private readonly ICorpusMappingService _corpusMappingService = corpusMappingService;
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
        if (build?.DateFinished is null)
            throw new InvalidOperationException($"Could not find any completed builds for engine '{engineId}'.");

        string markerPlacementRemark = GenerateMarkerPlacementRemark(
            paragraphMarkerBehavior,
            embedBehavior,
            styleMarkerBehavior
        );

        // Determine if book level or chapter level remarks should be added
        List<(int, string)> remarks = [];
        List<int> chapters =
            build
                .Pretranslate?.SelectMany(p => p.SourceFilters ?? [])
                .SelectMany(s =>
                    ScriptureRangeParser
                        .GetChapters(s.ScriptureRange)
                        .TryGetValue(textId, out List<int>? filterChapters)
                        ? filterChapters
                        : []
                )
                .ToList()
            ?? [];
        if (chapters.Count > 0)
        {
            // Chapter level remarks
            foreach (int chapterNum in chapters)
            {
                string disclaimerRemark = string.Format(
                    CultureInfo.InvariantCulture,
                    AIDisclaimerRemark,
                    $"{textId} {chapterNum}",
                    build.DateFinished.Value.ToUniversalTime().ToString("u")
                );
                remarks.Add((chapterNum, disclaimerRemark));
                remarks.Add((chapterNum, markerPlacementRemark));
            }
        }
        else
        {
            // Book level remarks
            string disclaimerRemark = string.Format(
                CultureInfo.InvariantCulture,
                AIDisclaimerRemark,
                textId,
                build.DateFinished.Value.ToUniversalTime().ToString("u")
            );
            remarks.Add((0, disclaimerRemark));
            remarks.Add((0, markerPlacementRemark));
        }

        SIL.ServiceToolkit.Models.ParallelCorpus[] parallelCorpora = _corpusMappingService.Map(build, engine).ToArray();

        IEnumerable<SIL.ServiceToolkit.Models.ParallelRow> pretranslations = (
            await GetAllAsync(engineId, modelRevision, corpusId, textId, cancellationToken)
        ).Select(p => new SIL.ServiceToolkit.Models.ParallelRow
        {
            SourceRefs = p.SourceRefs ?? [],
            TargetRefs = p.TargetRefs ?? [],
            TargetText = p.Translation,
            Alignment = p
                .Alignment?.Select(wp => new SIL.Machine.Corpora.AlignedWordPair(wp.SourceIndex, wp.TargetIndex))
                .ToArray(),
            SourceTokens = p.SourceTokens,
            TargetTokens = p.TranslationTokens,
        });

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

            usfm = _parallelCorpusService.UpdateTargetUsfm(
                parallelCorpora,
                corpusId,
                textId,
                textOrigin == PretranslationUsfmTextOrigin.OnlyExisting ? [] : pretranslations.ToArray(),
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
            usfm = _parallelCorpusService.UpdateSourceUsfm(
                parallelCorpora,
                corpusId,
                textId,
                textOrigin == PretranslationUsfmTextOrigin.OnlyExisting ? [] : pretranslations.ToArray(),
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
