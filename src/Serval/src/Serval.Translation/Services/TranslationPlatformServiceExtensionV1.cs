using Google.Protobuf.WellKnownTypes;
using Serval.Translation.V1;

namespace Serval.Translation.Services;

public class TranslationPlatformServiceExtensionV1(IRepository<TranslationEngine> engines)
    : TranslationPlatformExtensionsApi.TranslationPlatformExtensionsApiBase
{
    private readonly IRepository<TranslationEngine> _engines = engines;
    protected static readonly Empty Empty = new();

    public override async Task<Empty> IncrementTranslationEngineCorpusSize(
        IncrementTranslationEngineCorpusSizeRequest request,
        ServerCallContext context
    )
    {
        await _engines.UpdateAsync(
            request.EngineId,
            u => u.Inc(e => e.CorpusSize, request.Count),
            cancellationToken: context.CancellationToken
        );
        return Empty;
    }
}
