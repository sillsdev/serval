namespace Serval.Aqua.Shared.Contracts;

public record VersionDto
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required string IsoLanguage { get; init; }
    public required string IsoScript { get; init; }
    public required string Abbreviation { get; init; }
    public string? Rights { get; init; }
    public int? ForwardTranslationId { get; init; }
    public int? BackTranslationId { get; init; }

    [JsonPropertyName("machineTranslation")]
    public required bool MachineTranslation { get; init; }
    public required int OwnerId { get; init; }
    public required IReadOnlyList<int> GroupIds { get; init; }
}
