namespace Serval.Aqua.Shared.Contracts;

public record TokenDto
{
    public required string AccessToken { get; init; }
    public required string TokenType { get; init; }
}
