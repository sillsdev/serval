namespace Serval.Shared.Services;

public class ScriptureDataFileService(IFileSystem fileSystem, IOptionsMonitor<DataFileOptions> dataFileOptions)
    : IScriptureDataFileService
{
    private readonly IFileSystem _fileSystem = fileSystem;
    private readonly IOptionsMonitor<DataFileOptions> _dataFileOptions = dataFileOptions;

    public ParatextProjectSettings GetParatextProjectSettings(string filename)
    {
        using IZipContainer container = _fileSystem.OpenZipFile(GetFilePath(filename));
        ZipParatextProjectSettingsParser settingsParser = new(_fileSystem, container);
        return settingsParser.Parse();
    }

    public async Task<string?> ReadParatextProjectBookAsync(string filename, string book)
    {
        using IZipContainer container = _fileSystem.OpenZipFile(GetFilePath(filename));
        ParatextProjectSettings settings = ParseProjectSettings(container);
        string entryName = GetBookFileName(settings, book);
        if (!container.EntryExists(entryName))
            return null;
        using StreamReader reader = new(container.OpenEntry(entryName));
        return await reader.ReadToEndAsync();
    }

    private string GetFilePath(string filename)
    {
        return Path.Combine(_dataFileOptions.CurrentValue.FilesDirectory, filename);
    }

    private ParatextProjectSettings ParseProjectSettings(IZipContainer container)
    {
        ZipParatextProjectSettingsParser settingsParser = new(_fileSystem, container);
        return settingsParser.Parse();
    }

    private static string GetBookFileName(ParatextProjectSettings settings, string bookId)
    {
        string bookPart;
        if (settings.FileNameForm == "MAT")
            bookPart = bookId;
        else if (settings.FileNameForm == "40" || settings.FileNameForm == "41")
            bookPart = GetBookFileNameDigits(bookId);
        else
            bookPart = GetBookFileNameDigits(bookId) + bookId;
        return settings.FileNamePrefix + bookPart + settings.FileNameSuffix;
    }

    private static string GetBookFileNameDigits(string bookId)
    {
        int bookNum = Canon.BookIdToNumber(bookId);

        if (bookNum < 10)
            return "0" + bookNum;
        if (bookNum < 40)
            return bookNum.ToString(CultureInfo.InvariantCulture);
        if (bookNum < 100)
            return (bookNum + 1).ToString(CultureInfo.InvariantCulture);
        if (bookNum < 110)
            return "A" + (bookNum - 100);
        if (bookNum < 120)
            return "B" + (bookNum - 110);
        return "C" + (bookNum - 120);
    }
}
