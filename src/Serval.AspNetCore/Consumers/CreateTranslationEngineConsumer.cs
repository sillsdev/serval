using Serval.Engine.Translation.V1;

namespace Serval.AspNetCore.Consumers;

public class CreateTranslationEngineConsumer : IConsumer<CreateTranslationEngine>
{
    private readonly IRepository<TranslationEngine> _engines;
    private readonly GrpcClientFactory _grpcClientFactory;
    private readonly IMapper _mapper;

    public CreateTranslationEngineConsumer(
        IRepository<TranslationEngine> engines,
        GrpcClientFactory grpcClientFactory,
        IMapper mapper
    )
    {
        _engines = engines;
        _grpcClientFactory = grpcClientFactory;
        _mapper = mapper;
    }

    public async Task Consume(ConsumeContext<CreateTranslationEngine> context)
    {
        var engine = new TranslationEngine
        {
            Name = context.Message.Name,
            SourceLanguageTag = context.Message.SourceLanguageTag,
            TargetLanguageTag = context.Message.TargetLanguageTag,
            Type = context.Message.Type,
            Owner = context.Message.Owner
        };
        await _engines.InsertAsync(engine);
        var client = _grpcClientFactory.CreateClient<TranslationService.TranslationServiceClient>(engine.Type);
        await client.CreateAsync(new CreateRequest { EngineType = engine.Type, EngineId = engine.Id });
        await context.RespondAsync(_mapper.Map<TranslationEngineResult>(engine));
    }
}
