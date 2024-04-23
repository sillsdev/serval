namespace Serval.Aqua.Shared.Services;

public interface ILanguageTagService
{
    bool TryParse(
        string languageTag,
        [MaybeNullWhen(false)] out string languageCode,
        [MaybeNullWhen(false)] out string scriptCode
    );
}
