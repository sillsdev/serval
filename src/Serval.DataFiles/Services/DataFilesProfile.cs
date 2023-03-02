namespace Serval.AspNetCore.Services;

public class DataFilesProfile : Profile
{
    private const string FilesUrl = "/files";

    public DataFilesProfile()
    {
        CreateMap<DataFile, DataFileDto>().ForMember(dto => dto.Url, o => o.MapFrom((e, _) => $"{FilesUrl}/{e.Id}"));
    }
}
