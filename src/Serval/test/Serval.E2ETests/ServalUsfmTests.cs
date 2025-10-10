using SIL.Machine.Corpora;

namespace Serval.E2ETests;

[TestFixture]
public class ServalUsfmTests
{
    public static readonly string PretranslationPath = Path.Combine("..", "..", "..", "data", "pretranslations.json");
    public static readonly string ParatextProjectPath = Path.Combine("..", "..", "..", "data", "project");

    [Test]
    [Ignore("This is for manual testing only.  Remove this tag to run the test.")]
    /*
   In order to run this test on specific projects, place the Paratext projects or Paratext project zips in the Corpora/TestData/project/ folder.
   If only testing one project, you can instead place the project in the Corpora/TestData/ folder and rename it to "project"
   */
    public async Task CreateUsfmFile()
    {
        async Task GetUsfmAsync(string projectPath)
        {
            ParatextProjectSettingsParserBase parser;
            ZipArchive? projectArchive = null;
            try
            {
                projectArchive = ZipFile.Open(projectPath, ZipArchiveMode.Read);
                parser = new ZipParatextProjectSettingsParser(projectArchive);
            }
            catch (UnauthorizedAccessException)
            {
                parser = new FileParatextProjectSettingsParser(projectPath);
            }
            ParatextProjectSettings settings = parser.Parse();

            // Read text from pretranslations file
            using Stream pretranslationStream = File.OpenRead(PretranslationPath);
            UpdateUsfmRow[] pretranslations = await JsonSerializer
                .DeserializeAsyncEnumerable<Pretranslation>(
                    pretranslationStream,
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
                )
                .Select(p => new UpdateUsfmRow(
                    (IReadOnlyList<ScriptureRef>)(
                        p?.Refs.Select(r => ScriptureRef.Parse(r, settings.Versification).ToRelaxed()).ToArray() ?? []
                    ),
                    p?.Translation ?? ""
                ))
                .ToArrayAsync();
            List<string> bookIds = [];
            ParatextProjectTextUpdaterBase updater;
            if (projectArchive == null)
            {
                bookIds = (
                    Directory
                        .EnumerateFiles(projectPath, $"{settings.FileNamePrefix}*{settings.FileNameSuffix}")
                        .Select(path => new DirectoryInfo(path).Name)
                        .Select(filename =>
                        {
                            string bookId;
                            if (settings.IsBookFileName(filename, out bookId))
                                return bookId;
                            else
                                return "";
                        })
                        .Where(id => id != "")
                ).ToList();
                updater = new FileParatextProjectTextUpdater(projectPath);
            }
            else
            {
                bookIds = projectArchive
                    .Entries.Where(e =>
                        e.Name.StartsWith(settings.FileNamePrefix) && e.Name.EndsWith(settings.FileNameSuffix)
                    )
                    .Select(e =>
                    {
                        string bookId;
                        if (settings.IsBookFileName(e.Name, out bookId))
                            return bookId;
                        else
                            return "";
                    })
                    .Where(id => id != "")
                    .ToList();
                updater = new ZipParatextProjectTextUpdater(projectArchive);
            }
            foreach (string bookId in bookIds)
            {
                string newUsfm = updater.UpdateUsfm(
                    bookId,
                    pretranslations,
                    textBehavior: UpdateUsfmTextBehavior.StripExisting
                );
                Assert.That(newUsfm, Is.Not.Null);
            }
        }
        if (!File.Exists(Path.Combine(ParatextProjectPath, "Settings.xml")))
        {
            Assert.Multiple(() =>
            {
                foreach (string subdir in Directory.EnumerateFiles(ParatextProjectPath))
                    Assert.DoesNotThrowAsync(async () => await GetUsfmAsync(subdir), $"Failed to parse {subdir}");
            });
        }
        else
        {
            await GetUsfmAsync(ParatextProjectPath);
        }
    }
}
