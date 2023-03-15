using System.Collections.Generic;
using Newtonsoft.Json;

namespace Serval.Core
{
    public class CorpusConfigDto
    {
        /// <summary>
        /// The corpus name.
        /// </summary>
        public string Name { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string SourceLanguage { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string TargetLanguage { get; set; }
        public bool? Pretranslate { get; set; }

        [JsonProperty(Required = Required.Always)]
        public List<CorpusFileConfigDto> SourceFiles { get; set; } = new List<CorpusFileConfigDto>();

        [JsonProperty(Required = Required.Always)]
        public List<CorpusFileConfigDto> TargetFiles { get; set; } = new List<CorpusFileConfigDto>();
    }
}
