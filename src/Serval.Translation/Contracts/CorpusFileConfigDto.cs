using Newtonsoft.Json;

namespace Serval.Translation.Contracts;

public class CorpusFileConfigDto
{
    [JsonProperty(Required = Required.Always)]
    public string FileId { get; set; } = default!;

    public string? TextId { get; set; }
}
