namespace Serval.Translation.Services;

[TestFixture]
public class PretranslationServiceTests
{
    [Test]
    [TestCase(PretranslationUsfmTextOrigin.PreferPretranslated, "OnlyPretranslated")]
    [TestCase(PretranslationUsfmTextOrigin.PreferExisting, "OnlyPretranslated")]
    [TestCase(PretranslationUsfmTextOrigin.OnlyPretranslated, "OnlyPretranslated")]
    [TestCase(PretranslationUsfmTextOrigin.OnlyExisting, "Blank")]
    public async Task GetUsfmAsync_SourceBook(PretranslationUsfmTextOrigin textOrigin, string returnUsfmType)
    {
        TestEnvironment env = new();
        string usfm = await env.Service.GetUsfmAsync("engine1", 1, "corpus1", "MAT", textOrigin: textOrigin);
        Assert.That(usfm.Replace("\r\n", "\n"), Is.EqualTo(TestEnvironment.GetUsfm(returnUsfmType, id: "MAT - TRG")));
    }

    [Test]
    [TestCase(PretranslationUsfmTextOrigin.PreferPretranslated, "PreferPretranslated")]
    [TestCase(PretranslationUsfmTextOrigin.PreferExisting, "PreferExisting")]
    [TestCase(PretranslationUsfmTextOrigin.OnlyPretranslated, "OnlyPretranslated")]
    [TestCase(PretranslationUsfmTextOrigin.OnlyExisting, "OnlyExisting")]
    public async Task GetUsfmAsync_TargetBook(PretranslationUsfmTextOrigin textOrigin, string returnUsfmType)
    {
        TestEnvironment env = new();
        env.AddMatthewToTarget();
        string usfm = await env.Service.GetUsfmAsync("engine1", 1, "corpus1", "MAT", textOrigin: textOrigin);
        Assert.That(usfm.Replace("\r\n", "\n"), Is.EqualTo(TestEnvironment.GetUsfm(returnUsfmType, id: "MAT - TRG")));
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
                .Returns(Task.FromResult<string?>(CreateExisting(book: "MAT", id: "MAT - SRC")));
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
                .Returns(Task.FromResult<string?>(CreateExisting(book: "MAT", id: "MAT - TRG")));
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

        private static string CreateExisting(string book = "MAT", string id = "MAT - TRG")
        {
            return $@"\id {id}
\h {Canon.BookIdToEnglishName(book)}
\c 1
\p
\v 1 Chapter one, verse one.
\v 2
\c 2
\p
\v 1 Chapter two, verse one.
\v 2 Chapter two, verse two.
";
        }

        private static string CreatePretranslationsOnly(string id = "MAT - TRG")
        {
            return $@"\id {id}
\h
\c 1
\p
\v 1 Chapter 1, verse 1.
\v 2 Chapter 1, verse 2.
\c 2
\p
\v 1 Chapter 2, verse 1.
\v 2
";
        }

        private static string CreatePreferPretranslations(string book = "MAT", string id = "MAT - TRG")
        {
            return $@"\id {id}
\h {Canon.BookIdToEnglishName(book)}
\c 1
\p
\v 1 Chapter 1, verse 1.
\v 2 Chapter 1, verse 2.
\c 2
\p
\v 1 Chapter 2, verse 1.
\v 2 Chapter two, verse two.
";
        }

        private static string CreatePreferExisting(string book = "MAT", string id = "MAT - TRG")
        {
            return $@"\id {id}
\h {Canon.BookIdToEnglishName(book)}
\c 1
\p
\v 1 Chapter one, verse one.
\v 2 Chapter 1, verse 2.
\c 2
\p
\v 1 Chapter two, verse one.
\v 2 Chapter two, verse two.
";
        }

        private static string CreateBlank(string id = "MAT - TRG")
        {
            return $@"\id {id}
\h
\c 1
\p
\v 1
\v 2
\c 2
\p
\v 1
\v 2
";
        }

        public static string GetUsfm(string type, string book = "MAT", string id = "MAT - TRG")
        {
            string usfm = type switch
            {
                "OnlyPretranslated" => CreatePretranslationsOnly(id),
                "PreferPretranslated" => CreatePreferPretranslations(book, id),
                "PreferExisting" => CreatePreferExisting(book, id),
                "OnlyExisting" => CreateExisting(book, id),
                "Blank" => CreateBlank(id),
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
            return usfm.Replace("\r\n", "\n");
        }
    }
}
