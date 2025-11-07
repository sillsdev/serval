namespace Serval.Machine.Shared.Services;

public class LanguageTagService : ILanguageTagService
{
    private readonly HashSet<string> _flores200Languages = [];
    private readonly HashSet<string> _flores200Scripts = [];
    private readonly LanguageTagParser _parser = new();

    public LanguageTagService()
    {
        InitializeFlores200Languages();
    }

    private void InitializeFlores200Languages()
    {
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
            _flores200Languages.Add(values[1].Trim());
            _flores200Scripts.Add(values[1].Trim().Split('_')[1]);
        }
    }

    /// <summary>
    /// Converts a language tag to a Flores-200 code
    /// </summary>
    /// <param name="languageTag">The language tag to convert</param>
    /// <param name="flores200Code">The converted Flores-200 code</param>
    /// <returns> Is the language in the Flores-200 list and is the script in the Flores-200 list</returns>
    public Flores200Support ConvertToFlores200Code(string languageTag, out string flores200Code)
    {
        if (_parser.TryParse(languageTag, out string? languageCode, out string? scriptCode))
            flores200Code = $"{languageCode}_{scriptCode}";
        else
            flores200Code = languageTag;
        if (_flores200Scripts.Contains(scriptCode ?? ""))
        {
            if (_flores200Languages.Contains(flores200Code))
            {
                return Flores200Support.LanguageAndScript;
            }
            return Flores200Support.OnlyScript;
        }
        return Flores200Support.None;
    }
}
