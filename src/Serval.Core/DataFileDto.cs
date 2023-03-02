using Newtonsoft.Json;

namespace Serval.Core
{
    public class DataFileDto
    {
        [JsonProperty(Required = Required.DisallowNull)]
        public string Id { get; set; }

        [JsonProperty(Required = Required.DisallowNull)]
        public string Href { get; set; }

        [JsonProperty(Required = Required.DisallowNull)]
        public ResourceLinkDto Corpus { get; set; }

        [JsonProperty(Required = Required.DisallowNull)]
        public string LanguageTag { get; set; }

        [JsonProperty(Required = Required.DisallowNull)]
        public string Name { get; set; }
        public string TextId { get; set; }
    }
}
