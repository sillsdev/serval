using Serval.Engine.V1;
using Serval.Translation.V1;

namespace Serval.Machine.Shared.Services;

public class ServalPlatformService(
    EnginePlatformApi.EnginePlatformApiClient client,
    IMessageOutboxService outboxService
) : IPlatformService
{
    private readonly EnginePlatformApi.EnginePlatformApiClient _client = client;
    private readonly IMessageOutboxService _outboxService = outboxService;

    public async Task JobStartedAsync(string buildId, CancellationToken cancellationToken = default)
    {
        await _outboxService.EnqueueMessageAsync(
            ServalPlatformOutboxConstants.OutboxId,
            ServalPlatformOutboxConstants.BuildStarted,
            buildId,
            JsonSerializer.Serialize(new JobStartedRequest { JobId = buildId }),
            cancellationToken: cancellationToken
        );
    }

    public async Task JobCompletedAsync(
        string buildId,
        int trainSize,
        double confidence,
        CancellationToken cancellationToken = default
    )
    {
        await _outboxService.EnqueueMessageAsync(
            ServalPlatformOutboxConstants.OutboxId,
            ServalPlatformOutboxConstants.BuildCompleted,
            buildId,
            JsonSerializer.Serialize(
                new JobCompletedRequest
                {
                    JobId = buildId,
                    CorpusSize = trainSize,
                    Confidence = confidence
                }
            ),
            cancellationToken: cancellationToken
        );
    }

    public async Task JobCanceledAsync(string buildId, CancellationToken cancellationToken = default)
    {
        await _outboxService.EnqueueMessageAsync(
            ServalPlatformOutboxConstants.OutboxId,
            ServalPlatformOutboxConstants.BuildCanceled,
            buildId,
            JsonSerializer.Serialize(new JobCanceledRequest { JobId = buildId }),
            cancellationToken: cancellationToken
        );
    }

    public async Task JobFaultedAsync(string buildId, string message, CancellationToken cancellationToken = default)
    {
        await _outboxService.EnqueueMessageAsync(
            ServalPlatformOutboxConstants.OutboxId,
            ServalPlatformOutboxConstants.BuildFaulted,
            buildId,
            JsonSerializer.Serialize(new JobFaultedRequest { JobId = buildId, Message = message }),
            cancellationToken: cancellationToken
        );
    }

    public async Task JobRestartingAsync(string buildId, CancellationToken cancellationToken = default)
    {
        await _outboxService.EnqueueMessageAsync(
            ServalPlatformOutboxConstants.OutboxId,
            ServalPlatformOutboxConstants.JobRestarting,
            buildId,
            JsonSerializer.Serialize(new JobRestartingRequest { JobId = buildId }),
            cancellationToken: cancellationToken
        );
    }

    public async Task UpdateJobStatusAsync(
        string buildId,
        ProgressStatus progressStatus,
        int? queueDepth = null,
        CancellationToken cancellationToken = default
    )
    {
        var request = new UpdateJobStatusRequest { JobId = buildId, Step = progressStatus.Step };
        if (progressStatus.PercentCompleted.HasValue)
            request.PercentCompleted = progressStatus.PercentCompleted.Value;
        if (progressStatus.Message is not null)
            request.Message = progressStatus.Message;
        if (queueDepth is not null)
            request.QueueDepth = queueDepth.Value;

        // just try to send it - if it fails, it fails.
        await _client.UpdateJobStatusAsync(request, cancellationToken: cancellationToken);
    }

    public async Task UpdateJobStatusAsync(string buildId, int step, CancellationToken cancellationToken = default)
    {
        // just try to send it - if it fails, it fails.
        await _client.UpdateJobStatusAsync(
            new UpdateJobStatusRequest { JobId = buildId, Step = step },
            cancellationToken: cancellationToken
        );
    }

    public async Task InsertPretranslationsAsync(
        string engineId,
        Stream pretranslationsStream,
        CancellationToken cancellationToken = default
    )
    {
        await _outboxService.EnqueueMessageAsync(
            ServalPlatformOutboxConstants.OutboxId,
            ServalPlatformOutboxConstants.InsertPretranslations,
            engineId,
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
            ServalPlatformOutboxConstants.OutboxId,
            ServalPlatformOutboxConstants.IncrementTranslationEngineCorpusSize,
            engineId,
            JsonSerializer.Serialize(
                new IncrementTranslationEngineCorpusSizeRequest { EngineId = engineId, Count = count }
            ),
            cancellationToken: cancellationToken
        );
    }
}
