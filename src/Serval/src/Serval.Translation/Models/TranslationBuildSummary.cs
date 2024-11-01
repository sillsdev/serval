namespace Serval.Translation.Models;

public class TranslationBuildSummary
{
    public int LinesTrainedOn { get; set; }
    public int LinesPretranslated { get; set; }
    public TimeSpan TotalBuildTime { get; set; }
}
