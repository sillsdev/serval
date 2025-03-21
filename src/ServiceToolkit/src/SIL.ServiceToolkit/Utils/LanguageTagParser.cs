namespace SIL.ServiceToolkit.Utils;

public partial class LanguageTagParser
{
    private static readonly Dictionary<string, string> StandardLanguages =
        new()
        {
            { "ar", "arb" },
            { "ms", "zsm" },
            { "lv", "lvs" },
            { "ne", "npi" },
            { "sw", "swh" },
            { "cmn", "zh" }
        };

    private static readonly Dictionary<string, string> StandardScripts = new() { { "Kore", "Hang" } };

    private readonly Dictionary<string, string> _defaultScripts;

    [GeneratedRegex("(?'language'[a-zA-Z]{2,8})([_-](?'script'[a-zA-Z]{4}))?", RegexOptions.ExplicitCapture)]
    private static partial Regex LangTagPattern();

    public LanguageTagParser()
    {
        // initialize SLDR language tags to retrieve latest langtags.json file
        _defaultScripts = InitializeDefaultScripts();
    }

    private static Dictionary<string, string> InitializeDefaultScripts()
    {
        Sldr.InitializeLanguageTags();
        string cachedAllTagsPath = Path.Combine(Sldr.SldrCachePath, "langtags.json");
        using FileStream stream = new(cachedAllTagsPath, FileMode.Open);
        var json = JsonNode.Parse(stream);

        Dictionary<string, string> defaultScripts = [];
        foreach (JsonNode? entry in json!.AsArray())
        {
            if (entry is null)
                continue;

            var script = (string?)entry["script"];
            if (script is null)
                continue;

            JsonNode? tags = entry["tags"];
            if (tags is not null)
            {
                foreach (var t in tags.AsArray().Select(v => (string?)v))
                {
                    if (
                        t is not null
                        && IetfLanguageTag.TryGetParts(t, out _, out string? s, out _, out _)
                        && s is null
                    )
                    {
                        defaultScripts[t] = script;
                    }
                }
            }

            var tag = (string?)entry["tag"];
            if (tag is not null)
                defaultScripts[tag] = script;
        }
        return defaultScripts;
    }

    public bool TryParse(
        string languageTag,
        [MaybeNullWhen(false)] out string languageCode,
        [MaybeNullWhen(false)] out string scriptCode
    )
    {
        languageCode = null;
        scriptCode = null;

        // Try to find a pattern of {language code}_{script}
        Match langTagMatch = LangTagPattern().Match(languageTag);
        if (!langTagMatch.Success)
            return false;

        string parsedLanguage = langTagMatch.Groups["language"].Value;
        string languageSubtag = parsedLanguage;
        languageCode = parsedLanguage;

        // Best attempt to convert language to a registered ISO 639-3 code
        // Uses https://www.iana.org/assignments/language-subtag-registry/language-subtag-registry for mapping

        // If they gave us the ISO code, revert it to the 2 character code
        if (StandardSubtags.TryGetLanguageFromIso3Code(languageSubtag, out LanguageSubtag tempSubtag))
            languageSubtag = tempSubtag.Code;

        // There are a few extra conversions not in SIL Writing Systems that we need to handle
        if (StandardLanguages.TryGetValue(languageSubtag, out string? tempName))
            languageSubtag = tempName;

        if (StandardSubtags.RegisteredLanguages.TryGet(languageSubtag, out LanguageSubtag? languageSubtagObj))
            languageCode = languageSubtagObj.Iso3Code;

        // Use default script unless there is one parsed out of the language tag
        Group scriptGroup = langTagMatch.Groups["script"];

        if (scriptGroup.Success)
            scriptCode = scriptGroup.Value;
        else if (_defaultScripts.TryGetValue(languageTag, out string? tempScript2))
            scriptCode = tempScript2;
        else if (_defaultScripts.TryGetValue(languageSubtag, out string? tempScript))
            scriptCode = tempScript;

        // There are a few extra conversions not in SIL Writing Systems that we need to handle
        if (scriptCode is not null && StandardScripts.TryGetValue(scriptCode, out string? tempScript3))
            scriptCode = tempScript3;

        return scriptCode is not null;
    }
}
