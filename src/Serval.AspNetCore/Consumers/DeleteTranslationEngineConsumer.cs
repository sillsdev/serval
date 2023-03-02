using Serval.Engine.Translation.V1;

namespace Serval.AspNetCore.Consumers;

public class DeleteTranslationEngineConsumer : IConsumer<DeleteTranslationEngine>
{
    private readonly IRepository<TranslationEngine> _engines;
    private readonly IRepository<Build> _builds;
    private readonly GrpcClientFactory _grpcClientFactory;

    public DeleteTranslationEngineConsumer(
        IRepository<TranslationEngine> engines,
        IRepository<Build> builds,
        GrpcClientFactory grpcClientFactory
    )
    {
        _engines = engines;
        _builds = builds;
        _grpcClientFactory = grpcClientFactory;
    }

    public async Task Consume(ConsumeContext<DeleteTranslationEngine> context)
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

        engine = await _engines.DeleteAsync(context.Message.EngineId);
        if (engine == null)
        {
            await context.RespondAsync(new TranslationEngineNotFound { EngineId = context.Message.EngineId });
            return;
        }
        await _builds.DeleteAllAsync(b => b.ParentRef == context.Message.EngineId);

        var client = _grpcClientFactory.CreateClient<TranslationService.TranslationServiceClient>(engine.Type);
        await client.DeleteAsync(new DeleteRequest { EngineType = engine.Type, EngineId = engine.Id });

        await context.RespondAsync(new TranslationEngineDeleted { EngineId = context.Message.EngineId });
    }
}
