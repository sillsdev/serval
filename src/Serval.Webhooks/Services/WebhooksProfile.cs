namespace Serval.Webhooks.Services;

public class WebhooksProfile : Profile
{
    private const string WebhooksUrl = "/hooks";

    public WebhooksProfile()
    {
        CreateMap<Webhook, WebhookDto>().ForMember(dto => dto.Url, o => o.MapFrom((h, _) => $"{WebhooksUrl}/{h.Id}"));
    }
}
