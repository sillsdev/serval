namespace Serval.Translation.Services;

[TestFixture]
public class PretranslationServiceTests
{
    [Test]
    public async Task GetUsfmAsync_SourceBook()
    {
        TestEnvironment env = new();
        string usfm = await env.Service.GetUsfmAsync("engine1", 1, "corpus1", "MAT");
        Assert.That(
            usfm.Replace("\r\n", "\n"),
            Is.EqualTo(
                @"\id MAT - TRG
\h
\c 1
\p
\v 1 Chapter 1, verse 1.
\v 2 Chapter 1, verse 2.
\c 2
\p
\v 1 Chapter 2, verse 1.
\v 2
".Replace("\r\n", "\n")
            )
        );
    }

    [Test]
    public async Task GetUsfmAsync_TargetBook()
    {
        TestEnvironment env = new();
        env.AddMatthewToTarget();
        string usfm = await env.Service.GetUsfmAsync("engine1", 1, "corpus1", "MAT");
        Assert.That(
            usfm.Replace("\r\n", "\n"),
            Is.EqualTo(
                @"\id MAT - TRG
\h Matthew
\c 1
\p
\v 1 Chapter 1, verse 1.
\v 2 Chapter 1, verse 2.
\c 2
\p
\v 1 Chapter 2, verse 1.
\v 2 Chapter two, verse two.
".Replace("\r\n", "\n")
            )
        );
    }

    private class TestEnvironment
    {
        public TestEnvironment()
        {
            Engines = new MemoryRepository<Engine>(
                [
                    new()
                    {
                        Id = "engine1",
                        Owner = "owner1",
                        SourceLanguage = "en",
                        TargetLanguage = "en",
                        Type = "nmt",
                        ModelRevision = 1,
                        Corpora =
                        [
                            new()
                            {
                                Id = "corpus1",
                                SourceLanguage = "en",
                                TargetLanguage = "en",
                                SourceFiles =
                                [
                                    new()
                                    {
                                        Id = "file1",
                                        Filename = "file1.zip",
                                        Format = Shared.Contracts.FileFormat.Paratext,
                                        TextId = "project1"
                                    }
                                ],
                                TargetFiles =
                                [
                                    new()
                                    {
                                        Id = "file2",
                                        Filename = "file2.zip",
                                        Format = Shared.Contracts.FileFormat.Paratext,
                                        TextId = "project1"
                                    }
                                ],
                            }
                        ]
                    }
                ]
            );

            Pretranslations = new MemoryRepository<Pretranslation>(
                [
                    new()
                    {
                        Id = "pt1",
                        EngineRef = "engine1",
                        ModelRevision = 1,
                        CorpusRef = "corpus1",
                        TextId = "MAT",
                        Refs = ["MAT 1:1"],
                        Translation = "Chapter 1, verse 1."
                    },
                    new()
                    {
                        Id = "pt2",
                        EngineRef = "engine1",
                        ModelRevision = 1,
                        CorpusRef = "corpus1",
                        TextId = "MAT",
                        Refs = ["MAT 1:2"],
                        Translation = "Chapter 1, verse 2."
                    },
                    new()
                    {
                        Id = "pt3",
                        EngineRef = "engine1",
                        ModelRevision = 1,
                        CorpusRef = "corpus1",
                        TextId = "MAT",
                        Refs = ["MAT 2:1"],
                        Translation = "Chapter 2, verse 1."
                    }
                ]
            );
            ScriptureDataFileService = Substitute.For<IScriptureDataFileService>();
            ScriptureDataFileService.GetParatextProjectSettings("file1.zip").Returns(CreateProjectSettings("SRC"));
            ScriptureDataFileService.GetParatextProjectSettings("file2.zip").Returns(CreateProjectSettings("TRG"));
            ScriptureDataFileService
                .ReadParatextProjectBookAsync("file1.zip", "MAT")
                .Returns(Task.FromResult<string?>(CreateUsfm("SRC", "MAT")));
            ScriptureDataFileService
                .ReadParatextProjectBookAsync("file2.zip", "MAT")
                .Returns(Task.FromResult<string?>(null));
            Service = new PretranslationService(Pretranslations, Engines, ScriptureDataFileService);
        }

        public PretranslationService Service { get; }
        public MemoryRepository<Pretranslation> Pretranslations { get; }
        public MemoryRepository<Engine> Engines { get; }
        public IScriptureDataFileService ScriptureDataFileService { get; }

        public void AddMatthewToTarget()
        {
            ScriptureDataFileService
                .ReadParatextProjectBookAsync("file2.zip", "MAT")
                .Returns(Task.FromResult<string?>(CreateUsfm("TRG", "MAT")));
        }

        private static ParatextProjectSettings CreateProjectSettings(string name)
        {
            return new ParatextProjectSettings(
                name: name,
                fullName: name,
                encoding: Encoding.UTF8,
                versification: ScrVers.English,
                stylesheet: new UsfmStylesheet("usfm.sty"),
                fileNamePrefix: "",
                fileNameForm: "MAT",
                fileNameSuffix: $"{name}.SFM",
                biblicalTermsListType: "Major",
                biblicalTermsProjectName: "",
                biblicalTermsFileName: "BiblicalTerms.xml"
            );
        }

        private static string CreateUsfm(string name, string book)
        {
            return $@"\id {book} - {name}
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
        }
    }
}
