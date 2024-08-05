namespace Serval.Shared.Services;

public class ZipParatextProjectTextUpdater : ParatextProjectTextUpdaterBase, IDisposable
{
    public ZipParatextProjectTextUpdater(IZipContainer container)
        : base(new ZipParatextProjectSettingsParser(container))
    {
        _projectContainer = container;
    }

    public ZipParatextProjectTextUpdater(IZipContainer container, ParatextProjectSettings settings)
        : base(settings)
    {
        _projectContainer = container;
    }

    private readonly IZipContainer _projectContainer;

    protected override bool Exists(string fileName)
    {
        return _projectContainer.EntryExists(fileName);
    }

    protected override Stream Open(string fileName)
    {
        return _projectContainer.OpenEntry(fileName);
    }

    public void Dispose()
    {
        _projectContainer.Dispose();
        GC.SuppressFinalize(this);
    }
}
