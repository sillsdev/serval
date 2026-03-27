namespace Serval.DataFiles.Handlers;

public class GetDataFileHandler(IDataFileService dataFileService) : IRequestHandler<GetDataFile, GetDataFileResponse>
{
    public async Task<GetDataFileResponse> HandleAsync(GetDataFile request, CancellationToken cancellationToken)
    {
        try
        {
            DataFile dataFile = await dataFileService.GetAsync(request.DataFileId, request.Owner, cancellationToken);
            return new(IsFound: true, new(dataFile.Id, dataFile.Name, dataFile.Filename, dataFile.Format));
        }
        catch (EntityNotFoundException)
        {
            return new(IsFound: false);
        }
    }
}
