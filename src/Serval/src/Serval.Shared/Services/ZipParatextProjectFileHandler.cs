namespace Serval.Shared.Services;

public class ZipParatextProjectFileHandler(IZipContainer container, ParatextProjectSettings? settings = null)
    : IParatextProjectFileHandler
{
    private readonly IZipContainer _projectContainer = container;
    private readonly ParatextProjectSettings _settings =
        settings ?? new ZipParatextProjectSettingsParser(container).Parse();

    public ParatextProjectSettings GetSettings()
    {
        return _settings;
    }

    public bool Exists(string fileName)
    {
        return _projectContainer.EntryExists(fileName);
    }

    public Stream Open(string fileName)
    {
        return _projectContainer.OpenEntry(fileName);
    }
}
