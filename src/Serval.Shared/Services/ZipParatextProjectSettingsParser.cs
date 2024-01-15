namespace Serval.Shared.Services;

public class ZipParatextProjectSettingsParser(IZipContainer projectContainer) : ZipParatextProjectSettingsParserBase
{
    private readonly IZipContainer _projectContainer = projectContainer;

    protected override bool Exists(string fileName)
    {
        return _projectContainer.EntryExists(fileName);
    }

    protected override string? Find(string extension)
    {
        return _projectContainer.Entries.FirstOrDefault(e => e.EndsWith(extension));
    }

    protected override Stream Open(string fileName)
    {
        return _projectContainer.OpenEntry(fileName);
    }
}
