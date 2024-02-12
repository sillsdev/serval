namespace Serval.Shared.Services;

[TestFixture]
public class ScriptureDataFileServiceTests
{
    [Test]
    public void GetParatextProjectSettings()
    {
        TestEnvironment env = new();
        ParatextProjectSettings settings = env.Service.GetParatextProjectSettings("file1.zip");
        Assert.That(settings.Name, Is.EqualTo("PROJ"));
    }

    [Test]
    public async Task ReadParatextProjectBookAsync_Exists()
    {
        TestEnvironment env = new();
        string? usfm = await env.Service.ReadParatextProjectBookAsync("file1.zip", "MAT");
        Assert.That(usfm, Is.Not.Null);
        Assert.That(
            usfm.Replace("\r\n", "\n"),
            Is.EqualTo(
                @"\id MAT - PROJ
\h Matthew
\c 1
\p
\v 1 Chapter one, verse one.
\v 2 Chapter one, verse two.
\c 2
\p
\v 1 Chapter two, verse one.
\v 2 Chapter two, verse two.
".Replace("\r\n", "\n")
            )
        );
    }

    [Test]
    public async Task ReadParatextProjectBookAsync_DoesNotExist()
    {
        TestEnvironment env = new();
        string? usfm = await env.Service.ReadParatextProjectBookAsync("file1.zip", "MRK");
        Assert.That(usfm, Is.Null);
    }

    private class TestEnvironment
    {
        public TestEnvironment()
        {
            IFileSystem fileSystem = Substitute.For<IFileSystem>();
            fileSystem
                .OpenZipFile("file1.zip")
                .Returns(ci =>
                {
                    IZipContainer container = CreateZipContainer();
                    AddBook(container, "MAT");
                    return container;
                });
            IOptionsMonitor<DataFileOptions> dataFileOptions = Substitute.For<IOptionsMonitor<DataFileOptions>>();
            dataFileOptions.CurrentValue.Returns(new DataFileOptions());

            Service = new ScriptureDataFileService(fileSystem, dataFileOptions);
        }

        public ScriptureDataFileService Service { get; }

        private static IZipContainer CreateZipContainer()
        {
            IZipContainer container = Substitute.For<IZipContainer>();
            container.EntryExists("Settings.xml").Returns(true);
            XElement settingsXml =
                new(
                    "ScriptureText",
                    new XElement("StyleSheet", "usfm.sty"),
                    new XElement("Name", "PROJ"),
                    new XElement("FullName", "PROJ"),
                    new XElement("Encoding", "65001"),
                    new XElement(
                        "Naming",
                        new XAttribute("PrePart", ""),
                        new XAttribute("PostPart", "PROJ.SFM"),
                        new XAttribute("BookNameForm", "MAT")
                    ),
                    new XElement("BiblicalTermsListSetting", "Major::BiblicalTerms.xml")
                );
            container
                .OpenEntry("Settings.xml")
                .Returns(new MemoryStream(Encoding.UTF8.GetBytes(settingsXml.ToString())));
            container.EntryExists("custom.vrs").Returns(false);
            container.EntryExists("usfm.sty").Returns(false);
            container.EntryExists("custom.sty").Returns(false);
            return container;
        }

        private static void AddBook(IZipContainer container, string book)
        {
            string bookFileName = $"{book}PROJ.SFM";
            container.EntryExists(bookFileName).Returns(true);
            string usfm =
                $@"\id {book} - PROJ
\h {Canon.BookIdToEnglishName(book)}
\c 1
\p
\v 1 Chapter one, verse one.
\v 2 Chapter one, verse two.
\c 2
\p
\v 1 Chapter two, verse one.
\v 2 Chapter two, verse two.
";
            container.OpenEntry(bookFileName).Returns(new MemoryStream(Encoding.UTF8.GetBytes(usfm)));
        }
    }
}
