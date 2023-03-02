using Newtonsoft.Json;

namespace Serval.Core
{
    public class CorpusFileDto
    {
        [JsonProperty(Required = Required.DisallowNull)]
        public string Id { get; set; }

        [JsonProperty(Required = Required.DisallowNull)]
        public string Url { get; set; }

        [JsonProperty(Required = Required.DisallowNull)]
        public ResourceLinkDto Corpus { get; set; }

        [JsonProperty(Required = Required.DisallowNull)]
        public ResourceLinkDto File { get; set; }

        [JsonProperty(Required = Required.DisallowNull)]
        public string LanguageTag { get; set; }

        public string TextId { get; set; }
    }
}
