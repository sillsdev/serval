namespace ApiExample;

/// <summary>
/// The Serval API options configured via <c>dotnet user-secrets</c>.
/// </summary>
public record ServalOptions
{
    /// <summary>
    /// Gets the Serval API Server to use.
    /// </summary>
    public string ApiServer { get; init; } = string.Empty;

    /// <summary>
    /// Gets the JWT audience.
    /// </summary>
    public string Audience { get; init; } = string.Empty;

    /// <summary>
    /// Gets the JWT client identifier.
    /// </summary>
    public string ClientId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the JWT client secret.
    /// </summary>
    public string ClientSecret { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the endpoint to generate the JWT.
    /// </summary>
    public string TokenUrl { get; init; } = string.Empty;
}
