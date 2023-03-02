using Newtonsoft.Json;

namespace Serval.Core
{
    public class CorpusDto
    {
        [JsonProperty(Required = Required.DisallowNull)]
        public string Id { get; set; }

        [JsonProperty(Required = Required.DisallowNull)]
        public string Url { get; set; }

        [JsonProperty(Required = Required.DisallowNull)]
        public string Name { get; set; }
        public CorpusType Type { get; set; }
        public FileFormat Format { get; set; }
    }
}
