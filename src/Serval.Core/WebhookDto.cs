using Newtonsoft.Json;

namespace Serval.Core
{
    public class WebhookDto
    {
        [JsonProperty(Required = Required.DisallowNull)]
        public string Id { get; set; }

        [JsonProperty(Required = Required.DisallowNull)]
        public string Href { get; set; }

        [JsonProperty(Required = Required.DisallowNull)]
        public string Url { get; set; }

        [JsonProperty(Required = Required.DisallowNull)]
        public WebhookEvent[] Events { get; set; }
    }
}
