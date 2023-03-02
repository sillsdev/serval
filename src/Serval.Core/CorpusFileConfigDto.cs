using Newtonsoft.Json;

namespace Serval.Core
{
    public class CorpusFileConfigDto
    {
        [JsonProperty(Required = Required.Always)]
        public string FileId { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string LanguageTag { get; set; }

        public string TextId { get; set; }
    }
}
