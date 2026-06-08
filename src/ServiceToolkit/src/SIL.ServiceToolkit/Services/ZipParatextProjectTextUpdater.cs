namespace SIL.ServiceToolkit.Services;

public class ZipParatextProjectTextUpdater(
    IZipContainer projectContainer,
    ParatextProjectSettings? parentProjectSettings = null
)
    : ParatextProjectTextUpdaterBase(
        new ZipParatextProjectFileHandler(projectContainer),
        new ZipParatextProjectSettingsParser(projectContainer, parentProjectSettings).Parse()
    ),
        IDisposable
{
    private bool _disposed;

    private readonly IZipContainer _projectContainer = projectContainer;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _projectContainer.Dispose();
            }
            _disposed = true;
        }
    }
}
