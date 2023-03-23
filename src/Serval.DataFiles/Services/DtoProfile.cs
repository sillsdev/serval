namespace Serval.DataFiles.Services;

public class DtoProfile : Profile
{
    public DtoProfile()
    {
        CreateMap<DataFile, DataFileDto>().AfterMap<DataFileDtoMappingAction>();
    }
}

public class DataFileDtoMappingAction : IMappingAction<DataFile, DataFileDto>
{
    private readonly IUrlService _urlService;

    public DataFileDtoMappingAction(IUrlService urlService)
    {
        _urlService = urlService;
    }

    public void Process(DataFile source, DataFileDto destination, ResolutionContext context)
    {
        destination.Url = _urlService.GetUrl("GetDataFile", new { id = source.Id });
    }
}
