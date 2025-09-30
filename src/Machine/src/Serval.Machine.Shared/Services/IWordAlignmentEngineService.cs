using Serval.WordAlignment.V1;

namespace Serval.Machine.Shared.Services;

public interface IWordAlignmentEngineService
{
    EngineType Type { get; }

    Task CreateAsync(
        string engineId,
        string? engineName,
        string sourceLanguage,
        string targetLanguage,
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
        string? buildOptions,
        IReadOnlyList<SIL.ServiceToolkit.Models.ParallelCorpus> corpora,
        CancellationToken cancellationToken = default
    );

    Task<string?> CancelBuildAsync(string engineId, CancellationToken cancellationToken = default);

    int GetQueueSize();
}
