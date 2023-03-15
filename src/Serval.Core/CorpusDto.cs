using System.Collections.Generic;
using Newtonsoft.Json;

namespace Serval.Core
{
    public class CorpusDto
    {
        [JsonProperty(Required = Required.DisallowNull)]
        public string Id { get; set; }

        [JsonProperty(Required = Required.DisallowNull)]
        public string Url { get; set; }

        public string Name { get; set; }

        [JsonProperty(Required = Required.DisallowNull)]
        public string SourceLanguage { get; set; }

        [JsonProperty(Required = Required.DisallowNull)]
        public string TargetLanguage { get; set; }
        public bool Pretranslate { get; set; }
        public List<CorpusFileDto> SourceFiles { get; set; } = new List<CorpusFileDto>();
        public List<CorpusFileDto> TargetFiles { get; set; } = new List<CorpusFileDto>();
    }
}
