using SIL.IO;

namespace Serval.Shared.Services;

public class ZipParatextProjectFileHandler(IZipContainer container) : IParatextProjectFileHandler
{
    private readonly IZipContainer _projectContainer = container;

    public UsfmStylesheet CreateStylesheet(string fileName)
    {
        TempFile? stylesheetTempFile = null;
        TempFile? customStylesheetTempFile = null;
        try
        {
            string stylesheetPath = fileName;
            if (Exists(fileName))
            {
                stylesheetTempFile = TempFile.CreateAndGetPathButDontMakeTheFile();
                using (Stream source = Open(fileName))
                using (Stream target = File.OpenWrite(stylesheetTempFile.Path))
                {
                    source.CopyTo(target);
                }
                stylesheetPath = stylesheetTempFile.Path;
            }

            string? customStylesheetPath = null;
            if (Exists("custom.sty"))
            {
                customStylesheetTempFile = TempFile.CreateAndGetPathButDontMakeTheFile();
                using (Stream source = Open("custom.sty"))
                using (Stream target = File.OpenWrite(customStylesheetTempFile.Path))
                {
                    source.CopyTo(target);
                }
                customStylesheetPath = customStylesheetTempFile.Path;
            }
            return new UsfmStylesheet(stylesheetPath, customStylesheetPath);
        }
        finally
        {
            stylesheetTempFile?.Dispose();
            customStylesheetTempFile?.Dispose();
        }
    }

    public bool Exists(string fileName)
    {
        return _projectContainer.EntryExists(fileName);
    }

    public Stream Open(string fileName)
    {
        return _projectContainer.OpenEntry(fileName);
    }

    public string? Find(string extension)
    {
        return _projectContainer.Entries.FirstOrDefault(e => e.EndsWith(extension));
    }
}
