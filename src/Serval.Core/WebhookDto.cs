using Newtonsoft.Json;

namespace Serval.Core
{
    public class WebhookDto
    {
        [JsonProperty(Required = Required.DisallowNull)]
        public string Id { get; set; }

        [JsonProperty(Required = Required.DisallowNull)]
        public string Url { get; set; }

        [JsonProperty(Required = Required.DisallowNull)]
        public string PayloadUrl { get; set; }

        [JsonProperty(Required = Required.DisallowNull)]
        public WebhookEvent[] Events { get; set; }
    }
}
