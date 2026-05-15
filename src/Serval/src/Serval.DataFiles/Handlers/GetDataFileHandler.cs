namespace Serval.DataFiles.Handlers;

public class GetDataFileHandler(IRepository<DataFile> dataFiles) : IRequestHandler<GetDataFile, GetDataFileResponse>
{
    public async Task<GetDataFileResponse> HandleAsync(GetDataFile request, CancellationToken cancellationToken)
    {
        DataFile? dataFile = await dataFiles.GetAsync(
            f => f.Id == request.DataFileId && f.Owner == request.Owner,
            cancellationToken
        );
        if (dataFile is null)
            return new(IsFound: false);
        return new(IsFound: true, new(dataFile.Id, dataFile.Name, dataFile.Filename, dataFile.Format));
    }
}
