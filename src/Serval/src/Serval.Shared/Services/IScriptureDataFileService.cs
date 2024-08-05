namespace Serval.Shared.Services;

public interface IScriptureDataFileService
{
    ParatextProjectSettings GetParatextProjectSettings(string filename);
    ZipParatextProjectTextUpdater GetZipParatextProjectTextUpdater(string filename);
}
