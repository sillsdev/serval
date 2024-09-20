using System.ComponentModel.DataAnnotations;

namespace Serval.Translation.Contracts;

public record TranslationParallelCorpusUpdateConfigDto : IValidatableObject
{
    public IReadOnlyList<string>? SourceCorpusRefs { get; init; }

    public IReadOnlyList<string>? TargetCorpusRefs { get; init; }

    public IEnumerable<System.ComponentModel.DataAnnotations.ValidationResult> Validate(
        ValidationContext validationContext
    )
    {
        if (SourceCorpusRefs is null && TargetCorpusRefs is null)
        {
            yield return new System.ComponentModel.DataAnnotations.ValidationResult(
                "At least one field must be specified.",
                [nameof(SourceCorpusRefs), nameof(TargetCorpusRefs)]
            );
        }
    }
}
