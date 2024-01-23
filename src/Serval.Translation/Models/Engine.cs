namespace Serval.Translation.Models;

public partial class Engine : IOwnedEntity
{
    public string Id { get; set; } = default!;
    public int Revision { get; set; } = 1;
    public string? Name { get; set; }
    public string SourceLanguage { get; set; } = default!;
    public string TargetLanguage { get; set; } = default!;
    private string _type = default!;
    public string Type
    {
        get => _type;
        set { _type = ToPascalCase(value); }
    }
    public string Owner { get; set; } = default!;
    public List<Corpus> Corpora { get; set; } = default!;
    public bool IsBuilding { get; set; }
    public int ModelRevision { get; set; }
    public double Confidence { get; set; }
    public int CorpusSize { get; set; }

    [GeneratedRegex(@"^[A-Z][a-z]+(?:[A-Z][a-z]+)*$")]
    private static partial Regex PascalCaseRegex();

    [GeneratedRegex(@"^[a-z]+(?:[A-Z][a-z]+)*$")]
    private static partial Regex SnakeCaseRegex();

    public static string ToPascalCase(string name)
    {
        // preserve Pascal, but convert snake, kebab, and space case to Pascal
        if (PascalCaseRegex().IsMatch(name))
        {
            return name;
        }
        else if (SnakeCaseRegex().IsMatch(name))
        {
            //capitalize first letter of string to make it into PascalCase
            return string.Concat(name[0].ToString().ToUpper(), name.AsSpan(1));
        }
        var tempName = name.ToLower().Replace("_", " ").Replace("-", " ");
        TextInfo info = CultureInfo.CurrentCulture.TextInfo;
        tempName = info.ToTitleCase(tempName).Replace(" ", string.Empty);
        return tempName;
    }
}
