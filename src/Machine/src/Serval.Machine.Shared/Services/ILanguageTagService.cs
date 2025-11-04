namespace Serval.Machine.Shared.Services;

public enum Flores200Support
{
    LanguageAndScript,
    OnlyScript,
    None
}

public interface ILanguageTagService
{
    Flores200Support ConvertToFlores200Code(
        string languageTag,
        out string flores200Code
    );
}
