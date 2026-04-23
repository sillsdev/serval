namespace Serval.Translation.Features.Engines;

public record TranslationParallelCorpusConfigDto
{
    /// <summary>
    /// The corpus name.
    /// </summary>
    public string? Name { get; init; }

    public required IReadOnlyList<string> SourceCorpusIds { get; init; }
    public required IReadOnlyList<string> TargetCorpusIds { get; init; }
}

public record AddParallelCorpus(string Owner, string EngineId, TranslationParallelCorpusConfigDto CorpusConfig)
    : IRequest<AddParallelCorpusResponse>;

public record AddParallelCorpusResponse(TranslationParallelCorpusDto Corpus);

public class AddParallelCorpusHandler(
    IRepository<Engine> engines,
    IIdGenerator idGenerator,
    IRequestHandler<GetCorpus, GetCorpusResponse> getCorpusHandler,
    DtoMapper mapper
) : IRequestHandler<AddParallelCorpus, AddParallelCorpusResponse>
{
    public async Task<AddParallelCorpusResponse> HandleAsync(
        AddParallelCorpus request,
        CancellationToken cancellationToken = default
    )
    {
        await engines.CheckOwnerAsync(request.EngineId, request.Owner, cancellationToken);

        ParallelCorpus corpus = new()
        {
            Id = idGenerator.GenerateId(),
            SourceCorpora = await MapCorporaAsync(
                request.Owner,
                request.CorpusConfig.SourceCorpusIds,
                cancellationToken
            ),
            TargetCorpora = await MapCorporaAsync(
                request.Owner,
                request.CorpusConfig.TargetCorpusIds,
                cancellationToken
            ),
        };
        await engines.UpdateAsync(
            e => e.Id == request.EngineId,
            u => u.Add(e => e.ParallelCorpora, corpus),
            cancellationToken: cancellationToken
        );
        return new(mapper.Map(request.EngineId, corpus));
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
    /// Add a parallel corpus to a translation engine
    /// </summary>
    /// <remarks>
    /// ## Parameters
    /// * **`sourceCorpusIds`**: The source corpora associated with the parallel corpus
    /// * **`targetCorpusIds`**: The target corpora associated with the parallel corpus
    /// </remarks>
    /// <param name="id">The translation engine id</param>
    /// <param name="corpusConfig">The corpus configuration (see remarks)</param>
    /// <param name="cancellationToken"></param>
    /// <response code="201">The added corpus</response>
    /// <response code="400">Bad request</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the translation engine.</response>
    /// <response code="404">The engine does not exist.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.UpdateTranslationEngines)]
    [HttpPost("{id}/parallel-corpora")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<TranslationParallelCorpusDto>> AddParallelCorpusAsync(
        [NotNull] string id,
        [FromBody] TranslationParallelCorpusConfigDto corpusConfig,
        [FromServices] IRequestHandler<AddParallelCorpus, AddParallelCorpusResponse> handler,
        CancellationToken cancellationToken
    )
    {
        AddParallelCorpusResponse response = await handler.HandleAsync(new(Owner, id, corpusConfig), cancellationToken);
        return Created(response.Corpus.Url, response.Corpus);
    }
}
