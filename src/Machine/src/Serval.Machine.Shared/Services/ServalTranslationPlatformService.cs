using Google.Protobuf.WellKnownTypes;
using Serval.Translation.V1;
using Phase = Serval.Translation.V1.Phase;

namespace Serval.Machine.Shared.Services;

public class ServalTranslationPlatformService(
    TranslationPlatformApi.TranslationPlatformApiClient client,
    IOutboxService outboxService
) : IPlatformService
{
    EngineGroup IPlatformService.EngineGroup => EngineGroup.Translation;
    private readonly TranslationPlatformApi.TranslationPlatformApiClient _client = client;
    private readonly IOutboxService _outboxService = outboxService;

    public async Task BuildStartedAsync(string buildId, CancellationToken cancellationToken = default)
    {
        await _outboxService.EnqueueMessageAsync(
            outboxId: ServalTranslationPlatformOutboxConstants.OutboxId,
            method: ServalTranslationPlatformOutboxConstants.BuildStarted,
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
            outboxId: ServalTranslationPlatformOutboxConstants.OutboxId,
            method: ServalTranslationPlatformOutboxConstants.BuildCompleted,
            groupId: buildId,
            content: new BuildCompletedRequest
            {
                BuildId = buildId,
                CorpusSize = trainSize,
                Confidence = confidence,
            },
            cancellationToken: cancellationToken
        );
    }

    public async Task BuildCanceledAsync(string buildId, CancellationToken cancellationToken = default)
    {
        await _outboxService.EnqueueMessageAsync(
            outboxId: ServalTranslationPlatformOutboxConstants.OutboxId,
            method: ServalTranslationPlatformOutboxConstants.BuildCanceled,
            groupId: buildId,
            content: new BuildCanceledRequest { BuildId = buildId },
            cancellationToken: cancellationToken
        );
    }

    public async Task BuildFaultedAsync(string buildId, string message, CancellationToken cancellationToken = default)
    {
        await _outboxService.EnqueueMessageAsync(
            outboxId: ServalTranslationPlatformOutboxConstants.OutboxId,
            method: ServalTranslationPlatformOutboxConstants.BuildFaulted,
            groupId: buildId,
            content: new BuildFaultedRequest { BuildId = buildId, Message = message },
            cancellationToken: cancellationToken
        );
    }

    public async Task BuildRestartingAsync(string buildId, CancellationToken cancellationToken = default)
    {
        await _outboxService.EnqueueMessageAsync(
            outboxId: ServalTranslationPlatformOutboxConstants.OutboxId,
            method: ServalTranslationPlatformOutboxConstants.BuildRestarting,
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
        DateTime? started = null,
        DateTime? completed = null,
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
            var phase = new Phase { Stage = (PhaseStage)buildPhase.Stage };
            if (buildPhase.Step is not null)
                phase.Step = buildPhase.Step.Value;
            if (buildPhase.StepCount is not null)
                phase.StepCount = buildPhase.StepCount.Value;
            if (buildPhase.Started is not null)
                phase.Started = buildPhase.Started.Value.ToTimestamp();
            request.Phases.Add(phase);
        }

        if (started is not null)
            request.Started = started.Value.ToTimestamp();
        if (completed is not null)
            request.Completed = completed.Value.ToTimestamp();

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
        await _outboxService.EnqueueMessageAsync(
            outboxId: ServalTranslationPlatformOutboxConstants.OutboxId,
            method: ServalTranslationPlatformOutboxConstants.InsertPretranslations,
            groupId: engineId,
            content: engineId,
            stream: pretranslationsStream,
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
            outboxId: ServalTranslationPlatformOutboxConstants.OutboxId,
            method: ServalTranslationPlatformOutboxConstants.IncrementEngineCorpusSize,
            groupId: engineId,
            content: new IncrementEngineCorpusSizeRequest { EngineId = engineId, Count = count },
            cancellationToken: cancellationToken
        );
    }

    public async Task UpdateBuildExecutionDataAsync(
        string engineId,
        string buildId,
        BuildExecutionData executionData,
        CancellationToken cancellationToken = default
    )
    {
        var request = new UpdateBuildExecutionDataRequest
        {
            EngineId = engineId,
            BuildId = buildId,
            ExecutionData = new ExecutionData
            {
                TrainCount = executionData.TrainCount ?? 0,
                PretranslateCount = executionData.PretranslateCount ?? 0,
                EngineSourceLanguageTag = executionData.EngineSourceLanguageTag,
                EngineTargetLanguageTag = executionData.EngineTargetLanguageTag,
                ResolvedSourceLanguage = executionData.ResolvedSourceLanguage ?? string.Empty,
                ResolvedTargetLanguage = executionData.ResolvedTargetLanguage ?? string.Empty,
            },
        };
        foreach (string warning in executionData.Warnings ?? [])
            request.ExecutionData.Warnings.Add(warning);
        await _outboxService.EnqueueMessageAsync(
            outboxId: ServalTranslationPlatformOutboxConstants.OutboxId,
            method: ServalTranslationPlatformOutboxConstants.UpdateBuildExecutionData,
            groupId: engineId,
            content: request,
            cancellationToken: cancellationToken
        );
    }

    public async Task UpdateTargetQuoteConventionAsync(
        string engineId,
        string buildId,
        string quoteConvention,
        CancellationToken cancellationToken = default
    )
    {
        var content = new UpdateTargetQuoteConventionRequest
        {
            EngineId = engineId,
            BuildId = buildId,
            TargetQuoteConvention = quoteConvention,
        };

        await _outboxService.EnqueueMessageAsync(
            outboxId: ServalTranslationPlatformOutboxConstants.OutboxId,
            method: ServalTranslationPlatformOutboxConstants.UpdateTargetQuoteConvention,
            groupId: engineId,
            content,
            cancellationToken: cancellationToken
        );
    }
}
