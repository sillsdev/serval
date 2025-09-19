namespace Serval.Translation.Controllers;

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/translation/engine-types")]
[OpenApiTag("Translation Engines")]
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
    /// This endpoint exists primarily to support `nmt` model-training since `echo` and `smt-transfer` engines support all languages equally. Given a language tag, it provides the ISO 639-3 code that the tag maps to internally
    /// and whether it is supported in the NLLB 200 model without training.  This is useful for determining if a language is a good candidate for a source language.
    /// **Base Models available**
    /// * **NLLB-200**: This is the only base NMT translation model currently available.
    ///   * The languages supported by the base model can be found [here](https://github.com/facebookresearch/flores/blob/main/nllb_seed/README.md).
    /// Response format:
    /// * **`engineType`**: See above
    /// * **`isNative`**: Whether the base translation model supports this language without fine-tuning.
    /// * **`internalCode`**: The translation model's internal language code. See more details about how the language tag is mapped to an internal code [here](https://github.com/sillsdev/serval/wiki/FLORES%E2%80%90200-Language-Code-Resolution-for-NMT-Engine).
    /// </remarks>
    /// <param name="engineType">A valid engine type: nmt, echo, or smt-transfer</param>
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
