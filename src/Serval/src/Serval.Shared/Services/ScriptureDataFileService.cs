namespace Serval.Shared.Services;

public class ScriptureDataFileService(IFileSystem fileSystem, IOptionsMonitor<DataFileOptions> dataFileOptions)
    : IScriptureDataFileService
{
    private readonly IFileSystem _fileSystem = fileSystem;
    private readonly IOptionsMonitor<DataFileOptions> _dataFileOptions = dataFileOptions;

    public ParatextProjectSettings GetParatextProjectSettings(string filename)
    {
        using IZipContainer container = _fileSystem.OpenZipFile(GetFilePath(filename));
        return ParseProjectSettings(container);
    }

    public ZipParatextProjectTextUpdater GetZipParatextProjectTextUpdater(string filename)
    {
        using IZipContainer container = _fileSystem.OpenZipFile(GetFilePath(filename));
        return new ZipParatextProjectTextUpdater(container);
    }

    public async Task<string?> ReadParatextProjectBookAsync(string filename, string book)
    {
        using IZipContainer container = _fileSystem.OpenZipFile(GetFilePath(filename));
        ParatextProjectSettings settings = ParseProjectSettings(container);
        string entryName = settings.GetBookFileName(book);
        if (!container.EntryExists(entryName))
            return null;
        using StreamReader reader = new(container.OpenEntry(entryName));
        return await reader.ReadToEndAsync();
    }

    private string GetFilePath(string filename)
    {
        return Path.Combine(_dataFileOptions.CurrentValue.FilesDirectory, filename);
    }

    private static ParatextProjectSettings ParseProjectSettings(IZipContainer container)
    {
        ZipParatextProjectSettingsParser settingsParser = new(container);
        return settingsParser.Parse();
    }
}
