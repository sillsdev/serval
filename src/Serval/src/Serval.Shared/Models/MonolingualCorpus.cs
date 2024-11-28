namespace Serval.Shared.Models;

public record MonolingualCorpus
{
    public required string Id { get; set; }
    public string? Name { get; set; }
    public required string Language { get; set; }
    public required IReadOnlyList<CorpusFile> Files { get; set; }

    public async Task PopulateFilenamesAsync(
        IRequestClient<GetDataFile> getDataFileClient,
        string owner,
        CancellationToken cancellationToken
    )
    {
        await Task.WhenAll(
            Files.Select(file => file.PopulateFilenameAsync(getDataFileClient, owner, cancellationToken))
        );
    }
}
