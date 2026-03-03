namespace Serval.Shared.Services;

public interface IScriptureDataFileService
{
    ParatextProjectSettings GetParatextProjectSettings(string filename, string[]? referenceFilenames = null);
    ZipParatextProjectTextUpdater GetZipParatextProjectTextUpdater(
        string filename,
        string[]? referenceFilenames = null
    );
}
