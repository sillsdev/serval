namespace Serval.Shared.Services;

public interface IScriptureDataFileService
{
    ParatextProjectSettings GetParatextProjectSettings(string filename);
    public ZipParatextProjectTextUpdater GetZipParatextProjectTextUpdater(string filename);
    Task<string?> ReadParatextProjectBookAsync(string filename, string book);
}
