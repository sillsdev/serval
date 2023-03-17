namespace Serval.DataFiles.Services;

public class DtoProfile : Profile
{
    private const string FilesUrl = "/files";

    public DtoProfile()
    {
        CreateMap<DataFile, DataFileDto>()
            .ForMember(dest => dest.Url, o => o.MapFrom((src, _) => $"{FilesUrl}/{src.Id}"));
    }
}
