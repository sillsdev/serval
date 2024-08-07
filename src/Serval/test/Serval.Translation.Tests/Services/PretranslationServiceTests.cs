namespace Serval.Translation.Services;

[TestFixture]
public class PretranslationServiceTests
{
    private const string SourceUsfm =
        $@"\id MAT - SRC
\c 1
\v 1 SRC - Chapter one, verse one.
\v 2
\v 3 SRC - Chapter one, verse three.
";

    private const string TargetUsfm =
        @"\id MAT - TRG
\c 1
\v 1 TRG - Chapter one, verse one.
\v 2
\v 3 TRG - Chapter one, verse three.
";

    [Test]
    public async Task GetUsfmAsync_Source_PreferExisting()
    {
        TestEnvironment env = new();

        string usfm = await env.GetUsfmAsync(
            PretranslationUsfmTextOrigin.PreferExisting,
            PretranslationUsfmTemplate.Source
        );

        Assert.That(
            usfm,
            Is.EqualTo(
                    @"\id MAT - TRG
\c 1
\v 1 Chapter 1, verse 1.
\v 2 Chapter 1, verse 2.
\v 3
"
                )
                .IgnoreLineEndings()
        );
    }

    [Test]
    public async Task GetUsfmAsync_Source_PreferPretranslated()
    {
        TestEnvironment env = new();

        string usfm = await env.GetUsfmAsync(
            PretranslationUsfmTextOrigin.PreferPretranslated,
            PretranslationUsfmTemplate.Source
        );

        Assert.That(
            usfm,
            Is.EqualTo(
                    @"\id MAT - TRG
\c 1
\v 1 Chapter 1, verse 1.
\v 2 Chapter 1, verse 2.
\v 3
"
                )
                .IgnoreLineEndings()
        );
    }

    [Test]
    public async Task GetUsfmAsync_Source_OnlyExisting()
    {
        TestEnvironment env = new();

        string usfm = await env.GetUsfmAsync(
            PretranslationUsfmTextOrigin.OnlyExisting,
            PretranslationUsfmTemplate.Source
        );

        Assert.That(
            usfm,
            Is.EqualTo(
                    @"\id MAT - TRG
\c 1
\v 1
\v 2
\v 3
"
                )
                .IgnoreLineEndings()
        );
    }

    [Test]
    public async Task GetUsfmAsync_Source_OnlyPretranslated()
    {
        TestEnvironment env = new();

        string usfm = await env.GetUsfmAsync(
            PretranslationUsfmTextOrigin.OnlyPretranslated,
            PretranslationUsfmTemplate.Source
        );

        Assert.That(
            usfm,
            Is.EqualTo(
                    @"\id MAT - TRG
\c 1
\v 1 Chapter 1, verse 1.
\v 2 Chapter 1, verse 2.
\v 3
"
                )
                .IgnoreLineEndings()
        );
    }

    [Test]
    public async Task GetUsfmAsync_Target_PreferExisting()
    {
        TestEnvironment env = new();
        env.AddMatthewToTarget();

        string usfm = await env.GetUsfmAsync(
            PretranslationUsfmTextOrigin.PreferExisting,
            PretranslationUsfmTemplate.Target
        );

        Assert.That(
            usfm,
            Is.EqualTo(
                    @"\id MAT - TRG
\c 1
\v 1 TRG - Chapter one, verse one.
\v 2 Chapter 1, verse 2.
\v 3 TRG - Chapter one, verse three.
"
                )
                .IgnoreLineEndings()
        );
    }

    [Test]
    public async Task GetUsfmAsync_Target_PreferPretranslated()
    {
        TestEnvironment env = new();
        env.AddMatthewToTarget();

        string usfm = await env.GetUsfmAsync(
            PretranslationUsfmTextOrigin.PreferPretranslated,
            PretranslationUsfmTemplate.Target
        );

        Assert.That(
            usfm,
            Is.EqualTo(
                    @"\id MAT - TRG
\c 1
\v 1 Chapter 1, verse 1.
\v 2 Chapter 1, verse 2.
\v 3 TRG - Chapter one, verse three.
"
                )
                .IgnoreLineEndings()
        );
    }

    [Test]
    public async Task GetUsfmAsync_Target_TargetBookDoesNotExist()
    {
        TestEnvironment env = new();

        string usfm = await env.GetUsfmAsync(
            PretranslationUsfmTextOrigin.PreferPretranslated,
            PretranslationUsfmTemplate.Target
        );

        Assert.That(usfm, Is.EqualTo(""));
    }

    [Test]
    public async Task GetUsfmAsync_Auto_TargetBookDoesNotExist()
    {
        TestEnvironment env = new();

        string usfm = await env.GetUsfmAsync(
            PretranslationUsfmTextOrigin.PreferPretranslated,
            PretranslationUsfmTemplate.Auto
        );

        Assert.That(
            usfm,
            Is.EqualTo(
                    @"\id MAT - TRG
\c 1
\v 1 Chapter 1, verse 1.
\v 2 Chapter 1, verse 2.
\v 3
"
                )
                .IgnoreLineEndings()
        );
    }

    [Test]
    public async Task GetUsfmAsync_Auto_TargetBookExists()
    {
        TestEnvironment env = new();
        env.AddMatthewToTarget();

        string usfm = await env.GetUsfmAsync(
            PretranslationUsfmTextOrigin.PreferPretranslated,
            PretranslationUsfmTemplate.Auto
        );

        Assert.That(
            usfm,
            Is.EqualTo(
                    @"\id MAT - TRG
\c 1
\v 1 Chapter 1, verse 1.
\v 2 Chapter 1, verse 2.
\v 3 TRG - Chapter one, verse three.
"
                )
                .IgnoreLineEndings()
        );
    }

    [Test]
    public async Task GetUsfmAsync_Target_OnlyExisting()
    {
        TestEnvironment env = new();
        env.AddMatthewToTarget();

        string usfm = await env.GetUsfmAsync(
            PretranslationUsfmTextOrigin.OnlyExisting,
            PretranslationUsfmTemplate.Target
        );

        Assert.That(usfm, Is.EqualTo(TargetUsfm).IgnoreLineEndings());
    }

    [Test]
    public async Task GetUsfmAsync_Target_OnlyPretranslated()
    {
        TestEnvironment env = new();
        env.AddMatthewToTarget();

        string usfm = await env.GetUsfmAsync(
            PretranslationUsfmTextOrigin.OnlyPretranslated,
            PretranslationUsfmTemplate.Target
        );

        Assert.That(
            usfm,
            Is.EqualTo(
                    @"\id MAT - TRG
\c 1
\v 1 Chapter 1, verse 1.
\v 2 Chapter 1, verse 2.
\v 3
"
                )
                .IgnoreLineEndings()
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
                    }
                ]
            );
            ScriptureDataFileService = Substitute.For<IScriptureDataFileService>();
            ScriptureDataFileService.GetParatextProjectSettings("file1.zip").Returns(CreateProjectSettings("SRC"));
            ScriptureDataFileService.GetParatextProjectSettings("file2.zip").Returns(CreateProjectSettings("TRG"));
            var zipSubstituteSource = Substitute.For<IZipContainer>();
            var zipSubstituteTarget = Substitute.For<IZipContainer>();
            zipSubstituteSource.OpenEntry("MATSRC.SFM").Returns(new MemoryStream(Encoding.UTF8.GetBytes(SourceUsfm)));
            zipSubstituteTarget.OpenEntry("MATTRG.SFM").Returns(new MemoryStream(Encoding.UTF8.GetBytes("")));
            zipSubstituteSource.EntryExists(Arg.Any<string>()).Returns(false);
            zipSubstituteTarget.EntryExists(Arg.Any<string>()).Returns(false);
            zipSubstituteSource.EntryExists("MATSRC.SFM").Returns(true);
            zipSubstituteTarget.EntryExists("MATTRG.SFM").Returns(true);
            TargetZipContainer = zipSubstituteTarget;
            using var textUpdaterSource = new Shared.Services.ZipParatextProjectTextUpdater(
                zipSubstituteSource,
                CreateProjectSettings("SRC")
            );
            using var textUpdaterTarget = new Shared.Services.ZipParatextProjectTextUpdater(
                zipSubstituteTarget,
                CreateProjectSettings("TRG")
            );
            ScriptureDataFileService.GetZipParatextProjectTextUpdater("file1.zip").Returns(textUpdaterSource);
            ScriptureDataFileService.GetZipParatextProjectTextUpdater("file2.zip").Returns(textUpdaterTarget);
            Service = new PretranslationService(Pretranslations, Engines, ScriptureDataFileService);
        }

        public PretranslationService Service { get; }
        public MemoryRepository<Pretranslation> Pretranslations { get; }
        public MemoryRepository<Engine> Engines { get; }
        public IScriptureDataFileService ScriptureDataFileService { get; }
        public IZipContainer TargetZipContainer { get; }

        public async Task<string> GetUsfmAsync(
            PretranslationUsfmTextOrigin textOrigin,
            PretranslationUsfmTemplate template
        )
        {
            string usfm = await Service.GetUsfmAsync(
                engineId: "engine1",
                modelRevision: 1,
                corpusId: "corpus1",
                textId: "MAT",
                textOrigin: textOrigin,
                template: template
            );
            return usfm.Replace("\r\n", "\n");
        }

        public void AddMatthewToTarget()
        {
            TargetZipContainer.OpenEntry("MATTRG.SFM").Returns(new MemoryStream(Encoding.UTF8.GetBytes(TargetUsfm)));
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
    }
}
