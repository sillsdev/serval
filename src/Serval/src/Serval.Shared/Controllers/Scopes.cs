namespace Serval.Shared.Controllers;

public static class Scopes
{
    public const string CreateTranslationEngines = "create:translation_engines";
    public const string ReadTranslationEngines = "read:translation_engines";
    public const string UpdateTranslationEngines = "update:translation_engines";
    public const string DeleteTranslationEngines = "delete:translation_engines";

    public const string CreateAssessmentCorpora = "create:assessment_corpora";
    public const string ReadAssessmentCorpora = "read:assessment_corpora";
    public const string UpdateAssessmentCorpora = "update:assessment_corpora";
    public const string DeleteAssessmentCorpora = "delete:assessment_corpora";

    public const string CreateAssessmentEngines = "create:assessment_engines";
    public const string ReadAssessmentEngines = "read:assessment_engines";
    public const string UpdateAssessmentEngines = "update:assessment_engines";
    public const string DeleteAssessmentEngines = "delete:assessment_engines";

    public const string CreateHooks = "create:hooks";
    public const string ReadHooks = "read:hooks";
    public const string DeleteHooks = "delete:hooks";

    public const string CreateFiles = "create:files";
    public const string ReadFiles = "read:files";
    public const string UpdateFiles = "update:files";
    public const string DeleteFiles = "delete:files";

    public const string ReadStatus = "read:status";

    public static IEnumerable<string> All =>
        new[]
        {
            CreateTranslationEngines,
            ReadTranslationEngines,
            UpdateTranslationEngines,
            DeleteTranslationEngines,
            CreateAssessmentEngines,
            ReadAssessmentEngines,
            UpdateAssessmentEngines,
            DeleteAssessmentEngines,
            CreateAssessmentCorpora,
            ReadAssessmentCorpora,
            UpdateAssessmentCorpora,
            DeleteAssessmentCorpora,
            CreateHooks,
            ReadHooks,
            DeleteHooks,
            CreateFiles,
            ReadFiles,
            UpdateFiles,
            DeleteFiles,
            ReadStatus
        };
}
