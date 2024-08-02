namespace Serval.Shared.Services;

public interface IScriptureDataFileService
{
    ParatextProjectSettings GetParatextProjectSettings(string filename);
    public ZipParatextProjectTextUpdater GetZipParatextProjectTextUpdater(string filename);
}
