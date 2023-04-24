using Newtonsoft.Json;

namespace Serval.Translation.Contracts;

public class PretranslateCorpusConfigDto
{
    [JsonProperty(Required = Required.Always)]
    public string CorpusId { get; set; } = default!;

    public List<string>? TextIds { get; set; }
}
