using System.ComponentModel.DataAnnotations;

namespace Serval.Translation.Contracts;

public record TranslationCorpusUpdateConfigDto : IValidatableObject
{
    public IReadOnlyList<TranslationCorpusFileConfigDto>? SourceFiles { get; init; }

    public IReadOnlyList<TranslationCorpusFileConfigDto>? TargetFiles { get; init; }

    public IEnumerable<System.ComponentModel.DataAnnotations.ValidationResult> Validate(
        ValidationContext validationContext
    )
    {
        if (SourceFiles is null && TargetFiles is null)
        {
            yield return new System.ComponentModel.DataAnnotations.ValidationResult(
                "At least one field must be specified.",
                new[] { nameof(SourceFiles), nameof(TargetFiles) }
            );
        }
    }
}
