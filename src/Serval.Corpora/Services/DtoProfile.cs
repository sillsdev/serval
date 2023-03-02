namespace Serval.AspNetCore.Services;

public class DtoProfile : Profile
{
    private const string CorporaUrl = "/corpora";

    public DtoProfile()
    {
        CreateMap<Corpus, CorpusDto>().ForMember(dto => dto.Href, o => o.MapFrom((c, _) => $"{CorporaUrl}/{c.Id}"));
        CreateMap<DataFile, DataFileDto>()
            .ForMember(
                dto => dto.Href,
                o => o.MapFrom((f, _, _, ctxt) => $"{CorporaUrl}/{ctxt.Items["CorpusId"]}/files/{f.Id}")
            );
    }
}
