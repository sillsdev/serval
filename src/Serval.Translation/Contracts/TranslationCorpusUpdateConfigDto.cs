using System.ComponentModel.DataAnnotations;

namespace Serval.Translation.Contracts;

public class TranslationCorpusUpdateConfigDto : IValidatableObject
{
    public IList<TranslationCorpusFileConfigDto>? SourceFiles { get; set; }

    public IList<TranslationCorpusFileConfigDto>? TargetFiles { get; set; }

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
