namespace Serval.WordAlignment.Features.Engines;

public record WordAlignmentEngineConfigDto
{
    /// <summary>
    /// The word alignment engine name.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// The source language tag.
    /// </summary>
    public required string SourceLanguage { get; init; }

    /// <summary>
    /// The target language tag.
    /// </summary>
    public required string TargetLanguage { get; init; }

    /// <summary>
    /// The translation engine type.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// The model is saved when built and can be retrieved.
    /// </summary>
    public bool? IsModelPersisted { get; init; }
}

public record CreateEngine(string Owner, WordAlignmentEngineConfigDto EngineConfig) : IRequest<CreateEngineResponse>;

public record CreateEngineResponse(WordAlignmentEngineDto Engine);

public class CreateEngineHandler(
    IDataAccessContext dataAccessContext,
    IRepository<Engine> engines,
    IEngineServiceFactory engineServiceFactory,
    DtoMapper mapper
) : IRequestHandler<CreateEngine, CreateEngineResponse>
{
    public async Task<CreateEngineResponse> HandleAsync(
        CreateEngine request,
        CancellationToken cancellationToken = default
    )
    {
        if (!engineServiceFactory.EngineTypeExists(request.EngineConfig.Type))
            throw new InvalidOperationException($"'{request.EngineConfig.Type}' is an invalid engine type.");

        return await dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                Engine engine = new()
                {
                    Name = request.EngineConfig.Name,
                    SourceLanguage = request.EngineConfig.SourceLanguage,
                    TargetLanguage = request.EngineConfig.TargetLanguage,
                    Type = request.EngineConfig.Type.ToPascalCase(),
                    Owner = request.Owner,
                    ParallelCorpora = [],
                    DateCreated = DateTime.UtcNow,
                };
                await engines.InsertAsync(engine, ct);

                await engineServiceFactory
                    .GetEngineService(engine.Type)
                    .CreateAsync(engine.Id, engine.SourceLanguage, engine.TargetLanguage, engine.Name, ct);
                return new CreateEngineResponse(mapper.Map(engine));
            },
            cancellationToken
        );
    }
}

public partial class WordAlignmentEnginesController
{
    /// <summary>
    /// Create a new word alignment engine
    /// </summary>
    /// <remarks>
    /// ## Parameters
    /// * **`name`**: (optional) A name to help identify and distinguish the file.
    ///   * Recommendation: Create a multi-part name to distinguish between projects, uses, etc.
    ///   * The name does not have to be unique, as the engine is uniquely identified by the auto-generated id
    /// * **`sourceLanguage`**: The source language code (a valid [IETF language tag](https://en.wikipedia.org/wiki/IETF_language_tag) is recommended)
    /// * **`targetLanguage`**: The target language code (a valid IETF language tag is recommended)
    /// * **type**: **`statistical`** or **`echo-word-alignment`**
    /// ### statistical
    /// The Statistical engine is based off of the [Thot library](https://github.com/sillsdev/thot) and contains IBM-1, IBM-2, IBM-3, IBM-4, FastAlign and HMM algorithms.
    /// ### echo-word-alignment
    /// The echo-word-alignment engine has full coverage of all endpoints. Endpoints like create and build return empty responses.
    /// Endpoints like align echo the sent content back to the user in the proper format. This engine is useful for debugging and testing purposes.
    /// ## Sample request:
    ///
    ///     {
    ///       "name": "myTeam:myProject:myEngine",
    ///       "sourceLanguage": "el",
    ///       "targetLanguage": "en",
    ///       "type": "statistical"
    ///     }
    ///
    /// </remarks>
    /// <param name="engineConfig">The engine configuration (see above)</param>
    /// <param name="cancellationToken"></param>
    /// <response code="201">The new engine</response>
    /// <response code="400">Bad request. Is the engine type correct?</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the engine.</response>
    /// <response code="404">The engine does not exist.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.CreateWordAlignmentEngines)]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<WordAlignmentEngineDto>> CreateAsync(
        [FromBody] WordAlignmentEngineConfigDto engineConfig,
        [FromServices] IRequestHandler<CreateEngine, CreateEngineResponse> handler,
        CancellationToken cancellationToken
    )
    {
        CreateEngineResponse response = await handler.HandleAsync(new(Owner, engineConfig), cancellationToken);

        return Created(response.Engine.Url, response.Engine);
    }
}
