using Newtonsoft.Json;

namespace Serval.Core
{
    public class ResourceLinkDto
    {
        [JsonProperty(Required = Required.DisallowNull)]
        public string Id { get; set; }

        [JsonProperty(Required = Required.DisallowNull)]
        public string Href { get; set; }
    }
}
