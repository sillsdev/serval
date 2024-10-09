using System.ComponentModel.DataAnnotations;

namespace Serval.Translation.Contracts;

public record TranslationParallelCorpusUpdateConfigDto : IValidatableObject
{
    public IReadOnlyList<string>? SourceCorpusIds { get; init; }

    public IReadOnlyList<string>? TargetCorpusIds { get; init; }

    public IEnumerable<System.ComponentModel.DataAnnotations.ValidationResult> Validate(
        ValidationContext validationContext
    )
    {
        if (SourceCorpusIds is null && TargetCorpusIds is null)
        {
            yield return new System.ComponentModel.DataAnnotations.ValidationResult(
                "At least one field must be specified.",
                [nameof(SourceCorpusIds), nameof(TargetCorpusIds)]
            );
        }
    }
}
