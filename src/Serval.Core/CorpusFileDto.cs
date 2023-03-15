using Newtonsoft.Json;

namespace Serval.Core
{
    public class CorpusFileDto
    {
        [JsonProperty(Required = Required.DisallowNull)]
        public ResourceLinkDto File { get; set; }

        public string TextId { get; set; }
    }
}
