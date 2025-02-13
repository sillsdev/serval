namespace Serval.Translation.Contracts;

public class TranslationEngineUpdateConfigDto
{
    public string SourceLanguage { get; init; } = string.Empty;

    public string TargetLanguage { get; init; } = string.Empty;
}
