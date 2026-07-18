namespace Serval.ApiKeys.Dtos;

public record ApiKeyCreatedDto : ApiKeyDto
{
    /// <summary>
    /// The plaintext API key. It is only returned when the API key is created and cannot be retrieved afterwards.
    /// </summary>
    public required string Key { get; init; }
}
