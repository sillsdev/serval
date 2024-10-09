namespace Serval.Webhooks.Contracts;

public enum WebhookEvent
{
    TranslationBuildStarted,
    TranslationBuildFinished,

    AssessmentJobStarted,
    AssessmentJobFinished,
    WordAlignmentBuildStarted,
    WordAlignmentBuildFinished
}
