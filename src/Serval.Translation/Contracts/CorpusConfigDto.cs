using Newtonsoft.Json;

namespace Serval.Translation.Contracts;

public class CorpusConfigDto
{
    /// <summary>
    /// The corpus name.
    /// </summary>
    public string? Name { get; set; }

    [JsonProperty(Required = Required.Always)]
    public string SourceLanguage { get; set; } = default!;

    [JsonProperty(Required = Required.Always)]
    public string TargetLanguage { get; set; } = default!;
    public bool? Pretranslate { get; set; }

    [JsonProperty(Required = Required.Always)]
    public CorpusFileConfigDto[] SourceFiles { get; set; } = default!;

    [JsonProperty(Required = Required.Always)]
    public CorpusFileConfigDto[] TargetFiles { get; set; } = default!;
}
