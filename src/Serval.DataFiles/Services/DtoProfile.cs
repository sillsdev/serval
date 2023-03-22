namespace Serval.DataFiles.Services;

public class DtoProfile : Profile
{
    public DtoProfile()
    {
        CreateMap<DataFile, DataFileDto>()
            .ForMember(dest => dest.Url, o => o.MapFrom((src, _) => $"{Urls.Files}/{src.Id}"));
    }
}
