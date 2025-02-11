namespace Serval.Translation.Contracts;

public class UpdateLanguagesRequestDto
{
    public string SourceLanguage { get; set; } = string.Empty;

    public string TargetLanguage { get; set; } = string.Empty;
}
