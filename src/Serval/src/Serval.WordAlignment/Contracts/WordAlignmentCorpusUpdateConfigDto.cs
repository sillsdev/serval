using System.ComponentModel.DataAnnotations;

namespace Serval.WordAlignment.Contracts;

public record WordAlignmentCorpusUpdateConfigDto : IValidatableObject
{
    public IReadOnlyList<WordAlignmentCorpusFileConfigDto>? SourceFiles { get; init; }

    public IReadOnlyList<WordAlignmentCorpusFileConfigDto>? TargetFiles { get; init; }

    public IEnumerable<System.ComponentModel.DataAnnotations.ValidationResult> Validate(
        ValidationContext validationContext
    )
    {
        if (SourceFiles is null && TargetFiles is null)
        {
            yield return new System.ComponentModel.DataAnnotations.ValidationResult(
                "At least one field must be specified.",
                [nameof(SourceFiles), nameof(TargetFiles)]
            );
        }
    }
}
