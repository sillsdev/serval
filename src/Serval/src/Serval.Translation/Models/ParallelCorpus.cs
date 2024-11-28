namespace Serval.Translation.Models;

public record ParallelCorpus
{
    public required string Id { get; set; }
    public IReadOnlyList<MonolingualCorpus> SourceCorpora { get; set; } = new List<MonolingualCorpus>();
    public IReadOnlyList<MonolingualCorpus> TargetCorpora { get; set; } = new List<MonolingualCorpus>();

    public async Task PopulateFilenamesAsync(
        IRequestClient<GetDataFile> getDataFileClient,
        string owner,
        CancellationToken cancellationToken
    )
    {
        await Task.WhenAll(
            SourceCorpora.Select(corpus => corpus.PopulateFilenamesAsync(getDataFileClient, owner, cancellationToken))
        );
        await Task.WhenAll(
            TargetCorpora.Select(corpus => corpus.PopulateFilenamesAsync(getDataFileClient, owner, cancellationToken))
        );
    }
}
