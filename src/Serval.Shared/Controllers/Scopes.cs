namespace Serval.Shared.Controllers;

public static class Scopes
{
    public const string CreateTranslationEngines = "create:translation_engines";
    public const string ReadTranslationEngines = "read:translation_engines";
    public const string UpdateTranslationEngines = "update:translation_engines";
    public const string DeleteTranslationEngines = "delete:translation_engines";

    public const string CreateHooks = "create:hooks";
    public const string ReadHooks = "read:hooks";
    public const string DeleteHooks = "delete:hooks";

    public const string CreateFiles = "create:files";
    public const string ReadFiles = "read:files";
    public const string DeleteFiles = "delete:files";

    public static IEnumerable<string> All =>
        new[]
        {
            CreateTranslationEngines,
            ReadTranslationEngines,
            UpdateTranslationEngines,
            DeleteTranslationEngines,
            CreateHooks,
            ReadHooks,
            DeleteHooks,
            CreateFiles,
            ReadFiles,
            DeleteFiles
        };
}
