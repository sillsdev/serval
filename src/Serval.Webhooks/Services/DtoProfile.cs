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
    private readonly IUrlService _urlService;

    public WebhookDtoMappingAction(IUrlService urlService)
    {
        _urlService = urlService;
    }

    public void Process(Webhook source, WebhookDto destination, ResolutionContext context)
    {
        destination.Url = _urlService.GetUrl("GetWebhook", new { id = source.Id });
    }
}
