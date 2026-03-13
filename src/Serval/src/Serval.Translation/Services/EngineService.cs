using MassTransit.Mediator;

namespace Serval.Translation.Services;

public class EngineService(
    IRepository<Engine> engines,
    IRepository<Pretranslation> pretranslations,
    IScopedMediator mediator,
    IDataAccessContext dataAccessContext
) : OwnedEntityServiceBase<Engine>(engines), IEngineService
{
    private readonly IRepository<Pretranslation> _pretranslations = pretranslations;
    private readonly IScopedMediator _mediator = mediator;
    private readonly IDataAccessContext _dataAccessContext = dataAccessContext;

    public Task AddCorpusAsync(string engineId, Corpus corpus, CancellationToken cancellationToken = default)
    {
        return Entities.UpdateAsync(
            e => e.Id == engineId,
            u => u.Add(e => e.Corpora, corpus),
            cancellationToken: cancellationToken
        );
    }

    public Task<Corpus> UpdateCorpusAsync(
        string engineId,
        string corpusId,
        IReadOnlyList<CorpusFile>? sourceFiles,
        IReadOnlyList<CorpusFile>? targetFiles,
        CancellationToken cancellationToken = default
    )
    {
        return _dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                Engine? engine = await Entities.UpdateAsync(
                    e => e.Id == engineId && e.Corpora.Any(c => c.Id == corpusId),
                    u =>
                    {
                        if (sourceFiles is not null)
                            u.Set(c => c.Corpora.FirstMatchingElement().SourceFiles, sourceFiles);
                        if (targetFiles is not null)
                            u.Set(c => c.Corpora.FirstMatchingElement().TargetFiles, targetFiles);
                    },
                    cancellationToken: ct
                );
                if (engine is null)
                {
                    throw new EntityNotFoundException(
                        $"Could not find the Corpus '{corpusId}' in Engine '{engineId}'."
                    );
                }

                await _pretranslations.DeleteAllAsync(pt => pt.CorpusRef == corpusId, ct);
                return engine.Corpora.First(c => c.Id == corpusId);
            },
            cancellationToken
        );
    }

    public Task DeleteCorpusAsync(
        string engineId,
        string corpusId,
        bool deleteFiles,
        CancellationToken cancellationToken = default
    )
    {
        return _dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                Engine? originalEngine = await Entities.UpdateAsync(
                    e => e.Id == engineId,
                    u => u.RemoveAll(e => e.Corpora, c => c.Id == corpusId),
                    returnOriginal: true,
                    cancellationToken: ct
                );
                if (originalEngine is null || !originalEngine.Corpora.Any(c => c.Id == corpusId))
                {
                    throw new EntityNotFoundException(
                        $"Could not find the Corpus '{corpusId}' in Engine '{engineId}'."
                    );
                }
                await _pretranslations.DeleteAllAsync(pt => pt.CorpusRef == corpusId, ct);

                if (deleteFiles && originalEngine != null)
                {
                    foreach (
                        string id in originalEngine.Corpora.SelectMany(c =>
                            c.TargetFiles.Select(f => f.Id).Concat(c.SourceFiles.Select(f => f.Id).Distinct())
                        )
                    )
                    {
                        await _mediator.Send<DeleteDataFile>(new { DataFileId = id }, ct);
                    }
                }
            },
            cancellationToken
        );
    }

    public Task AddParallelCorpusAsync(
        string engineId,
        ParallelCorpus corpus,
        CancellationToken cancellationToken = default
    )
    {
        return Entities.UpdateAsync(
            e => e.Id == engineId,
            u => u.Add(e => e.ParallelCorpora, corpus),
            cancellationToken: cancellationToken
        );
    }

    public Task<ParallelCorpus> UpdateParallelCorpusAsync(
        string engineId,
        string parallelCorpusId,
        IReadOnlyList<MonolingualCorpus>? sourceCorpora,
        IReadOnlyList<MonolingualCorpus>? targetCorpora,
        CancellationToken cancellationToken = default
    )
    {
        return _dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                Engine? engine = await Entities.UpdateAsync(
                    e => e.Id == engineId && e.ParallelCorpora.Any(c => c.Id == parallelCorpusId),
                    u =>
                    {
                        if (sourceCorpora is not null)
                            u.Set(c => c.ParallelCorpora.FirstMatchingElement().SourceCorpora, sourceCorpora);
                        if (targetCorpora is not null)
                            u.Set(c => c.ParallelCorpora.FirstMatchingElement().TargetCorpora, targetCorpora);
                    },
                    cancellationToken: ct
                );
                if (engine is null)
                {
                    throw new EntityNotFoundException($"Could not find the Engine '{engineId}'.");
                }

                await _pretranslations.DeleteAllAsync(pt => pt.CorpusRef == parallelCorpusId, cancellationToken: ct);
                ParallelCorpus? parallelCorpus = engine.ParallelCorpora.FirstOrDefault(c => c.Id == parallelCorpusId);
                if (parallelCorpus is null)
                {
                    throw new EntityNotFoundException(
                        $"Could not find the Corpus '{parallelCorpusId}' in Engine '{engineId}'."
                    );
                }
                return parallelCorpus;
            },
            cancellationToken
        );
    }

    public Task DeleteParallelCorpusAsync(
        string engineId,
        string parallelCorpusId,
        CancellationToken cancellationToken = default
    )
    {
        return _dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                Engine? originalEngine = await Entities.UpdateAsync(
                    e => e.Id == engineId,
                    u => u.RemoveAll(e => e.ParallelCorpora, c => c.Id == parallelCorpusId),
                    returnOriginal: true,
                    cancellationToken: ct
                );
                if (originalEngine is null || !originalEngine.ParallelCorpora.Any(c => c.Id == parallelCorpusId))
                {
                    throw new EntityNotFoundException(
                        $"Could not find the Corpus '{parallelCorpusId}' in Engine '{engineId}'."
                    );
                }
                await _pretranslations.DeleteAllAsync(pt => pt.CorpusRef == parallelCorpusId, ct);
            },
            cancellationToken
        );
    }

    public async Task DeleteAllCorpusFilesAsync(string dataFileId, CancellationToken cancellationToken = default)
    {
        await _dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                HashSet<string> parallelCorpusIds = (
                    await Entities.GetAllAsync(
                        e =>
                            e.ParallelCorpora.Any(c =>
                                c.SourceCorpora.Any(cs => cs.Files.Any(f => f.Id == dataFileId))
                                || c.TargetCorpora.Any(tc => tc.Files.Any(f => f.Id == dataFileId))
                            ),
                        ct
                    )
                )
                    .SelectMany(e => e.ParallelCorpora.Select(c => c.Id))
                    .ToHashSet();

                HashSet<string> corpusIds = (
                    await Entities.GetAllAsync(
                        e =>
                            e.Corpora.Any(c =>
                                c.SourceFiles.Any(f => f.Id == dataFileId) || c.TargetFiles.Any(f => f.Id == dataFileId)
                            ),
                        ct
                    )
                )
                    .SelectMany(e => e.Corpora.Select(c => c.Id))
                    .ToHashSet();

                await Entities.UpdateAllAsync(
                    e =>
                        e.Corpora.Any(c =>
                            c.SourceFiles.Any(f => f.Id == dataFileId) || c.TargetFiles.Any(f => f.Id == dataFileId)
                        )
                        || e.ParallelCorpora.Any(c =>
                            c.SourceCorpora.Any(sc => sc.Files.Any(f => f.Id == dataFileId))
                            || c.TargetCorpora.Any(tc => tc.Files.Any(f => f.Id == dataFileId))
                        ),
                    u =>
                    {
                        u.RemoveAll(e => e.Corpora.AllElements().SourceFiles, f => f.Id == dataFileId);
                        u.RemoveAll(e => e.Corpora.AllElements().TargetFiles, f => f.Id == dataFileId);
                        u.RemoveAll(
                            e => e.ParallelCorpora.AllElements().SourceCorpora.AllElements().Files,
                            f => f.Id == dataFileId
                        );
                        u.RemoveAll(
                            e => e.ParallelCorpora.AllElements().TargetCorpora.AllElements().Files,
                            f => f.Id == dataFileId
                        );
                    },
                    cancellationToken: ct
                );

                await _pretranslations.DeleteAllAsync(
                    pt => parallelCorpusIds.Contains(pt.CorpusRef) || corpusIds.Contains(pt.CorpusRef),
                    ct
                );
            },
            cancellationToken
        );
    }

    public async Task UpdateDataFileFilenameFilesAsync(
        string dataFileId,
        string filename,
        CancellationToken cancellationToken = default
    )
    {
        await _dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                await Entities.UpdateAllAsync(
                    e =>
                        e.Corpora.Any(c =>
                            c.SourceFiles.Any(f => f.Id == dataFileId) || c.TargetFiles.Any(f => f.Id == dataFileId)
                        )
                        || e.ParallelCorpora.Any(c =>
                            c.SourceCorpora.Any(sc => sc.Files.Any(f => f.Id == dataFileId))
                            || c.TargetCorpora.Any(tc => tc.Files.Any(f => f.Id == dataFileId))
                        ),
                    u =>
                    {
                        u.SetAll(
                            e => e.Corpora.AllElements().SourceFiles,
                            f => f.Filename,
                            filename,
                            f => f.Id == dataFileId
                        );
                        u.SetAll(
                            e => e.Corpora.AllElements().TargetFiles,
                            f => f.Filename,
                            filename,
                            f => f.Id == dataFileId
                        );
                        u.SetAll(
                            e => e.ParallelCorpora.AllElements().SourceCorpora.AllElements().Files,
                            f => f.Filename,
                            filename,
                            f => f.Id == dataFileId
                        );
                        u.SetAll(
                            e => e.ParallelCorpora.AllElements().TargetCorpora.AllElements().Files,
                            f => f.Filename,
                            filename,
                            f => f.Id == dataFileId
                        );
                    },
                    cancellationToken: cancellationToken
                );

                HashSet<string> parallelCorpusIds = (
                    await Entities.GetAllAsync(
                        e =>
                            e.ParallelCorpora.Any(c =>
                                c.SourceCorpora.Any(cs => cs.Files.Any(f => f.Id == dataFileId))
                                || c.TargetCorpora.Any(tc => tc.Files.Any(f => f.Id == dataFileId))
                            ),
                        cancellationToken: cancellationToken
                    )
                )
                    .SelectMany(e => e.ParallelCorpora.Select(c => c.Id))
                    .ToHashSet();

                HashSet<string> corpusIds = (
                    await Entities.GetAllAsync(
                        e =>
                            e.Corpora.Any(c =>
                                c.SourceFiles.Any(f => f.Id == dataFileId) || c.TargetFiles.Any(f => f.Id == dataFileId)
                            ),
                        cancellationToken
                    )
                )
                    .SelectMany(e => e.Corpora.Select(c => c.Id))
                    .ToHashSet();

                await _pretranslations.DeleteAllAsync(
                    pt => parallelCorpusIds.Contains(pt.CorpusRef) || corpusIds.Contains(pt.CorpusRef),
                    cancellationToken: cancellationToken
                );
            },
            cancellationToken: cancellationToken
        );
    }

    public Task UpdateCorpusFilesAsync(
        string corpusId,
        IReadOnlyList<CorpusFile> files,
        CancellationToken cancellationToken = default
    )
    {
        return _dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                await Entities.UpdateAllAsync(
                    e =>
                        e.ParallelCorpora.Any(c =>
                            c.SourceCorpora.Any(sc => sc.Id == corpusId) || c.TargetCorpora.Any(tc => tc.Id == corpusId)
                        ),
                    u =>
                    {
                        u.SetAll(
                            e => e.ParallelCorpora.AllElements().SourceCorpora,
                            mc => mc.Files,
                            files,
                            mc => mc.Id == corpusId
                        );
                        u.SetAll(
                            e => e.ParallelCorpora.AllElements().TargetCorpora,
                            mc => mc.Files,
                            files,
                            mc => mc.Id == corpusId
                        );
                    },
                    cancellationToken: ct
                );
                await _pretranslations.DeleteAllAsync(pt => pt.CorpusRef == corpusId, ct);
            },
            cancellationToken
        );
    }
}
