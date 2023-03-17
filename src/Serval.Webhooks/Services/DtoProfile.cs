namespace Serval.Webhooks.Services;

public class DtoProfile : Profile
{
    private const string WebhooksUrl = "/hooks";

    public DtoProfile()
    {
        CreateMap<Webhook, WebhookDto>()
            .ForMember(dest => dest.Url, o => o.MapFrom((src, _) => $"{WebhooksUrl}/{src.Id}"));
    }
}
