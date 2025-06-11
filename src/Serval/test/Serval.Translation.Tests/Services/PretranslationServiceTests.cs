namespace Serval.Translation.Services;

[TestFixture]
public class PretranslationServiceTests
{
    private const string SourceUsfm =
        $@"\id MAT - SRC
\c 1
\v 1 SRC - Chapter one, verse one.
\p new paragraph
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
        using TestEnvironment env = new();

        string usfm = await env.GetUsfmAsync(
            PretranslationUsfmTextOrigin.PreferExisting,
            PretranslationUsfmTemplate.Source
        );

        Assert.That(
            usfm,
            Is.EqualTo(
                    @"\id MAT - TRG
\rem This draft of MAT was generated using AI on 1970-01-01 00:00:00Z. It should be reviewed and edited carefully.
\c 1
\v 1 Chapter 1, verse 1. Translated new paragraph
\p
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
        using TestEnvironment env = new();

        string usfm = await env.GetUsfmAsync(
            PretranslationUsfmTextOrigin.PreferPretranslated,
            PretranslationUsfmTemplate.Source
        );

        Assert.That(
            usfm,
            Is.EqualTo(
                    @"\id MAT - TRG
\rem This draft of MAT was generated using AI on 1970-01-01 00:00:00Z. It should be reviewed and edited carefully.
\c 1
\v 1 Chapter 1, verse 1. Translated new paragraph
\p
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
        using TestEnvironment env = new();

        string usfm = await env.GetUsfmAsync(
            PretranslationUsfmTextOrigin.OnlyExisting,
            PretranslationUsfmTemplate.Source
        );

        Assert.That(
            usfm,
            Is.EqualTo(
                    @"\id MAT - TRG
\rem This draft of MAT was generated using AI on 1970-01-01 00:00:00Z. It should be reviewed and edited carefully.
\c 1
\v 1
\p
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
        using TestEnvironment env = new();

        string usfm = await env.GetUsfmAsync(
            PretranslationUsfmTextOrigin.OnlyPretranslated,
            PretranslationUsfmTemplate.Source
        );

        Assert.That(
            usfm,
            Is.EqualTo(
                    @"\id MAT - TRG
\rem This draft of MAT was generated using AI on 1970-01-01 00:00:00Z. It should be reviewed and edited carefully.
\c 1
\v 1 Chapter 1, verse 1. Translated new paragraph
\p
\v 2 Chapter 1, verse 2.
\v 3
"
                )
                .IgnoreLineEndings()
        );
    }

    [Test]
    public async Task GetUsfmAsync_Source_PlaceMarkers()
    {
        using TestEnvironment env = new();

        string usfm = await env.GetUsfmAsync(
            PretranslationUsfmTextOrigin.OnlyPretranslated,
            PretranslationUsfmTemplate.Source,
            paragraphMarkerBehavior: PretranslationUsfmMarkerBehavior.PreservePosition
        );

        Assert.That(
            usfm,
            Is.EqualTo(
                    @"\id MAT - TRG
\rem This draft of MAT was generated using AI on 1970-01-01 00:00:00Z. It should be reviewed and edited carefully.
\c 1
\v 1 Chapter 1, verse 1.
\p Translated new paragraph
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
        using TestEnvironment env = new();
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
        using TestEnvironment env = new();
        env.AddMatthewToTarget();

        string usfm = await env.GetUsfmAsync(
            PretranslationUsfmTextOrigin.PreferPretranslated,
            PretranslationUsfmTemplate.Target
        );

        Assert.That(
            usfm,
            Is.EqualTo(
                    @"\id MAT - TRG
\rem This draft of MAT was generated using AI on 1970-01-01 00:00:00Z. It should be reviewed and edited carefully.
\c 1
\v 1 Chapter 1, verse 1. Translated new paragraph
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
        using TestEnvironment env = new();

        string usfm = await env.GetUsfmAsync(
            PretranslationUsfmTextOrigin.PreferPretranslated,
            PretranslationUsfmTemplate.Target
        );

        Assert.That(usfm, Is.EqualTo(""));
    }

    [Test]
    public async Task GetUsfmAsync_Auto_TargetBookDoesNotExist()
    {
        using TestEnvironment env = new();

        string usfm = await env.GetUsfmAsync(
            PretranslationUsfmTextOrigin.PreferPretranslated,
            PretranslationUsfmTemplate.Auto
        );

        Assert.That(
            usfm,
            Is.EqualTo(
                    @"\id MAT - TRG
\rem This draft of MAT was generated using AI on 1970-01-01 00:00:00Z. It should be reviewed and edited carefully.
\c 1
\v 1 Chapter 1, verse 1. Translated new paragraph
\p
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
        using TestEnvironment env = new();
        env.AddMatthewToTarget();

        string usfm = await env.GetUsfmAsync(
            PretranslationUsfmTextOrigin.PreferPretranslated,
            PretranslationUsfmTemplate.Auto
        );

        Assert.That(
            usfm,
            Is.EqualTo(
                    @"\id MAT - TRG
\rem This draft of MAT was generated using AI on 1970-01-01 00:00:00Z. It should be reviewed and edited carefully.
\c 1
\v 1 Chapter 1, verse 1. Translated new paragraph
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
        using TestEnvironment env = new();
        env.AddMatthewToTarget();

        string usfm = await env.GetUsfmAsync(
            PretranslationUsfmTextOrigin.OnlyExisting,
            PretranslationUsfmTemplate.Target
        );

        List<string> lines = TargetUsfm.Split('\n').ToList();

        lines.Insert(
            1,
            @"\rem This draft of MAT was generated using AI on 1970-01-01 00:00:00Z. It should be reviewed and edited carefully."
        );
        Assert.That(usfm, Is.EqualTo(string.Join('\n', lines)).IgnoreLineEndings());
    }

    [Test]
    public async Task GetUsfmAsync_Target_OnlyPretranslated()
    {
        using TestEnvironment env = new();
        env.AddMatthewToTarget();

        string usfm = await env.GetUsfmAsync(
            PretranslationUsfmTextOrigin.OnlyPretranslated,
            PretranslationUsfmTemplate.Target
        );

        Assert.That(
            usfm,
            Is.EqualTo(
                    @"\id MAT - TRG
\rem This draft of MAT was generated using AI on 1970-01-01 00:00:00Z. It should be reviewed and edited carefully.
\c 1
\v 1 Chapter 1, verse 1. Translated new paragraph
\v 2 Chapter 1, verse 2.
\v 3
"
                )
                .IgnoreLineEndings()
        );
    }

    [Test]
    public async Task GetUsfmAsync_Disclaimer_Remark_Shown()
    {
        using TestEnvironment env = new();

        string usfm = await env.GetUsfmAsync(
            PretranslationUsfmTextOrigin.PreferExisting,
            PretranslationUsfmTemplate.Source
        );
        Assert.That(usfm, Does.Contain("rem This draft"));
    }

    private class TestEnvironment : IDisposable
    {
        public TestEnvironment()
        {
            Shared.Models.CorpusFile file1 =
                new()
                {
                    Id = "file1",
                    Filename = "file1.zip",
                    Format = Shared.Contracts.FileFormat.Paratext,
                    TextId = "project1"
                };
            Shared.Models.CorpusFile file2 =
                new()
                {
                    Id = "file2",
                    Filename = "file2.zip",
                    Format = Shared.Contracts.FileFormat.Paratext,
                    TextId = "project1"
                };
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
                                SourceFiles = [file1],
                                TargetFiles = [file2],
                            }
                        ]
                    },
                    new()
                    {
                        Id = "parallel_engine1",
                        Owner = "owner1",
                        SourceLanguage = "en",
                        TargetLanguage = "en",
                        Type = "nmt",
                        ModelRevision = 1,
                        ParallelCorpora =
                        [
                            new()
                            {
                                Id = "parallel_corpus1",
                                SourceCorpora = new List<Shared.Models.MonolingualCorpus>()
                                {
                                    new()
                                    {
                                        Id = "src_1",
                                        Language = "en",
                                        Files = [file1],
                                    }
                                },
                                TargetCorpora = new List<Shared.Models.MonolingualCorpus>()
                                {
                                    new()
                                    {
                                        Id = "trg_1",
                                        Language = "es",
                                        Files = [file2],
                                    }
                                }
                            }
                        ]
                    },
                ]
            );

            Builds = new MemoryRepository<Build>(
                [
                    new()
                    {
                        Id = "build1",
                        EngineRef = "engine1",
                        DateFinished = DateTime.UnixEpoch
                    },
                    new()
                    {
                        Id = "build2",
                        EngineRef = "parallel_engine1",
                        DateFinished = DateTime.UnixEpoch
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
                        Translation = "Chapter 1, verse 1. Translated new paragraph",
                        SourceTokens = ["SRC", "-", "Chapter", "one", ",", "verse", "one", ".", "new", "paragraph"],
                        TranslationTokens = ["Chapter", "1", ",", "verse", "1", ".", "Translated", "new", "paragraph"],
                        Alignment =
                        [
                            new() { SourceIndex = 2, TargetIndex = 0 },
                            new() { SourceIndex = 3, TargetIndex = 1 },
                            new() { SourceIndex = 4, TargetIndex = 2 },
                            new() { SourceIndex = 5, TargetIndex = 3 },
                            new() { SourceIndex = 6, TargetIndex = 4 },
                            new() { SourceIndex = 7, TargetIndex = 5 },
                            new() { SourceIndex = 8, TargetIndex = 6 },
                            new() { SourceIndex = 8, TargetIndex = 7 },
                            new() { SourceIndex = 9, TargetIndex = 8 },
                        ]
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
                        EngineRef = "parallel_engine1",
                        ModelRevision = 1,
                        CorpusRef = "parallel_corpus1",
                        TextId = "MAT",
                        Refs = ["MAT 1:1"],
                        Translation = "Chapter 1, verse 1. Translated new paragraph",
                        SourceTokens = ["SRC", "-", "Chapter", "one", ",", "verse", "one", ".", "new", "paragraph"],
                        TranslationTokens = ["Chapter", "1", ",", "verse", "1", ".", "Translated", "new", "paragraph"],
                        Alignment =
                        [
                            new() { SourceIndex = 2, TargetIndex = 0 },
                            new() { SourceIndex = 3, TargetIndex = 1 },
                            new() { SourceIndex = 4, TargetIndex = 2 },
                            new() { SourceIndex = 5, TargetIndex = 3 },
                            new() { SourceIndex = 6, TargetIndex = 4 },
                            new() { SourceIndex = 7, TargetIndex = 5 },
                            new() { SourceIndex = 8, TargetIndex = 6 },
                            new() { SourceIndex = 8, TargetIndex = 7 },
                            new() { SourceIndex = 9, TargetIndex = 8 },
                        ]
                    },
                    new()
                    {
                        Id = "pt4",
                        EngineRef = "parallel_engine1",
                        ModelRevision = 1,
                        CorpusRef = "parallel_corpus1",
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
            zipSubstituteSource
                .OpenEntry("MATSRC.SFM")
                .Returns(x => new MemoryStream(Encoding.UTF8.GetBytes(SourceUsfm)));
            zipSubstituteTarget.OpenEntry("MATTRG.SFM").Returns(x => new MemoryStream(Encoding.UTF8.GetBytes("")));
            zipSubstituteSource.EntryExists(Arg.Any<string>()).Returns(false);
            zipSubstituteTarget.EntryExists(Arg.Any<string>()).Returns(false);
            zipSubstituteSource.EntryExists("MATSRC.SFM").Returns(true);
            zipSubstituteTarget.EntryExists("MATTRG.SFM").Returns(true);
            TargetZipContainer = zipSubstituteTarget;
            TextUpdaters = new List<Shared.Services.ZipParatextProjectTextUpdater>();
            Shared.Services.ZipParatextProjectTextUpdater GetTextUpdater(string type)
            {
                var updater = type switch
                {
                    "SRC"
                        => new Shared.Services.ZipParatextProjectTextUpdater(
                            zipSubstituteSource,
                            CreateProjectSettings("SRC")
                        ),
                    "TRG"
                        => new Shared.Services.ZipParatextProjectTextUpdater(
                            zipSubstituteTarget,
                            CreateProjectSettings("TRG")
                        ),
                    _ => throw new ArgumentException()
                };
                TextUpdaters.Add(updater);
                return updater;
            }
            ScriptureDataFileService.GetZipParatextProjectTextUpdater("file1.zip").Returns(x => GetTextUpdater("SRC"));
            ScriptureDataFileService.GetZipParatextProjectTextUpdater("file2.zip").Returns(x => GetTextUpdater("TRG"));
            Service = new PretranslationService(Pretranslations, Engines, Builds, ScriptureDataFileService);
        }

        public PretranslationService Service { get; }
        public MemoryRepository<Pretranslation> Pretranslations { get; }
        public MemoryRepository<Engine> Engines { get; }
        public MemoryRepository<Build> Builds { get; }
        public IScriptureDataFileService ScriptureDataFileService { get; }
        public IZipContainer TargetZipContainer { get; }
        public IList<Shared.Services.ZipParatextProjectTextUpdater> TextUpdaters { get; }

        public async Task<string> GetUsfmAsync(
            PretranslationUsfmTextOrigin textOrigin,
            PretranslationUsfmTemplate template,
            PretranslationUsfmMarkerBehavior paragraphMarkerBehavior = PretranslationUsfmMarkerBehavior.Preserve
        )
        {
            string usfm = await Service.GetUsfmAsync(
                engineId: "engine1",
                modelRevision: 1,
                corpusId: "corpus1",
                textId: "MAT",
                textOrigin: textOrigin,
                template: template,
                paragraphMarkerBehavior: paragraphMarkerBehavior,
                embedBehavior: PretranslationUsfmMarkerBehavior.Preserve,
                styleMarkerBehavior: PretranslationUsfmMarkerBehavior.Strip
            );
            usfm = usfm.Replace("\r\n", "\n");
            string parallel_usfm = await Service.GetUsfmAsync(
                engineId: "parallel_engine1",
                modelRevision: 1,
                corpusId: "parallel_corpus1",
                textId: "MAT",
                textOrigin: textOrigin,
                template: template,
                paragraphMarkerBehavior: paragraphMarkerBehavior,
                embedBehavior: PretranslationUsfmMarkerBehavior.Preserve,
                styleMarkerBehavior: PretranslationUsfmMarkerBehavior.Strip
            );
            parallel_usfm = parallel_usfm.Replace("\r\n", "\n");
            Assert.That(parallel_usfm, Is.EqualTo(usfm));
            return usfm;
        }

        public void AddMatthewToTarget()
        {
            TargetZipContainer
                .OpenEntry("MATTRG.SFM")
                .Returns(x => new MemoryStream(Encoding.UTF8.GetBytes(TargetUsfm)));
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
                biblicalTermsFileName: "BiblicalTerms.xml",
                languageCode: "en"
            );
        }

        public void Dispose()
        {
            foreach (var updater in TextUpdaters)
            {
                updater.Dispose();
            }
        }
    }
}
