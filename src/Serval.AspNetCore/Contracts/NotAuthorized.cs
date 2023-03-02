namespace Serval.AspNetCore.Contracts;

public record NotAuthorized
{
    public string Id { get; init; } = default!;
    public string Owner { get; init; } = default!;
}
