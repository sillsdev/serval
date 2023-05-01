using Newtonsoft.Json;

namespace Serval.Translation.Contracts;

public class TranslationCorpusConfigDto
{
    /// <summary>
    /// The corpus name.
    /// </summary>
    public string? Name { get; set; }

    [JsonProperty(Required = Required.Always)]
    public string SourceLanguage { get; set; } = default!;

    [JsonProperty(Required = Required.Always)]
    public string TargetLanguage { get; set; } = default!;

    [JsonProperty(Required = Required.Always)]
    public IList<TranslationCorpusFileConfigDto> SourceFiles { get; set; } = default!;

    [JsonProperty(Required = Required.Always)]
    public IList<TranslationCorpusFileConfigDto> TargetFiles { get; set; } = default!;
}
