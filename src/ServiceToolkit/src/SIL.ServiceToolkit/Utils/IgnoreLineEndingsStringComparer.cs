namespace SIL.ServiceToolkit.Utils;

public sealed class IgnoreLineEndingsStringComparer : StringComparer
{
    public override int Compare(string? x, string? y)
    {
        return string.Compare(x?.ReplaceLineEndings(), y?.ReplaceLineEndings(), StringComparison.InvariantCulture);
    }

    public override bool Equals(string? x, string? y) =>
        string.Equals(x?.ReplaceLineEndings(), y?.ReplaceLineEndings(), StringComparison.InvariantCulture);

    public override int GetHashCode(string obj) => obj.ReplaceLineEndings().GetHashCode();
}
