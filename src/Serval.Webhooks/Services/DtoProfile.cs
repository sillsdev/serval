namespace Serval.Webhooks.Services;

public class DtoProfile : Profile
{
    public DtoProfile()
    {
        CreateMap<Webhook, WebhookDto>()
            .ForMember(dest => dest.Url, o => o.MapFrom((src, _) => $"{Urls.Webhooks}/{src.Id}"));
    }
}
