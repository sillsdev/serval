﻿namespace Serval.Machine.Shared.Services;

public static class ServalTranslationPlatformOutboxConstants
{
    public const string OutboxId = "ServalTranslationPlatform";

    public const string BuildStarted = "BuildStarted";
    public const string BuildCompleted = "BuildCompleted";
    public const string BuildCanceled = "BuildCanceled";
    public const string BuildFaulted = "BuildFaulted";
    public const string BuildRestarting = "BuildRestarting";
    public const string IncrementTrainEngineCorpusSize = "IncrementTrainEngineCorpusSize";
    public const string InsertInferences = "InsertInferences";
}