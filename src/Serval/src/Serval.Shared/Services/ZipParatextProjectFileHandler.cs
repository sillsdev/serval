namespace Serval.Shared.Services;

public class ZipParatextProjectFileHandler(IZipContainer container) : IParatextProjectFileHandler
{
    private readonly IZipContainer _projectContainer = container;

    public UsfmStylesheet CreateStylesheet(string fileName)
    {
        throw new NotImplementedException();
    }

    public bool Exists(string fileName)
    {
        return _projectContainer.EntryExists(fileName);
    }

    public Stream Open(string fileName)
    {
        return _projectContainer.OpenEntry(fileName);
    }

    public string? Find(string extension)
    {
        return _projectContainer.Entries.FirstOrDefault(e => e.EndsWith(extension));
    }
}
