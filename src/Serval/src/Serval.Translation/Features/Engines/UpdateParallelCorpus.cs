namespace Serval.Translation.Features.Engines;

public record TranslationParallelCorpusUpdateConfigDto : IValidatableObject
{
    public IReadOnlyList<string>? SourceCorpusIds { get; init; }

    public IReadOnlyList<string>? TargetCorpusIds { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (SourceCorpusIds is null && TargetCorpusIds is null)
        {
            yield return new ValidationResult(
                "At least one field must be specified.",
                [nameof(SourceCorpusIds), nameof(TargetCorpusIds)]
            );
        }
    }
}

public record UpdateParallelCorpus(
    string Owner,
    string EngineId,
    string ParallelCorpusId,
    TranslationParallelCorpusUpdateConfigDto CorpusConfig
) : IRequest<UpdateParallelCorpusResponse>;

public record UpdateParallelCorpusResponse(TranslationParallelCorpusDto Corpus);

public class UpdateParallelCorpusHandler(
    IDataAccessContext dataAccessContext,
    IRepository<Engine> engines,
    IRepository<Pretranslation> pretranslations,
    IRequestHandler<GetCorpus, GetCorpusResponse> getCorpusHandler,
    DtoMapper mapper
) : IRequestHandler<UpdateParallelCorpus, UpdateParallelCorpusResponse>
{
    public Task<UpdateParallelCorpusResponse> HandleAsync(
        UpdateParallelCorpus request,
        CancellationToken cancellationToken = default
    )
    {
        return dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                await engines.CheckOwnerAsync(request.EngineId, request.Owner, ct);

                IReadOnlyList<MonolingualCorpus>? sourceCorpora = request.CorpusConfig.SourceCorpusIds is null
                    ? null
                    : await MapCorporaAsync(request.Owner, request.CorpusConfig.SourceCorpusIds, ct);
                IReadOnlyList<MonolingualCorpus>? targetCorpora = request.CorpusConfig.TargetCorpusIds is null
                    ? null
                    : await MapCorporaAsync(request.Owner, request.CorpusConfig.TargetCorpusIds, ct);

                Engine? updatedEngine = await engines.UpdateAsync(
                    e => e.Id == request.EngineId && e.ParallelCorpora.Any(c => c.Id == request.ParallelCorpusId),
                    u =>
                    {
                        if (sourceCorpora is not null)
                        {
                            u.Set(c => c.ParallelCorpora.FirstMatchingElement().SourceCorpora, sourceCorpora);
                        }
                        if (targetCorpora is not null)
                        {
                            u.Set(c => c.ParallelCorpora.FirstMatchingElement().TargetCorpora, targetCorpora);
                        }
                    },
                    cancellationToken: ct
                );
                if (updatedEngine is null)
                    throw new EntityNotFoundException($"Could not find the Engine '{request.EngineId}'.");

                await pretranslations.DeleteAllAsync(
                    pt => pt.CorpusRef == request.ParallelCorpusId,
                    cancellationToken: ct
                );
                ParallelCorpus? parallelCorpus = updatedEngine.ParallelCorpora.FirstOrDefault(c =>
                    c.Id == request.ParallelCorpusId
                );
                if (parallelCorpus is null)
                {
                    throw new EntityNotFoundException(
                        $"Could not find the Corpus '{request.ParallelCorpusId}' in Engine '{request.EngineId}'."
                    );
                }
                return new UpdateParallelCorpusResponse(mapper.Map(request.EngineId, parallelCorpus));
            },
            cancellationToken
        );
    }

    private async Task<List<MonolingualCorpus>> MapCorporaAsync(
        string owner,
        IEnumerable<string> corpusIds,
        CancellationToken cancellationToken
    )
    {
        var corpora = new List<MonolingualCorpus>();
        foreach (string corpusId in corpusIds)
        {
            GetCorpusResponse response = await getCorpusHandler.HandleAsync(new(corpusId, owner), cancellationToken);
            if (response.IsFound)
            {
                if (!response.Corpus.Files.Any())
                {
                    throw new InvalidOperationException(
                        $"The corpus {corpusId} does not have any files associated with it."
                    );
                }
                corpora.Add(
                    new MonolingualCorpus
                    {
                        Id = corpusId,
                        Name = response.Corpus.Name ?? "",
                        Language = response.Corpus.Language,
                        Files =
                        [
                            .. response.Corpus.Files.Select(f => new CorpusFile
                            {
                                Id = f.File.DataFileId,
                                Filename = f.File.Filename,
                                Format = f.File.Format,
                                TextId = f.TextId ?? f.File.Name,
                            }),
                        ],
                    }
                );
            }
            else
            {
                throw new InvalidOperationException($"The corpus {corpusId} cannot be found.");
            }
        }
        return corpora;
    }
}

public partial class TranslationEnginesController
{
    /// <summary>
    /// Update a parallel corpus with a new set of corpora
    /// </summary>
    /// <remarks>
    /// Will completely replace the parallel corpus' file associations. Will not affect jobs already queued or running. Will not affect existing pretranslations until new build is complete.
    /// </remarks>
    /// <param name="id">The translation engine id</param>
    /// <param name="parallelCorpusId">The parallel corpus id</param>
    /// <param name="corpusConfig">The corpus configuration</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The corpus was updated successfully</response>
    /// <response code="400">Bad request</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the translation engine.</response>
    /// <response code="404">The engine or corpus does not exist.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.UpdateTranslationEngines)]
    [HttpPatch("{id}/parallel-corpora/{parallelCorpusId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<TranslationParallelCorpusDto>> UpdateParallelCorpusAsync(
        [NotNull] string id,
        [NotNull] string parallelCorpusId,
        [FromBody] TranslationParallelCorpusUpdateConfigDto corpusConfig,
        [FromServices] IRequestHandler<UpdateParallelCorpus, UpdateParallelCorpusResponse> handler,
        CancellationToken cancellationToken
    )
    {
        UpdateParallelCorpusResponse response = await handler.HandleAsync(
            new(Owner, id, parallelCorpusId, corpusConfig),
            cancellationToken
        );
        return Ok(response.Corpus);
    }
}
