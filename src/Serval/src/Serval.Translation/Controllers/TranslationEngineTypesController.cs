namespace Serval.Translation.Controllers;

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/translation/engine-types")]
[OpenApiTag("Translation")]
public class TranslationEngineTypesController(IAuthorizationService authService, IEngineService engineService)
    : ServalControllerBase(authService)
{
    private readonly IEngineService _engineService = engineService;

    /// <summary>
    /// Get queue information for a given engine type
    /// </summary>
    /// <param name="engineType">A valid engine type: smt-transfer, nmt, or echo</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">Queue information for the specified engine type</response>
    /// <response code="401">The client is not authenticated</response>
    /// <response code="403">The authenticated client cannot perform the operation</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details. </response>
    [Authorize(Scopes.ReadTranslationEngines)]
    [HttpGet("{engineType}/queues")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<QueueDto>> GetQueueAsync(
        [NotNull] string engineType,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return Map(
                await _engineService.GetQueueAsync(engineType.ToPascalCase(), cancellationToken: cancellationToken)
            );
        }
        catch (InvalidOperationException ioe)
        {
            return BadRequest(ioe.Message);
        }
    }

    /// <summary>
    /// Get information regarding a language for a given engine type
    /// </summary>
    /// <remarks>
    /// This endpoint is to support Nmt models.  It specifies the ISO 639-3 code that the language maps to
    /// and whether it is supported in the NLLB 200 model without training.  This is useful for determining if a
    /// language is an appropriate candidate for a source language or if two languages can be translated between
    /// **Base Models available**
    /// * **NLLB-200**: This is the only current base translation model available.
    ///   * The languages included in the base model are [here](https://github.com/facebookresearch/flores/blob/main/nllb_seed/README.md)
    /// without training.
    /// Response format:
    /// * **EngineType**: See above
    /// * **IsNative**: Whether the base translation model supports this language without fine-tuning.
    /// * **InternalCode**: The translation models language code that the language maps to according to [these rules](https://github.com/sillsdev/serval/wiki/FLORES%E2%80%90200-Language-Code-Resolution-for-NMT-Engine).
    /// </remarks>
    /// <param name="engineType">A valid engine type: nmt or echo</param>
    /// <param name="language">The language to retrieve information on.</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">Language information for the specified engine type</response>
    /// <response code="401">The client is not authenticated</response>
    /// <response code="403">The authenticated client cannot perform the operation</response>
    /// <response code="405">The method is not supported</response>
    [Authorize(Scopes.ReadTranslationEngines)]
    [HttpGet("{engineType}/languages/{language}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status405MethodNotAllowed)]
    public async Task<ActionResult<LanguageInfoDto>> GetLanguageInfoAsync(
        [NotNull] string engineType,
        [NotNull] string language,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return Map(
                await _engineService.GetLanguageInfoAsync(
                    engineType: engineType.ToPascalCase(),
                    language: language,
                    cancellationToken: cancellationToken
                )
            );
        }
        catch (InvalidOperationException ioe)
        {
            return BadRequest(ioe.Message);
        }
    }

    private static QueueDto Map(Queue source) =>
        new() { Size = source.Size, EngineType = source.EngineType.ToKebabCase() };

    private static LanguageInfoDto Map(LanguageInfo source) =>
        new()
        {
            EngineType = source.EngineType.ToKebabCase(),
            IsNative = source.IsNative,
            InternalCode = source.InternalCode
        };
}
