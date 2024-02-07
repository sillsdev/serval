using Newtonsoft.Json;

namespace Serval.Translation.Contracts;

public class TranslationEngineConfigDto
{
    /// <summary>
    /// The translation engine name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// The source language tag.
    /// </summary>
    [JsonProperty(Required = Required.Always)]
    public string SourceLanguage { get; set; } = default!;

    /// <summary>
    /// The target language tag.
    /// </summary>
    [JsonProperty(Required = Required.Always)]
    public string TargetLanguage { get; set; } = default!;

    /// <summary>
    /// The translation engine type.
    /// </summary>
    [JsonProperty(Required = Required.Always)]
    public string Type { get; set; } = default!;

    /// <summary>
    /// The model is saved when built and can be retrieved.
    /// </summary>
    [JsonProperty(Required = Required.Always)]
    public bool IsModelPersisted { get; set; } = false;
}
