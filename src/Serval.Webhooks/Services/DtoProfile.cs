namespace Serval.Webhooks.Services;

public class DtoProfile : Profile
{
    public DtoProfile()
    {
        CreateMap<Webhook, WebhookDto>().AfterMap<WebhookDtoMappingAction>();
    }
}

public class WebhookDtoMappingAction : IMappingAction<Webhook, WebhookDto>
{
    private readonly LinkGenerator _linkGenerator;

    public WebhookDtoMappingAction(LinkGenerator linkGenerator)
    {
        _linkGenerator = linkGenerator;
    }

    public void Process(Webhook source, WebhookDto destination, ResolutionContext context)
    {
        destination.Url = _linkGenerator.GetPathByAction(
            controller: "Webhooks",
            action: "Get",
            values: new { id = source.Id, version = "1" }
        )!;
    }
}
