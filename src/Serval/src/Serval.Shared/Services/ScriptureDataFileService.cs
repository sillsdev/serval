namespace Serval.Shared.Services;

public class ScriptureDataFileService(
    IFileSystem fileSystem,
    IOptionsMonitor<DataFileOptions> dataFileOptions,
    ITextCorpusService textCorpusService
) : IScriptureDataFileService
{
    private readonly IFileSystem _fileSystem = fileSystem;
    private readonly IOptionsMonitor<DataFileOptions> _dataFileOptions = dataFileOptions;
    private readonly ITextCorpusService _textCorpusService = textCorpusService;

    public ParatextProjectSettings GetParatextProjectSettings(string filename, string[]? referenceFilenames = null)
    {
        referenceFilenames ??= [];
        using IZipContainer container = _fileSystem.OpenZipFile(GetFilePath(filename));
        if (
            _textCorpusService
                .GetParentProjectLocations([GetFilePath(filename), .. referenceFilenames.Select(GetFilePath)])
                .TryGetValue(filename, out string? parentLocation)
        )
        {
            using IZipContainer parentContainer = _fileSystem.OpenZipFile(parentLocation);
            return ParseProjectSettings(container, parentContainer);
        }
        return ParseProjectSettings(container);
    }

    public ZipParatextProjectTextUpdater GetZipParatextProjectTextUpdater(
        string filename,
        string[]? referenceFilenames = null
    )
    {
        referenceFilenames ??= [];
        using IZipContainer container = _fileSystem.OpenZipFile(GetFilePath(filename));
        if (
            _textCorpusService
                .GetParentProjectLocations([GetFilePath(filename), .. referenceFilenames.Select(GetFilePath)])
                .TryGetValue(filename, out string? parentLocation)
        )
        {
            using IZipContainer parentContainer = _fileSystem.OpenZipFile(parentLocation);
            return new ZipParatextProjectTextUpdater(container, parentContainer);
        }
        return new ZipParatextProjectTextUpdater(container);
    }

    private string GetFilePath(string filename)
    {
        return Path.Combine(_dataFileOptions.CurrentValue.FilesDirectory, filename);
    }

    private static ParatextProjectSettings ParseProjectSettings(
        IZipContainer container,
        IZipContainer? parentContainer = null
    )
    {
        ZipParatextProjectSettingsParser settingsParser = new(container, parentContainer);
        return settingsParser.Parse();
    }
}
