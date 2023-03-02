using Newtonsoft.Json;

namespace Serval.Core
{
    public class TranslationEngineCorpusDto
    {
        [JsonProperty(Required = Required.DisallowNull)]
        public string Url { get; set; }

        [JsonProperty(Required = Required.DisallowNull)]
        public ResourceLinkDto Corpus { get; set; }
        public bool Pretranslate { get; set; }
    }
}
