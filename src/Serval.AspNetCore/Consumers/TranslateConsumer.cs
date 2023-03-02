using Serval.Engine.Translation.V1;

namespace Serval.AspNetCore.Consumers;

public class TranslateConsumer : IConsumer<Translate>
{
    private readonly IRepository<TranslationEngine> _engines;
    private readonly GrpcClientFactory _grpcClientFactory;
    private readonly IMapper _mapper;

    public TranslateConsumer(
        IRepository<TranslationEngine> engines,
        GrpcClientFactory grpcClientFactory,
        IMapper mapper
    )
    {
        _engines = engines;
        _grpcClientFactory = grpcClientFactory;
        _mapper = mapper;
    }

    public async Task Consume(ConsumeContext<Translate> context)
    {
        TranslationEngine? engine = await _engines.GetAsync(context.Message.EngineId);
        if (engine == null)
        {
            await context.RespondAsync(new TranslationEngineNotFound { EngineId = context.Message.EngineId });
            return;
        }

        if (engine.Owner != context.Message.Owner)
        {
            await context.RespondAsync(
                new NotAuthorized { Id = context.Message.EngineId, Owner = context.Message.Owner }
            );
            return;
        }

        var client = _grpcClientFactory.CreateClient<TranslationService.TranslationServiceClient>(engine.Type);
        TranslateResponse response = await client.TranslateAsync(
            new TranslateRequest
            {
                EngineType = engine.Type,
                EngineId = engine.Id,
                N = context.Message.N,
                Segment = context.Message.Segment
            }
        );
        await context.RespondAsync(
            new TranslateResult
            {
                Results = response.Results.Select(r => _mapper.Map<Contracts.TranslationResult>(r)).ToList()
            }
        );
    }
}
