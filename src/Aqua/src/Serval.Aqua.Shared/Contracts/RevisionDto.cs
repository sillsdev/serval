namespace Serval.Aqua.Shared.Contracts;

public record RevisionDto
{
    public required int Id { get; init; }
    public required int BibleVersionId { get; init; }
    public string? VersionAbbreviation { get; init; }
    public DateTime? Date { get; init; }
    public string? Name { get; init; }
    public bool? Published { get; init; }
    public int? BackTranslationId { get; init; }

    [JsonPropertyName("machineTranslation")]
    public bool? MachineTranslation { get; init; }
    public string? IsoLanguage { get; init; }
}
