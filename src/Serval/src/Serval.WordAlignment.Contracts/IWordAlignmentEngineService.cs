namespace Serval.WordAlignment.Contracts;

public interface IWordAlignmentEngineService
{
    Task CreateAsync(
        string engineId,
        string sourceLanguage,
        string targetLanguage,
        string? engineName = null,
        CancellationToken cancellationToken = default
    );
    Task DeleteAsync(string engineId, CancellationToken cancellationToken = default);

    Task<WordAlignmentResult> AlignAsync(
        string engineId,
        string sourceSegment,
        string targetSegment,
        CancellationToken cancellationToken = default
    );

    Task StartBuildAsync(
        string engineId,
        string buildId,
        IReadOnlyList<FilteredParallelCorpus> corpora,
        string? options = null,
        CancellationToken cancellationToken = default
    );

    Task<string?> CancelBuildAsync(string engineId, CancellationToken cancellationToken = default);

    Task<int> GetQueueSizeAsync(CancellationToken cancellationToken = default);
}
