namespace Serval.Shared.Controllers;

public static class Scopes
{
    public const string CreateTranslationEngines = "create:translation_engines";
    public const string ReadTranslationEngines = "read:translation_engines";
    public const string UpdateTranslationEngines = "update:translation_engines";
    public const string DeleteTranslationEngines = "delete:translation_engines";

    public const string CreateWordAlignmentEngines = "create:word_alignment_engines";
    public const string ReadWordAlignmentEngines = "read:word_alignment_engines";
    public const string UpdateWordAlignmentEngines = "update:word_alignment_engines";
    public const string DeleteWordAlignmentEngines = "delete:word_alignment_engines";

    public const string CreateHooks = "create:hooks";
    public const string ReadHooks = "read:hooks";
    public const string DeleteHooks = "delete:hooks";

    public const string CreateFiles = "create:files";
    public const string ReadFiles = "read:files";
    public const string UpdateFiles = "update:files";
    public const string DeleteFiles = "delete:files";

    public const string ReadStatus = "read:status";

    public static IEnumerable<string> All =>
        [
            CreateTranslationEngines,
            ReadTranslationEngines,
            UpdateTranslationEngines,
            DeleteTranslationEngines,
            CreateWordAlignmentEngines,
            ReadWordAlignmentEngines,
            UpdateWordAlignmentEngines,
            DeleteWordAlignmentEngines,
            CreateHooks,
            ReadHooks,
            DeleteHooks,
            CreateFiles,
            ReadFiles,
            UpdateFiles,
            DeleteFiles,
            ReadStatus
        ];
}
