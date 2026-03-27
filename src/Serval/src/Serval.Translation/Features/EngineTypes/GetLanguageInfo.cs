namespace Serval.Translation.Features.EngineTypes;

public record LanguageInfoDto
{
    public required string EngineType { get; init; }
    public required bool IsNative { get; init; }
    public string? InternalCode { get; init; }
}

public record GetLanguageInfo(string EngineType, string Language) : IRequest<GetLanguageInfoResponse>;

public record GetLanguageInfoResponse(LanguageInfoDto? LanguageInfo = null);

public class GetLanguageInfoHandler(IEngineServiceFactory engineServiceFactory)
    : IRequestHandler<GetLanguageInfo, GetLanguageInfoResponse>
{
    public async Task<GetLanguageInfoResponse> HandleAsync(GetLanguageInfo request, CancellationToken cancellationToken)
    {
        if (engineServiceFactory.TryGetEngineService(request.EngineType, out ITranslationEngineService? engineService))
        {
            LanguageInfo languageInfo = await engineService.GetLanguageInfoAsync(request.Language, cancellationToken);
            return new(
                new LanguageInfoDto
                {
                    EngineType = engineService.Type.ToKebabCase(),
                    InternalCode = languageInfo.InternalCode,
                    IsNative = languageInfo.IsNative,
                }
            );
        }
        return new();
    }
}

public partial class TranslationEngineTypesController
{
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
        [FromServices] IRequestHandler<GetLanguageInfo, GetLanguageInfoResponse> handler,
        CancellationToken cancellationToken
    )
    {
        GetLanguageInfoResponse response = await handler.HandleAsync(new(engineType, language), cancellationToken);
        if (response.LanguageInfo is not null)
            return Ok(response.LanguageInfo);
        return NotFound();
    }
}
