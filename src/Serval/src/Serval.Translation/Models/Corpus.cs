namespace Serval.Translation.Models;

public record Corpus
{
    public string Id { get; set; } = "";
    public string? Name { get; set; }
    public required string SourceLanguage { get; set; }
    public required string TargetLanguage { get; set; }
    public required IReadOnlyList<CorpusFile> SourceFiles { get; set; }
    public required IReadOnlyList<CorpusFile> TargetFiles { get; set; }

    public async Task PopulateFilenamesAsync(
        IRequestClient<GetDataFile> getDataFileClient,
        string owner,
        CancellationToken cancellationToken
    )
    {
        await Task.WhenAll(
            SourceFiles.Select(file => file.PopulateFilenameAsync(getDataFileClient, owner, cancellationToken))
        );
        await Task.WhenAll(
            TargetFiles.Select(file => file.PopulateFilenameAsync(getDataFileClient, owner, cancellationToken))
        );
    }
}
