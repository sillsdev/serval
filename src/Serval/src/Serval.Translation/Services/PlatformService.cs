using SIL.Machine.Corpora;

namespace Serval.Translation.Services;

public class PlatformService(
    IRepository<Build> builds,
    IRepository<Engine> engines,
    IRepository<Pretranslation> pretranslations,
    IDataAccessContext dataAccessContext,
    IEventRouter eventRouter,
    IBuildDiagnosticService buildDiagnosticService
) : ITranslationPlatformService
{
    private const int PretranslationInsertBatchSize = 128;
    private readonly IRepository<Build> _builds = builds;
    private readonly IRepository<Engine> _engines = engines;
    private readonly IRepository<Pretranslation> _pretranslations = pretranslations;
    private readonly IDataAccessContext _dataAccessContext = dataAccessContext;
    private readonly IEventRouter _eventRouter = eventRouter;
    private readonly IBuildDiagnosticService _buildDiagnosticService = buildDiagnosticService;

    public async Task BuildStartedAsync(string buildId, CancellationToken cancellationToken = default)
    {
        await _dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                Build? build = await _builds.UpdateAsync(
                    buildId,
                    u => u.Set(b => b.State, JobState.Active),
                    cancellationToken: ct
                );
                if (build is null)
                    throw new EntityNotFoundException($"Could not find the Build '{buildId}'.");

                Engine? engine = await _engines.UpdateAsync(
                    build.EngineRef,
                    u => u.Set(e => e.IsBuilding, true),
                    cancellationToken: ct
                );
                if (engine is null)
                {
                    throw new EntityNotFoundException($"Could not find the Engine '{build.EngineRef}'.");
                }

                await _eventRouter.PublishAsync(new TranslationBuildStarted(build.Id, engine.Id, engine.Owner), ct);
            },
            cancellationToken: cancellationToken
        );
    }

    public async Task BuildCompletedAsync(
        string buildId,
        int corpusSize,
        double confidence,
        CancellationToken cancellationToken = default
    )
    {
        await _dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                Build? build = await _builds.UpdateAsync(
                    buildId,
                    u =>
                        u.Set(b => b.State, JobState.Completed)
                            .Set(b => b.Message, "Completed")
                            .Set(b => b.DateFinished, DateTime.UtcNow),
                    cancellationToken: ct
                );
                if (build is null)
                    throw new EntityNotFoundException($"Could not find the Build '{buildId}'.");

                Engine? engine = await _engines.UpdateAsync(
                    build.EngineRef,
                    u =>
                        u.Set(e => e.Confidence, confidence)
                            .Set(e => e.CorpusSize, corpusSize)
                            .Set(e => e.IsBuilding, false)
                            .Inc(e => e.ModelRevision),
                    cancellationToken: ct
                );
                if (engine is null)
                {
                    throw new EntityNotFoundException($"Could not find the Engine '{build.EngineRef}'.");
                }

                // delete pretranslations created by the previous build
                await _pretranslations.DeleteAllAsync(
                    p => p.EngineRef == engine.Id && p.ModelRevision < engine.ModelRevision,
                    ct
                );

                await _eventRouter.PublishAsync(
                    new TranslationBuildFinished(
                        build.Id,
                        engine.Id,
                        engine.Owner,
                        build.State,
                        build.Message!,
                        build.DateFinished!.Value
                    ),
                    ct
                );
            },
            cancellationToken: cancellationToken
        );
    }

    public async Task BuildCanceledAsync(string buildId, CancellationToken cancellationToken = default)
    {
        await _dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                Build? build = await _builds.UpdateAsync(
                    buildId,
                    u =>
                        u.Set(b => b.Message, "Canceled")
                            .Set(b => b.DateFinished, DateTime.UtcNow)
                            .Set(b => b.State, JobState.Canceled),
                    cancellationToken: ct
                );
                if (build is null)
                    throw new EntityNotFoundException($"Could not find the Build '{buildId}'.");

                Engine? engine = await _engines.UpdateAsync(
                    build.EngineRef,
                    u => u.Set(e => e.IsBuilding, false),
                    cancellationToken: ct
                );
                if (engine is null)
                {
                    throw new EntityNotFoundException($"Could not find the Engine '{build.EngineRef}'.");
                }

                // delete pretranslations that might have been created during the build
                await _pretranslations.DeleteAllAsync(
                    p => p.EngineRef == engine.Id && p.ModelRevision > engine.ModelRevision,
                    ct
                );

                await _eventRouter.PublishAsync(
                    new TranslationBuildFinished(
                        build.Id,
                        engine.Id,
                        engine.Owner,
                        build.State,
                        build.Message!,
                        build.DateFinished!.Value
                    ),
                    ct
                );
            },
            cancellationToken: cancellationToken
        );
    }

    public async Task BuildFaultedAsync(string buildId, string message, CancellationToken cancellationToken = default)
    {
        await _dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                Build? build = await _builds.UpdateAsync(
                    buildId,
                    u =>
                        u.Set(b => b.State, JobState.Faulted)
                            .Set(b => b.Message, message)
                            .Set(b => b.DateFinished, DateTime.UtcNow),
                    cancellationToken: ct
                );
                if (build is null)
                    throw new EntityNotFoundException($"Could not find the Build '{buildId}'.");

                Engine? engine = await _engines.UpdateAsync(
                    build.EngineRef,
                    u => u.Set(e => e.IsBuilding, false),
                    cancellationToken: ct
                );
                if (engine is null)
                {
                    throw new EntityNotFoundException($"Could not find the Engine '{build.EngineRef}'.");
                }

                // delete pretranslations that might have been created during the build
                await _pretranslations.DeleteAllAsync(
                    p => p.EngineRef == engine.Id && p.ModelRevision > engine.ModelRevision,
                    ct
                );

                await _eventRouter.PublishAsync(
                    new TranslationBuildFinished(
                        build.Id,
                        engine.Id,
                        engine.Owner,
                        build.State,
                        build.Message!,
                        build.DateFinished!.Value
                    ),
                    ct
                );
            },
            cancellationToken: cancellationToken
        );
    }

    public async Task BuildRestartingAsync(string buildId, CancellationToken cancellationToken = default)
    {
        await _dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                Build? build = await _builds.UpdateAsync(
                    buildId,
                    u =>
                        u.Set(b => b.Message, "Restarting")
                            .Set(b => b.Step, 0)
                            .Set(b => b.Progress, 0)
                            .Set(b => b.State, JobState.Pending),
                    cancellationToken: ct
                );
                if (build is null)
                    throw new EntityNotFoundException($"Could not find the Build '{buildId}'.");

                Engine? engine = await _engines.GetAsync(build.EngineRef, ct);
                if (engine is null)
                {
                    throw new EntityNotFoundException($"Could not find the Engine '{build.EngineRef}'.");
                }

                // delete pretranslations that might have been created during the build
                await _pretranslations.DeleteAllAsync(
                    p => p.EngineRef == engine.Id && p.ModelRevision > engine.ModelRevision,
                    ct
                );
            },
            cancellationToken: cancellationToken
        );
    }

    public async Task<bool> BuildExistsAsync(string buildId, CancellationToken cancellationToken = default)
    {
        return await _builds.ExistsAsync(b => b.Id == buildId, cancellationToken);
    }

    public async Task UpdateBuildStatusAsync(
        string buildId,
        BuildProgressStatusContract progressStatus,
        int? queueDepth = null,
        IReadOnlyCollection<PhaseContract>? phases = null,
        DateTime? started = null,
        DateTime? completed = null,
        CancellationToken cancellationToken = default
    )
    {
        await _builds.UpdateAsync(
            b => b.Id == buildId && (b.State == JobState.Active || b.State == JobState.Pending),
            u =>
            {
                u.Set(b => b.Step, progressStatus.Step);
                if (progressStatus.PercentCompleted.HasValue)
                {
                    u.Set(
                        b => b.Progress,
                        Math.Round(progressStatus.PercentCompleted.Value, 4, MidpointRounding.AwayFromZero)
                    );
                }
                if (progressStatus.Message is not null)
                    u.Set(b => b.Message, progressStatus.Message);
                if (queueDepth.HasValue)
                    u.Set(b => b.QueueDepth, queueDepth.Value);
                if (phases is not null && phases.Count > 0)
                {
                    u.Set(
                        b => b.Phases,
                        [
                            .. phases.Select(p => new Phase
                            {
                                Stage = p.Stage,
                                Started = p.Started,
                                Step = p.Step,
                                StepCount = p.StepCount,
                            }),
                        ]
                    );
                }
                if (started.HasValue)
                    u.Set(b => b.DateStarted, started.Value);
                if (completed.HasValue)
                    u.Set(b => b.DateCompleted, completed.Value);
            },
            cancellationToken: cancellationToken
        );
    }

    public async Task UpdateBuildStatusAsync(string buildId, int step, CancellationToken cancellationToken = default)
    {
        await _builds.UpdateAsync(
            b => b.Id == buildId && (b.State == JobState.Active || b.State == JobState.Pending),
            u => u.Set(b => b.Step, step),
            cancellationToken: cancellationToken
        );
    }

    public async Task UpdateBuildExecutionDataAsync(
        string engineId,
        string buildId,
        ExecutionDataContract executionData,
        CancellationToken cancellationToken = default
    )
    {
        await _builds.UpdateAsync(
            b => b.Id == buildId,
            u =>
                u.Set(
                    b => b.ExecutionData,
                    new ExecutionData
                    {
                        TrainCount = executionData.TrainCount,
                        PretranslateCount = executionData.PretranslateCount,
                        TrainVerseCount = executionData.TrainVerseCount,
                        PretranslateVerseCount = executionData.PretranslateVerseCount,
                        IsTrainFilteredByChapter = executionData.IsTrainFilteredByChapter,
                        IsPretranslateFilteredByChapter = executionData.IsPretranslateFilteredByChapter,
                        Warnings = executionData.Warnings?.ToList() ?? [],
                        Diagnostics = executionData
                            .Diagnostics?.Select(d => new Diagnostic
                            {
                                Code = d.Code,
                                Category = d.Category,
                                Message = d.Message,
                                Severity = (Shared.Models.DiagnosticSeverity)d.Severity,
                                Data = d.Data,
                            })
                            .ToList(),
                        DiagnosticsTruncated = executionData.DiagnosticsTruncated,
                        EngineSourceLanguageTag = executionData.EngineSourceLanguageTag,
                        EngineTargetLanguageTag = executionData.EngineTargetLanguageTag,
                        ResolvedSourceLanguage = executionData.ResolvedSourceLanguage,
                        ResolvedTargetLanguage = executionData.ResolvedTargetLanguage,
                    }
                ),
            cancellationToken: cancellationToken
        );
    }

    public async Task UpdateTargetQuoteConventionAsync(
        string engineId,
        string buildId,
        string quoteConvention,
        CancellationToken cancellationToken = default
    )
    {
        Engine? engine = await _engines.GetAsync(engineId, cancellationToken);
        if (engine is null)
            return;
        var analysis = engine
            .ParallelCorpora.Select(pc => new ParallelCorpusAnalysis
            {
                ParallelCorpusRef = pc.Id,
                TargetQuoteConvention = quoteConvention,
            })
            .ToList();
        await _builds.UpdateAsync(
            b => b.Id == buildId && b.EngineRef == engineId,
            u =>
            {
                u.Set(b => b.TargetQuoteConvention, quoteConvention);
                u.Set(b => b.Analysis, analysis);
            },
            cancellationToken: cancellationToken
        );
    }

    public async Task IncrementEngineCorpusSizeAsync(
        string engineId,
        int count = 1,
        CancellationToken cancellationToken = default
    )
    {
        await _engines.UpdateAsync(
            engineId,
            u => u.Inc(e => e.CorpusSize, count),
            cancellationToken: cancellationToken
        );
    }

    public async Task InsertPretranslationsAsync(
        string engineId,
        string buildId,
        IAsyncEnumerable<PretranslationContract> pretranslations,
        CancellationToken cancellationToken = default
    )
    {
        Engine? engine = await _engines.GetAsync(engineId, cancellationToken);
        if (engine is null)
            throw new EntityNotFoundException($"Could not find the Engine '{engineId}'.");
        int nextModelRevision = engine.ModelRevision + 1;

        var batch = new List<Pretranslation>();
        double logConfidenceTotal = 0.0;
        int confidenceCount = 0;
        int numPretranslations = 0;
        Dictionary<string, double> logConfidenceTotalPerBook = [];
        Dictionary<string, int> confidenceCountPerBook = [];
        await foreach (PretranslationContract item in pretranslations.WithCancellation(cancellationToken))
        {
            batch.Add(
                new Pretranslation
                {
                    EngineRef = engineId,
                    ModelRevision = nextModelRevision,
                    CorpusRef = item.CorpusId,
                    TextId = item.TextId,
                    SourceRefs = item.SourceRefs.ToList(),
                    TargetRefs = item.TargetRefs.ToList(),
                    Refs = item.TargetRefs.ToList(),
                    Translation = item.Translation,
                    SourceTokens = item.SourceTokens,
                    TranslationTokens = item.TranslationTokens,
                    Alignment = item
                        .Alignment?.Select(a => new Shared.Models.AlignedWordPair
                        {
                            SourceIndex = a.SourceIndex,
                            TargetIndex = a.TargetIndex,
                            Score = a.Score,
                        })
                        .ToList(),
                    Confidence = item.Confidence,
                }
            );
            double? confidence = item.Confidence;
            if (confidence != null && confidence > 0.0)
            {
                double logConfidence = Math.Log((double)confidence);
                logConfidenceTotal += logConfidence;
                confidenceCount++;

                if (
                    item.TargetRefs.Count > 0
                    && ScriptureRef.TryParse(item.TargetRefs[0], out ScriptureRef scriptureRef)
                    && scriptureRef.IsVerse
                )
                {
                    string bookId = scriptureRef.Book;

                    if (!logConfidenceTotalPerBook.ContainsKey(bookId))
                        logConfidenceTotalPerBook[bookId] = 0.0;
                    logConfidenceTotalPerBook[bookId] += logConfidence;

                    if (!confidenceCountPerBook.ContainsKey(bookId))
                        confidenceCountPerBook[bookId] = 0;
                    confidenceCountPerBook[bookId]++;
                }
            }

            numPretranslations++;
            if (batch.Count == PretranslationInsertBatchSize)
            {
                await _pretranslations.InsertAllAsync(batch, cancellationToken);
                batch.Clear();
            }
        }
        if (batch.Count > 0)
            await _pretranslations.InsertAllAsync(batch, CancellationToken.None);

        string? model = (await _builds.GetAsync(b => b.Id == buildId, cancellationToken))?.Model;

        List<Diagnostic> badBookConfidences = logConfidenceTotalPerBook
            .Select(kvp =>
            {
                string bookId = kvp.Key;
                double logTotal = kvp.Value;
                int count = confidenceCountPerBook[bookId];
                double averageConfidence = count > 0 ? Math.Exp(logTotal / count) : 0.0;
                return (bookId, averageConfidence);
            })
            .Where(b =>
                PretranslationConfidenceEvaluator.IsBookPretranslationConfidenceUnusuallyLow(
                    b.averageConfidence,
                    b.bookId,
                    model
                )
            )
            .Select(b =>
                _buildDiagnosticService.CreateDiagnostic(
                    "MODEL-0003",
                    new Dictionary<string, object>
                    {
                        { "bookId", b.bookId },
                        { "averagePretranslationConfidence", b.averageConfidence },
                        { "modelName", model ?? "Unknown" },
                    }
                )
            )
            .Select(d => new Diagnostic
            {
                Code = d.Code,
                Category = d.Category,
                Message = d.Message,
                Severity = (Shared.Models.DiagnosticSeverity)d.Severity,
                Data = d.Data,
            })
            .ToList();

        Build? currentBuild = null;
        if (badBookConfidences.Count > 0)
        {
            currentBuild = await _builds.GetAsync(b => b.Id == buildId, cancellationToken);

            await _builds.UpdateAsync(
                b => b.Id == buildId,
                u =>
                    u.Set(
                        b => b.ExecutionData.Diagnostics,
                        currentBuild?.ExecutionData.Diagnostics is null
                            ? [.. badBookConfidences]
                            : [.. currentBuild.ExecutionData.Diagnostics, .. badBookConfidences]
                    ),
                cancellationToken: cancellationToken
            );
        }

        await _builds.UpdateAsync(
            b => b.Id == buildId,
            u =>
            {
                u.Set(
                    b => b.ExecutionData.AveragePretranslationConfidence,
                    // Calculate the geometric mean of the pretranslation confidences
                    confidenceCount > 0
                        ? Math.Exp(logConfidenceTotal / confidenceCount)
                        : 0.0
                );
                u.Set(
                    b => b.ExecutionData.Diagnostics,
                    currentBuild?.ExecutionData.Diagnostics is null
                        ? [.. badBookConfidences]
                        : [.. currentBuild.ExecutionData.Diagnostics, .. badBookConfidences]
                );
            },
            cancellationToken: cancellationToken
        );
    }
}
