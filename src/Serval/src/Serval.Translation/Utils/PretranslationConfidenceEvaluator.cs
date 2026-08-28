using SIL.Machine.QualityEstimation;

namespace Serval.Translation.Utils;

public class PretranslationConfidenceEvaluator
{
    public static bool IsBookPretranslationConfidenceUnusuallyLow(double confidence, string bookId, string? model)
    {
        return BookConfidence.IsBookConfidenceUnusuallyLow(confidence, bookId, model);
    }
}
