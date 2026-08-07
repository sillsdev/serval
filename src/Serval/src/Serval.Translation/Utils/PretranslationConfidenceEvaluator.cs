namespace Serval.Translation.Utils;

public class PretranslationConfidenceEvaluator
{
    private const double LowConfidenceThreshold = 0.25;

    public static bool IsBookPretranslationConfidenceUnusuallyLow(
        double confidence,
        string bookId,
        string? baseModel
    )
    {
        return confidence < LowConfidenceThreshold;
    }
}
