namespace Serval.Translation.Features.Engines;

public record TranslationEngineConfigDto
{
    /// <summary>
    /// The translation engine name.
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

public record CreateEngine(string Owner, TranslationEngineConfigDto EngineConfig) : IRequest<CreateEngineResponse>;

public record CreateEngineResponse(TranslationEngineDto Engine);

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
                    Corpora = [],
                    IsModelPersisted = request.EngineConfig.IsModelPersisted,
                    DateCreated = DateTime.UtcNow,
                };
                await engines.InsertAsync(engine, ct);

                await engineServiceFactory
                    .GetEngineService(engine.Type)
                    .CreateAsync(
                        engine.Id,
                        engine.SourceLanguage,
                        engine.TargetLanguage,
                        engine.Name,
                        engine.IsModelPersisted ?? false,
                        ct
                    );
                return new CreateEngineResponse(mapper.Map(engine));
            },
            cancellationToken
        );
    }
}

public partial class TranslationEnginesController
{
    /// <summary>
    /// Create a new translation engine
    /// </summary>
    /// <remarks>
    /// ## Parameters
    /// * **`name`**: (optional) A name to help identify and distinguish the translation engine.
    ///   * Recommendation: Create a multi-part name to distinguish between projects, uses, etc.
    ///   * The name does not have to be unique, as the engine is uniquely identified by the auto-generated id
    /// * **`sourceLanguage`**: The source language code (a valid [IETF language tag](https://en.wikipedia.org/wiki/IETF_language_tag) is recommended)
    /// * **`targetLanguage`**: The target language code (a valid IETF language tag is recommended)
    /// * **`type`**: **`smt-transfer`** or **`nmt`** or **`echo`**
    /// * **`isModelPersisted`**: (optional) - see below
    /// ### smt-transfer
    /// The Statistical Machine Translation Transfer Learning engine is primarily used for translation suggestions. Typical endpoints: translate, get-word-graph, train-segment
    /// * **`isModelPersisted`**: (default to `true`) All models are persistent and can be updated with train-segment.  False is not supported.
    /// ### nmt
    /// The Neural Machine Translation engine is primarily used for pretranslations.  It is fine-tuned from Meta's NLLB-200. Valid IETF language tags provided to Serval will be converted to [NLLB-200 codes](https://github.com/facebookresearch/flores/tree/main/flores200#languages-in-flores-200).  See more about language tag resolution [here](https://github.com/sillsdev/serval/wiki/FLORES%E2%80%90200-Language-Code-Resolution-for-NMT-Engine).
    /// * **`isModelPersisted`**: (default to `false`) Whether the model can be downloaded by the client after it has been successfully built.
    ///
    /// If you use a language among NLLB's supported languages, Serval will utilize everything the NLLB-200 model already knows about that language when translating. If the language you are working with is not among NLLB's supported languages, the language code will have no effect.
    ///
    /// Typical endpoints: pretranslate
    /// ### echo
    /// The echo engine has full coverage of all nmt and smt-transfer endpoints. Endpoints like create and build return empty responses. Endpoints like translate and get-word-graph echo the sent content back to the user in a format that mocks nmt or smt-transfer. For example, translating a segment "test" with the echo engine would yield a translation response with translation "test". This engine is useful for debugging and testing purposes.
    /// ## Sample request:
    ///
    ///     {
    ///       "name": "myTeam:myProject:myEngine",
    ///       "sourceLanguage": "el",
    ///       "targetLanguage": "en",
    ///       "type": "nmt"
    ///       "isModelPersisted": true
    ///     }
    ///
    /// </remarks>
    /// <param name="engineConfig">The translation engine configuration (see above)</param>
    /// <param name="cancellationToken"></param>
    /// <response code="201">The new translation engine</response>
    /// <response code="400">Bad request. Is the engine type correct?</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the translation engine.</response>
    /// <response code="404">The engine does not exist.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.CreateTranslationEngines)]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<TranslationEngineDto>> CreateAsync(
        [FromBody] TranslationEngineConfigDto engineConfig,
        [FromServices] IRequestHandler<CreateEngine, CreateEngineResponse> handler,
        CancellationToken cancellationToken
    )
    {
        CreateEngineResponse response = await handler.HandleAsync(new(Owner, engineConfig), cancellationToken);

        return Created(response.Engine.Url, response.Engine);
    }
}
