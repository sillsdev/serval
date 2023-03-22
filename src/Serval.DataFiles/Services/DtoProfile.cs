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
    private readonly LinkGenerator _linkGenerator;

    public DataFileDtoMappingAction(LinkGenerator linkGenerator)
    {
        _linkGenerator = linkGenerator;
    }

    public void Process(DataFile source, DataFileDto destination, ResolutionContext context)
    {
        destination.Url = _linkGenerator.GetPathByAction(
            controller: "DataFiles",
            action: "Get",
            values: new { id = source.Id, version = "1" }
        )!;
    }
}
