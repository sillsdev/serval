using Newtonsoft.Json;

namespace Serval.Translation.Contracts;

public class PretranslateCorpusConfigDto
{
    [JsonProperty(Required = Required.Always)]
    public string CorpusId { get; set; } = default!;

    public IList<string>? TextIds { get; set; }

    public string? BiblicalRange {get; set;}
}
