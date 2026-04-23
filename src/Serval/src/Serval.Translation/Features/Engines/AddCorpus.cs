namespace Serval.Translation.Features.Engines;

#pragma warning disable CS0612 // Type or member is obsolete

public record TranslationCorpusConfigDto
{
    /// <summary>
    /// The corpus name.
    /// </summary>
    public string? Name { get; init; }

    public required string SourceLanguage { get; init; }

    public required string TargetLanguage { get; init; }

    public required IReadOnlyList<TranslationCorpusFileConfigDto> SourceFiles { get; init; }

    public required IReadOnlyList<TranslationCorpusFileConfigDto> TargetFiles { get; init; }
}

public record AddCorpus(string Owner, string EngineId, TranslationCorpusConfigDto CorpusConfig)
    : IRequest<AddCorpusResponse>;

public record AddCorpusResponse(TranslationCorpusDto Corpus);

public class AddCorpusHandler(
    IRepository<Engine> engines,
    IIdGenerator idGenerator,
    IRequestHandler<GetDataFile, GetDataFileResponse> getDataFileHandler,
    DtoMapper mapper
) : IRequestHandler<AddCorpus, AddCorpusResponse>
{
    public async Task<AddCorpusResponse> HandleAsync(AddCorpus request, CancellationToken cancellationToken = default)
    {
        await engines.CheckOwnerAsync(request.EngineId, request.Owner, cancellationToken);

        Corpus corpus = await MapAsync(
            request.Owner,
            idGenerator.GenerateId(),
            request.CorpusConfig,
            cancellationToken
        );
        await engines.UpdateAsync(
            e => e.Id == request.EngineId,
            u => u.Add(e => e.Corpora, corpus),
            cancellationToken: cancellationToken
        );
        return new(mapper.Map(request.EngineId, corpus));
    }

    private async Task<Corpus> MapAsync(
        string owner,
        string corpusId,
        TranslationCorpusConfigDto source,
        CancellationToken cancellationToken
    )
    {
        return new Corpus
        {
            Id = corpusId,
            Name = source.Name,
            SourceLanguage = source.SourceLanguage,
            TargetLanguage = source.TargetLanguage,
            SourceFiles = await MapFilesAsync(owner, source.SourceFiles, cancellationToken),
            TargetFiles = await MapFilesAsync(owner, source.TargetFiles, cancellationToken),
        };
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
    /// Add a corpus to a translation engine (obsolete - use parallel corpora instead)
    /// </summary>
    /// <remarks>
    /// ## Parameters
    /// * **name**: A name to help identify and distinguish the corpus from other corpora
    ///   * The name does not have to be unique since the corpus is uniquely identified by an auto-generated id
    /// * **`sourceLanguage`**: The source language code (See documentation on endpoint /translation/engines/ - "Create a new translation engine" for details on language codes).
    ///   * Normally, this is the same as the engine's `sourceLanguage`.  This may change for future engines as a means of transfer learning.
    /// * **`targetLanguage`**: The target language code (See documentation on endpoint /translation/engines/ - "Create a new translation engine" for details on language codes).
    /// * **`sourceFiles`**: The source files associated with the corpus
    ///   * **`fileId`**: The unique id referencing the uploaded file
    ///   * **`textId`**: The client-defined name to associate source and target files.
    ///     * If the text ids in the source files and target files match, they will be used to train the engine.
    ///     * If selected for pretranslation when building, all source files that have no target file, or lines of text in a source file that have missing or blank lines in the target file will be pretranslated.
    ///     * If a text id is used more than once in source files, the sources will be randomly and evenly mixed for training.
    ///     * For pretranslating, multiple sources with the same text id will be combined, but the first source will always take precedence (no random mixing).
    ///     * For Paratext projects, text id will be ignored - multiple Paratext source projects will always be mixed (as if they have the same text id).
    /// * **`targetFiles`**: The target files associated with the corpus
    ///   * Same as `sourceFiles`, except only a single instance of a text id or a single Paratext project is supported.  There is no mixing or combining of multiple targets.
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
    [Obsolete("This endpoint is obsolete. Use parallel corpora instead.")]
    [Authorize(Scopes.UpdateTranslationEngines)]
    [HttpPost("{id}/corpora")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<TranslationCorpusDto>> AddCorpusAsync(
        [NotNull] string id,
        [FromBody] TranslationCorpusConfigDto corpusConfig,
        [FromServices] IRequestHandler<AddCorpus, AddCorpusResponse> handler,
        CancellationToken cancellationToken
    )
    {
        AddCorpusResponse response = await handler.HandleAsync(new(Owner, id, corpusConfig), cancellationToken);
        return Created(response.Corpus.Url, response.Corpus);
    }
}

#pragma warning restore CS0612 // Type or member is obsolete
