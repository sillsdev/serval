using Newtonsoft.Json;

namespace Serval.Translation.Contracts;

public class TranslationCorpusFileConfigDto
{
    [JsonProperty(Required = Required.Always)]
    public string FileId { get; set; } = default!;

    public string? TextId { get; set; }
}
