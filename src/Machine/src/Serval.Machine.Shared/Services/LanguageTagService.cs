namespace Serval.Machine.Shared.Services;

public class LanguageTagService : ILanguageTagService
{
    private readonly Dictionary<string, string> _flores200Languages = InitializeFlores200Languages();
    private readonly LanguageTagParser _parser = new();

    private static Dictionary<string, string> InitializeFlores200Languages()
    {
        Dictionary<string, string> flores200Languages = [];
        using var floresStream = Assembly
            .GetExecutingAssembly()
            .GetManifestResourceStream("Serval.Machine.Shared.data.flores200languages.csv");
        Debug.Assert(floresStream is not null);
        StreamReader reader = new(floresStream);
        string? firstLine = reader.ReadLine();
        Debug.Assert(firstLine == "language, code");
        while (!reader.EndOfStream)
        {
            string? line = reader.ReadLine();
            if (line is null)
                continue;
            string[] values = line.Split(',');
            flores200Languages[values[1].Trim()] = values[0].Trim();
        }
        return flores200Languages;
    }

    /**
     * Converts a language tag to a Flores 200 code
     * @param {string} languageTag - The language tag to convert
     * @param out {string} flores200Code - The converted Flores 200 code
     * @returns {bool} is the language is the Flores 200 list
     */
    public bool ConvertToFlores200Code(string languageTag, out string flores200Code)
    {
        if (_parser.TryParse(languageTag, out string? languageCode, out string? scriptCode))
            flores200Code = $"{languageCode}_{scriptCode}";
        else
            flores200Code = languageTag;
        return _flores200Languages.ContainsKey(flores200Code);
    }
}
