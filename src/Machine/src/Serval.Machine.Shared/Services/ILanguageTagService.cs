namespace Serval.Machine.Shared.Services;

public interface ILanguageTagService
{
    (bool LanguageInScriptIsKnown, bool ScriptIsKnown) ConvertToFlores200Code(
        string languageTag,
        out string flores200Code
    );
}
