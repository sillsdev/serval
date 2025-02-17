using Serval.Translation.V1;

namespace Serval.Machine.Shared.Services;

public class ServalTranslationPlatformService(
    TranslationPlatformApi.TranslationPlatformApiClient client,
    IMessageOutboxService outboxService
) : IPlatformService
{
    EngineGroup IPlatformService.EngineGroup => EngineGroup.Translation;
    private readonly TranslationPlatformApi.TranslationPlatformApiClient _client = client;
    private readonly IMessageOutboxService _outboxService = outboxService;

    public async Task BuildStartedAsync(string buildId, CancellationToken cancellationToken = default)
    {
        await _outboxService.EnqueueMessageAsync(
            ServalTranslationPlatformOutboxConstants.OutboxId,
            ServalTranslationPlatformOutboxConstants.BuildStarted,
            buildId,
            new BuildStartedRequest { BuildId = buildId },
            cancellationToken: cancellationToken
        );
    }

    public async Task BuildCompletedAsync(
        string buildId,
        int trainSize,
        double confidence,
        CancellationToken cancellationToken = default
    )
    {
        await _outboxService.EnqueueMessageAsync(
            ServalTranslationPlatformOutboxConstants.OutboxId,
            ServalTranslationPlatformOutboxConstants.BuildCompleted,
            buildId,
            new BuildCompletedRequest
            {
                BuildId = buildId,
                CorpusSize = trainSize,
                Confidence = confidence
            },
            cancellationToken: cancellationToken
        );
    }

    public async Task BuildCanceledAsync(string buildId, CancellationToken cancellationToken = default)
    {
        await _outboxService.EnqueueMessageAsync(
            ServalTranslationPlatformOutboxConstants.OutboxId,
            ServalTranslationPlatformOutboxConstants.BuildCanceled,
            buildId,
            new BuildCanceledRequest { BuildId = buildId },
            cancellationToken: cancellationToken
        );
    }

    public async Task BuildFaultedAsync(string buildId, string message, CancellationToken cancellationToken = default)
    {
        await _outboxService.EnqueueMessageAsync(
            ServalTranslationPlatformOutboxConstants.OutboxId,
            ServalTranslationPlatformOutboxConstants.BuildFaulted,
            buildId,
            new BuildFaultedRequest { BuildId = buildId, Message = message },
            cancellationToken: cancellationToken
        );
    }

    public async Task BuildRestartingAsync(string buildId, CancellationToken cancellationToken = default)
    {
        await _outboxService.EnqueueMessageAsync(
            ServalTranslationPlatformOutboxConstants.OutboxId,
            ServalTranslationPlatformOutboxConstants.BuildRestarting,
            buildId,
            new BuildRestartingRequest { BuildId = buildId },
            cancellationToken: cancellationToken
        );
    }

    public async Task UpdateBuildStatusAsync(
        string buildId,
        ProgressStatus progressStatus,
        int? queueDepth = null,
        CancellationToken cancellationToken = default
    )
    {
        var request = new UpdateBuildStatusRequest { BuildId = buildId, Step = progressStatus.Step };
        if (progressStatus.PercentCompleted.HasValue)
            request.PercentCompleted = progressStatus.PercentCompleted.Value;
        if (progressStatus.Message is not null)
            request.Message = progressStatus.Message;
        if (queueDepth is not null)
            request.QueueDepth = queueDepth.Value;

        // just try to send it - if it fails, it fails.
        await _client.UpdateBuildStatusAsync(request, cancellationToken: cancellationToken);
    }

    public async Task UpdateBuildStatusAsync(string buildId, int step, CancellationToken cancellationToken = default)
    {
        // just try to send it - if it fails, it fails.
        await _client.UpdateBuildStatusAsync(
            new UpdateBuildStatusRequest { BuildId = buildId, Step = step },
            cancellationToken: cancellationToken
        );
    }

    public async Task InsertInferenceResultsAsync(
        string engineId,
        Stream pretranslationsStream,
        CancellationToken cancellationToken = default
    )
    {
        await _outboxService.EnqueueMessageStreamAsync(
            ServalTranslationPlatformOutboxConstants.OutboxId,
            ServalTranslationPlatformOutboxConstants.InsertPretranslations,
            engineId,
            pretranslationsStream,
            cancellationToken: cancellationToken
        );
    }

    public async Task IncrementTrainSizeAsync(
        string engineId,
        int count = 1,
        CancellationToken cancellationToken = default
    )
    {
        await _outboxService.EnqueueMessageAsync(
            ServalTranslationPlatformOutboxConstants.OutboxId,
            ServalTranslationPlatformOutboxConstants.IncrementTrainEngineCorpusSize,
            engineId,
            new IncrementEngineCorpusSizeRequest { EngineId = engineId, Count = count },
            cancellationToken: cancellationToken
        );
    }

    public async Task UpdateBuildExecutionDataAsync(
        string engineId,
        string buildId,
        IReadOnlyDictionary<string, string> executionData,
        CancellationToken cancellationToken = default
    )
    {
        var request = new UpdateBuildExecutionDataRequest { EngineId = engineId, BuildId = buildId };
        request.ExecutionData.Add((IDictionary<string, string>)executionData);
        await _outboxService.EnqueueMessageAsync(
            ServalTranslationPlatformOutboxConstants.OutboxId,
            ServalTranslationPlatformOutboxConstants.UpdateBuildExecutionData,
            engineId,
            request,
            cancellationToken: cancellationToken
        );
    }
}
