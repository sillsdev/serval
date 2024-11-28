namespace Serval.Shared.Models;

public record CorpusFile
{
    public required string Id { get; set; }
    public required FileFormat Format { get; set; }
    public required string TextId { get; set; }

    private string? _filename;

    public async Task PopulateFilenameAsync(
        IRequestClient<GetDataFile> getDataFileClient,
        string owner,
        CancellationToken cancellationToken
    )
    {
        Response<DataFileResult, DataFileNotFound> response = await getDataFileClient.GetResponse<
            DataFileResult,
            DataFileNotFound
        >(new GetDataFile { DataFileId = Id, Owner = owner }, cancellationToken);
        if (response.Is(out Response<DataFileResult>? result))
        {
            _filename = result.Message.Filename;
        }
        else
        {
            throw new InvalidOperationException($"The data file {Id} cannot be found.");
        }
    }

    public void SetFilename(string filename)
    {
        _filename = filename;
    }

    public string GetFilename()
    {
        return _filename
            ?? throw new InvalidOperationException(
                "The filename has not been populated.  It is not stored in the database."
            );
    }
}
