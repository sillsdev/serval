namespace Serval.Translation.Features.Engines;

#pragma warning disable CS0612 // Type or member is obsolete

public record TranslationCorpusUpdateConfigDto : IValidatableObject
{
    public IReadOnlyList<TranslationCorpusFileConfigDto>? SourceFiles { get; init; }

    public IReadOnlyList<TranslationCorpusFileConfigDto>? TargetFiles { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (SourceFiles is null && TargetFiles is null)
        {
            yield return new ValidationResult(
                "At least one field must be specified.",
                [nameof(SourceFiles), nameof(TargetFiles)]
            );
        }
    }
}

public record UpdateCorpus(
    string Owner,
    string EngineId,
    string CorpusId,
    TranslationCorpusUpdateConfigDto CorpusConfig
) : IRequest<UpdateCorpusResponse>;

public record UpdateCorpusResponse(TranslationCorpusDto Corpus);

public class UpdateCorpusHandler(
    IDataAccessContext dataAccessContext,
    IRepository<Engine> engines,
    IRepository<Pretranslation> pretranslations,
    IRequestHandler<GetDataFile, GetDataFileResponse> getDataFileHandler,
    DtoMapper mapper
) : IRequestHandler<UpdateCorpus, UpdateCorpusResponse>
{
    public Task<UpdateCorpusResponse> HandleAsync(UpdateCorpus request, CancellationToken cancellationToken = default)
    {
        return dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                await engines.CheckOwnerAsync(request.EngineId, request.Owner, ct);

                IReadOnlyList<CorpusFile>? sourceFiles = request.CorpusConfig.SourceFiles is null
                    ? null
                    : await MapFilesAsync(request.Owner, request.CorpusConfig.SourceFiles, ct);
                IReadOnlyList<CorpusFile>? targetFiles = request.CorpusConfig.TargetFiles is null
                    ? null
                    : await MapFilesAsync(request.Owner, request.CorpusConfig.TargetFiles, ct);

                Engine? updatedEngine = await engines.UpdateAsync(
                    e => e.Id == request.EngineId && e.Corpora.Any(c => c.Id == request.CorpusId),
                    u =>
                    {
                        if (sourceFiles is not null)
                            u.Set(c => c.Corpora.FirstMatchingElement().SourceFiles, sourceFiles);
                        if (targetFiles is not null)
                            u.Set(c => c.Corpora.FirstMatchingElement().TargetFiles, targetFiles);
                    },
                    cancellationToken: ct
                );
                if (updatedEngine is null)
                {
                    throw new EntityNotFoundException(
                        $"Could not find the Corpus '{request.CorpusId}' in Engine '{request.EngineId}'."
                    );
                }
                await pretranslations.DeleteAllAsync(pt => pt.CorpusRef == request.CorpusId, ct);
                Corpus corpus = updatedEngine.Corpora.First(c => c.Id == request.CorpusId);
                return new UpdateCorpusResponse(mapper.Map(request.EngineId, corpus));
            },
            cancellationToken
        );
    }

    private async Task<List<CorpusFile>> MapFilesAsync(
        string owner,
        IEnumerable<TranslationCorpusFileConfigDto> fileConfigs,
        CancellationToken cancellationToken
    )
    {
        var files = new List<CorpusFile>();
        foreach (TranslationCorpusFileConfigDto fileConfig in fileConfigs)
        {
            GetDataFileResponse response = await getDataFileHandler.HandleAsync(
                new(fileConfig.FileId, owner),
                cancellationToken
            );
            if (response.IsFound)
            {
                files.Add(
                    new CorpusFile
                    {
                        Id = fileConfig.FileId,
                        Filename = response.File.Filename,
                        TextId = fileConfig.TextId ?? response.File.Name,
                        Format = response.File.Format,
                    }
                );
            }
            else
            {
                throw new InvalidOperationException($"The data file {fileConfig.FileId} cannot be found.");
            }
        }
        return files;
    }
}

public partial class TranslationEnginesController
{
    /// <summary>
    /// Update a corpus with a new set of files (obsolete - use parallel corpora instead)
    /// </summary>
    /// <remarks>
    /// See posting a new corpus for details of use. Will completely replace corpus' file associations.
    /// Will not affect jobs already queued or running. Will not affect existing pretranslations until new build is complete.
    /// </remarks>
    /// <param name="id">The translation engine id</param>
    /// <param name="corpusId">The corpus id</param>
    /// <param name="corpusConfig">The corpus configuration</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The corpus was updated successfully</response>
    /// <response code="400">Bad request</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the translation engine.</response>
    /// <response code="404">The engine or corpus does not exist.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Obsolete("This endpoint is obsolete. Use parallel corpora instead.")]
    [Authorize(Scopes.UpdateTranslationEngines)]
    [HttpPatch("{id}/corpora/{corpusId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<TranslationCorpusDto>> UpdateCorpusAsync(
        [NotNull] string id,
        [NotNull] string corpusId,
        [FromBody] TranslationCorpusUpdateConfigDto corpusConfig,
        [FromServices] IRequestHandler<UpdateCorpus, UpdateCorpusResponse> handler,
        CancellationToken cancellationToken
    )
    {
        UpdateCorpusResponse response = await handler.HandleAsync(
            new(Owner, id, corpusId, corpusConfig),
            cancellationToken
        );
        return Ok(response.Corpus);
    }
}

#pragma warning restore CS0612 // Type or member is obsolete
