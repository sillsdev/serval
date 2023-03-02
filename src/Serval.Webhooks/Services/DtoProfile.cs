namespace Serval.Webhooks.Services;

public class DtoProfile : Profile
{
    private const string WebhooksUrl = "/hooks";

    public DtoProfile()
    {
        CreateMap<Webhook, WebhookDto>().ForMember(dto => dto.Href, o => o.MapFrom((h, _) => $"{WebhooksUrl}/{h.Id}"));
    }
}
