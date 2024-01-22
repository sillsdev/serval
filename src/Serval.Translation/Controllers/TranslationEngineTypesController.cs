namespace Serval.Translation.Controllers;

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/translation/engine-types")]
[OpenApiTag("Translation Engines")]
public class TranslationController(IAuthorizationService authService, IEngineService engineService)
    : ServalControllerBase(authService)
{
    private readonly IEngineService _engineService = engineService;

    /// <summary>
    /// Get queue information for a given engine type
    /// </summary>
    /// <param name="engineType">A valid engine type: SmtTransfer, Nmt, or Echo</param>
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
            return Map(await _engineService.GetQueueAsync(engineType, cancellationToken: cancellationToken));
        }
        catch (InvalidOperationException ioe)
        {
            return BadRequest(ioe.Message);
        }
    }

    /// <summary>
    /// Get infromation regarding a language for a given engine type
    /// </summary>
    /// <remarks>
    /// SmtTransfer: supports all languages equally.  Language information is not needed.
    /// Nmt: Maps language codes to NLLB language codes according to [this](https://github.com/sillsdev/serval/wiki/Language-Tag-Resolution-for-NLLB%E2%80%90200).
    ///   Will say if the language is supported by the NLLB model natively and the resolved NLLB language code.
    /// </remarks>
    /// <param name="engineType">A valid engine type: SmtTransfer, Nmt, or Echo</param>
    /// <param name="language">A language code to be mapped </param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">Language information for the specified engine type</response>
    /// <response code="401">The client is not authenticated</response>
    /// <response code="403">The authenticated client cannot perform the operation</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details. </response>
    [Authorize(Scopes.ReadTranslationEngines)]
    [HttpGet("{engineType}/language-info/{language}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<LanguageInfoDto>> GetLanguageInfoAsync(
        [NotNull] string engineType,
        [NotNull] string language,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return Map(
                await _engineService.GetLanguageInfoAsync(engineType, language, cancellationToken: cancellationToken)
            );
        }
        catch (InvalidOperationException ioe)
        {
            return BadRequest(ioe.Message);
        }
    }

    private static QueueDto Map(Queue source) => new() { Size = source.Size, EngineType = source.EngineType };

    private static LanguageInfoDto Map(LanguageInfo source) =>
        new()
        {
            EngineType = source.EngineType,
            CommonLanguageName = source.CommonLanguageName,
            IsSupportedNatively = source.IsSupportedNatively,
            ISOLanguageCode = source.ISOLanguageCode
        };
}
