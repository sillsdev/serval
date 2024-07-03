namespace Serval.Translation.Services;

[TestFixture]
public class PretranslationServiceTests
{
    public enum TemplateType
    {
        SourceOnly,
        TargetAndSource,
    }

    [Test]
    [TestCase(PretranslationUsfmTextOrigin.PreferPretranslated, "OnlyPretranslated")]
    [TestCase(PretranslationUsfmTextOrigin.PreferExisting, "OnlyPretranslated")]
    [TestCase(PretranslationUsfmTextOrigin.OnlyPretranslated, "OnlyPretranslated")]
    [TestCase(PretranslationUsfmTextOrigin.UseSourceUsfm, "OnlyPretranslated")]
    [TestCase(PretranslationUsfmTextOrigin.OnlyExisting, "Blank")]
    public async Task GetUsfmAsync_SourceOnly(PretranslationUsfmTextOrigin textOrigin, string returnUsfmType)
    {
        TestEnvironment env = new();
        string usfm = await env.Service.GetUsfmAsync("engine1", 1, "corpus1", "MAT", textOrigin: textOrigin);
        Assert.That(
            usfm.Replace("\r\n", "\n"),
            Is.EqualTo(TestEnvironment.GetUsfmTruth(returnUsfmType, TemplateType.SourceOnly))
        );
    }

    [Test]
    [TestCase(PretranslationUsfmTextOrigin.PreferPretranslated, "PreferPretranslated", TemplateType.TargetAndSource)]
    [TestCase(PretranslationUsfmTextOrigin.PreferExisting, "PreferExisting", TemplateType.TargetAndSource)]
    [TestCase(PretranslationUsfmTextOrigin.OnlyPretranslated, "OnlyPretranslated", TemplateType.TargetAndSource)]
    [TestCase(PretranslationUsfmTextOrigin.OnlyExisting, "OnlyExisting", TemplateType.TargetAndSource)]
    [TestCase(PretranslationUsfmTextOrigin.UseSourceUsfm, "OnlyPretranslated", TemplateType.SourceOnly)]
    public async Task GetUsfmAsync_SourceAndTarget(
        PretranslationUsfmTextOrigin textOrigin,
        string returnUsfmType,
        TemplateType templateType
    )
    {
        TestEnvironment env = new();
        env.AddMatthewToTarget();
        string usfm = await env.Service.GetUsfmAsync("engine1", 1, "corpus1", "MAT", textOrigin: textOrigin);
        Assert.That(usfm.Replace("\r\n", "\n"), Is.EqualTo(TestEnvironment.GetUsfmTruth(returnUsfmType, templateType)));
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
                        Refs = ["MAT 2:1"],
                        Translation = "Chapter 2, verse 1."
                    },
                    new()
                    {
                        Id = "pt3",
                        EngineRef = "engine1",
                        ModelRevision = 1,
                        CorpusRef = "corpus1",
                        TextId = "MAT",
                        Refs = ["MAT 3:1"],
                        Translation = "Chapter 3, verse 1."
                    }
                ]
            );
            ScriptureDataFileService = Substitute.For<IScriptureDataFileService>();
            ScriptureDataFileService.GetParatextProjectSettings("file1.zip").Returns(CreateProjectSettings("SRC"));
            ScriptureDataFileService.GetParatextProjectSettings("file2.zip").Returns(CreateProjectSettings("TRG"));
            ScriptureDataFileService
                .ReadParatextProjectBookAsync("file1.zip", "MAT")
                .Returns(Task.FromResult<string?>(CreateExistingSource(book: "MAT", id: "MAT - SRC")));
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
                .Returns(Task.FromResult<string?>(CreateExistingTarget(book: "MAT", id: "MAT - TRG")));
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

        private static string CreateExistingSource(string book = "MAT", string id = "MAT - SRC")
        {
            return $@"\id {id}
\h {Canon.BookIdToEnglishName(book)}
\im comment
\ip
\c 1
\p
\v 1 SRC - Chapter one, verse one.
\v 2 SRC - Chapter one, verse two.
\v 3 SRC - Chapter one, verse three.
\c 2
\p
\v 1 SRC - Chapter two, verse one.
\v 2
\c 3
\p
\v 1
";
        }

        private static string CreateExistingTarget(string book = "MAT", string id = "MAT - TRG")
        {
            return $@"\id {id}
\h {Canon.BookIdToEnglishName(book)}
\s1 section
\s2
\c 1
\p
\v 1 TRG - Chapter one, verse one.
\v 2
\c 2
\p
\v 1 TRG - Chapter two, verse one.
\v 2 TRG - Chapter two, verse two.
\v 3 TRG - Chapter two, verse three.
\c 3
\p
\v 1
";
        }

        private static string StripContent(string usfm)
        {
            string temp = Regex.Replace(usfm, @"(\\v \d+)(.*)", "$1");
            return Regex.Replace(temp, @"\\(h|s1|im)(.*)", @"\$1");
        }

        private static string InsertPretranslationsIfBlank(string usfm)
        {
            return Regex.Replace(usfm, @"\\c (\d+)\n\\p\n\\v 1\n", "\\c $1\n\\p\n\\v 1 Chapter $1, verse 1.\n");
        }

        private static string InsertPretranslationsIfBlankOfFull(string usfm)
        {
            return Regex.Replace(usfm, @"\\c (\d+)\n\\p\n\\v 1(.*)\n", "\\c $1\n\\p\n\\v 1 Chapter $1, verse 1.\n");
        }

        public static string GetUsfmTruth(string type, TemplateType templateType, string book = "MAT")
        {
            string id = $"{book} - TRG";
            string usfm = templateType switch
            {
                TemplateType.SourceOnly => CreateExistingSource(book, id),
                TemplateType.TargetAndSource => CreateExistingTarget(book, id),
                _ => throw new ArgumentOutOfRangeException(nameof(templateType), templateType, null)
            };
            usfm = usfm.Replace("\r\n", "\n");
            usfm = type switch
            {
                "OnlyPretranslated" => InsertPretranslationsIfBlank(StripContent(usfm)),
                "PreferPretranslated" => InsertPretranslationsIfBlankOfFull(usfm),
                "PreferExisting" => InsertPretranslationsIfBlank(usfm),
                "OnlyExisting" => usfm,
                "Blank" => StripContent(usfm),
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
            return usfm;
        }
    }
}
