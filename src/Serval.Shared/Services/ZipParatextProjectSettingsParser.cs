namespace Serval.Shared.Services;

public class ZipParatextProjectSettingsParser(IFileSystem fileSystem, IZipContainer projectContainer)
    : ParatextProjectSettingsParserBase
{
    private readonly IFileSystem _fileSystem = fileSystem;
    private readonly IZipContainer _projectContainer = projectContainer;

    protected override UsfmStylesheet CreateStylesheet(string fileName)
    {
        string? stylesheetTempFileName = null;
        string? customStylesheetTempFileName = null;
        try
        {
            string stylesheetFileName = fileName;
            if (_projectContainer.EntryExists(fileName))
            {
                stylesheetTempFileName = CreateRandomTempFileName();
                using (Stream source = _projectContainer.OpenEntry(fileName))
                using (Stream target = _fileSystem.OpenWrite(stylesheetTempFileName))
                {
                    source.CopyTo(target);
                }
                stylesheetFileName = stylesheetTempFileName;
            }

            string? customStylesheetFileName = null;
            if (_projectContainer.EntryExists("custom.sty"))
            {
                customStylesheetTempFileName = CreateRandomTempFileName();
                using (Stream source = _projectContainer.OpenEntry("custom.sty"))
                using (Stream target = _fileSystem.OpenWrite(customStylesheetTempFileName))
                {
                    source.CopyTo(target);
                }
                customStylesheetFileName = customStylesheetTempFileName;
            }
            return new UsfmStylesheet(stylesheetFileName, customStylesheetFileName);
        }
        finally
        {
            if (stylesheetTempFileName is not null)
                _fileSystem.DeleteFile(stylesheetTempFileName);
            if (customStylesheetTempFileName is not null)
                _fileSystem.DeleteFile(customStylesheetTempFileName);
        }
    }

    protected override bool Exists(string fileName)
    {
        return _projectContainer.EntryExists(fileName);
    }

    protected override string? Find(string extension)
    {
        return _projectContainer.Entries.FirstOrDefault(e => e.EndsWith(extension));
    }

    protected override Stream Open(string fileName)
    {
        return _projectContainer.OpenEntry(fileName);
    }

    private static string CreateRandomTempFileName()
    {
        return Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    }
}
