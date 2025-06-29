using Serval.WordAlignment.V1;
using Phase = Serval.WordAlignment.V1.Phase;

namespace Serval.Machine.Shared.Services;

public class ServalWordAlignmentPlatformService(
    WordAlignmentPlatformApi.WordAlignmentPlatformApiClient client,
    IOutboxService outboxService
) : IPlatformService
{
    EngineGroup IPlatformService.EngineGroup => EngineGroup.WordAlignment;
    private readonly WordAlignmentPlatformApi.WordAlignmentPlatformApiClient _client = client;
    private readonly IOutboxService _outboxService = outboxService;

    public async Task BuildStartedAsync(string buildId, CancellationToken cancellationToken = default)
    {
        await _outboxService.EnqueueMessageAsync(
            outboxId: ServalWordAlignmentPlatformOutboxConstants.OutboxId,
            method: ServalWordAlignmentPlatformOutboxConstants.BuildStarted,
            groupId: buildId,
            content: new BuildStartedRequest { BuildId = buildId },
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
            outboxId: ServalWordAlignmentPlatformOutboxConstants.OutboxId,
            method: ServalWordAlignmentPlatformOutboxConstants.BuildCompleted,
            groupId: buildId,
            content: new BuildCompletedRequest
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
            outboxId: ServalWordAlignmentPlatformOutboxConstants.OutboxId,
            method: ServalWordAlignmentPlatformOutboxConstants.BuildCanceled,
            groupId: buildId,
            content: new BuildCanceledRequest { BuildId = buildId },
            cancellationToken: cancellationToken
        );
    }

    public async Task BuildFaultedAsync(string buildId, string message, CancellationToken cancellationToken = default)
    {
        await _outboxService.EnqueueMessageAsync(
            outboxId: ServalWordAlignmentPlatformOutboxConstants.OutboxId,
            method: ServalWordAlignmentPlatformOutboxConstants.BuildFaulted,
            groupId: buildId,
            content: new BuildFaultedRequest { BuildId = buildId, Message = message },
            cancellationToken: cancellationToken
        );
    }

    public async Task BuildRestartingAsync(string buildId, CancellationToken cancellationToken = default)
    {
        await _outboxService.EnqueueMessageAsync(
            outboxId: ServalWordAlignmentPlatformOutboxConstants.OutboxId,
            method: ServalWordAlignmentPlatformOutboxConstants.BuildRestarting,
            groupId: buildId,
            content: new BuildRestartingRequest { BuildId = buildId },
            cancellationToken: cancellationToken
        );
    }

    public async Task UpdateBuildStatusAsync(
        string buildId,
        ProgressStatus progressStatus,
        int? queueDepth = null,
        IReadOnlyCollection<BuildPhase>? phases = null,
        CancellationToken cancellationToken = default
    )
    {
        var request = new UpdateBuildStatusRequest { BuildId = buildId, Step = progressStatus.Step };
        if (progressStatus.PercentCompleted.HasValue)
            request.Progress = progressStatus.PercentCompleted.Value;
        if (progressStatus.Message is not null)
            request.Message = progressStatus.Message;
        if (queueDepth is not null)
            request.QueueDepth = queueDepth.Value;
        foreach (BuildPhase buildPhase in phases ?? [])
        {
            var phase = new Phase { Stage = (PhaseStage)buildPhase.Stage, };
            if (buildPhase.Step is not null)
                phase.Step = buildPhase.Step.Value;
            if (buildPhase.StepCount is not null)
                phase.StepCount = buildPhase.StepCount.Value;
            request.Phases.Add(phase);
        }

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
        Stream wordAlignmentsStream,
        CancellationToken cancellationToken = default
    )
    {
        await _outboxService.EnqueueMessageAsync(
            outboxId: ServalWordAlignmentPlatformOutboxConstants.OutboxId,
            method: ServalWordAlignmentPlatformOutboxConstants.InsertWordAlignments,
            groupId: engineId,
            content: engineId,
            stream: wordAlignmentsStream,
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
            outboxId: ServalWordAlignmentPlatformOutboxConstants.OutboxId,
            method: ServalWordAlignmentPlatformOutboxConstants.IncrementTrainEngineCorpusSize,
            groupId: engineId,
            content: new IncrementEngineCorpusSizeRequest { EngineId = engineId, Count = count },
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
            outboxId: ServalWordAlignmentPlatformOutboxConstants.OutboxId,
            method: ServalWordAlignmentPlatformOutboxConstants.UpdateBuildExecutionData,
            groupId: engineId,
            content: request,
            cancellationToken: cancellationToken
        );
    }
}
