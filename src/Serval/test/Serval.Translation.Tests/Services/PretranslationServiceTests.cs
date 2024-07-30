namespace Serval.Translation.Services;

[TestFixture]
public class PretranslationServiceTests
{
    private const string SourceUsfmMatthew =
        $@"\id MAT - SRC
\c 1
\v 1 SRC - Chapter one, verse one.
\v 2
\v 3 SRC - Chapter one, verse three.
";

    private const string TargetUsfmMatthew =
        @"\id MAT - TRG
\c 1
\v 1 TRG - Chapter one, verse one.
\v 2
\v 3 TRG - Chapter one, verse three.
";

    private const string SourceUsfmSusanna =
        @"\id SUS - SRC
    \c 1
\p
\v 1 et erat vir habitans in Babylone et nomen eius Ioachim
\v 2 et accepit uxorem nomine Susannam filiam Chelciae pulchram nimis et timentem Dominum
\v 3 parentes enim illius cum essent iusti erudierunt filiam suam secundum legem Mosi
\v 4 erat autem Ioachim dives valde et erat ei pomerium vicinum domus suae et ad ipsum confluebant Iudaei eo quod esset honorabilior omnium
\v 5 et constituti sunt duo senes iudices in anno illo de quibus locutus est Dominus quia egressa est iniquitas de Babylone a senibus iudicibus qui videbantur regere populum
\v 6 isti frequentabant domum Ioachim et veniebant ad eos omnes qui habebant iudicia
\v 7 cum autem populus revertisset per meridiem ingrediebatur Susanna et deambulabat in pomerio viri sui
\v 8 et videbant eam senes cotidie ingredientem et deambulantem et exarserunt in concupiscentia eius
\v 9 et everterunt sensum suum et declinaverunt oculos suos ut non viderent caelum neque recordarentur iudiciorum iustorum
\v 10 erant ergo ambo vulnerati amore eius nec indicaverunt sibi vicissim dolorem suum
\v 11 erubescebant enim indicare concupiscentiam suam volentes concumbere cum ea
\v 12 et observabant cotidie sollicitius videre eam dixitque alter ad alterum
\v 13 eamus domum quia prandii hora est et egressi recesserunt a se
\v 14 cumque revertissent venerunt in unum et sciscitantes ab invicem causam confessi sunt concupiscentiam suam et tunc in commune statuerunt tempus quando eam possent invenire solam
\v 15 factum est autem cum observarent diem aptum ingressa est aliquando sicut heri et nudius tertius cum duabus solis puellis voluitque lavari in pomerio aestus quippe erat
\v 16 et non erat ibi quisquam praeter duos senes absconditos et contemplantes eam
\v 17 dixit ergo puellis adferte mihi oleum et smegmata et ostia pomerii claudite ut lavem
\v 18 et fecerunt sicut praeceperat clauseruntque ostia pomerii et egressae sunt per posticium ut adferrent quae iusserat nesciebantque senes intus esse absconditos
\v 19 cum autem egressae essent puellae surrexerunt duo senes et adcurrerunt ad eam et dixerunt
\v 20 ecce ostia pomerii clausa sunt et nemo nos videt et in concupiscentia tui sumus quam ob rem adsentire nobis et commiscere nobiscum
\v 21 quod si nolueris dicemus testimonium contra te quod fuerit tecum iuvenis et ob hanc causam emiseris puellas a te
\v 22 ingemuit Susanna et ait angustiae mihi undique si enim hoc egero mors mihi est si autem non egero non effugiam manus vestras
\v 23 sed melius mihi est absque opere incidere in manus vestras quam peccare in conspectu Domini
\v 24 et exclamavit voce magna Susanna exclamaverunt autem et senes adversus eam
\v 25 et cucurrit unus et aperuit ostia pomerii
\v 26 cum ergo audissent clamorem in pomerio famuli domus inruerunt per posticam ut viderent quidnam esset
\v 27 postquam autem senes locuti sunt erubuerunt servi vehementer quia numquam dictus fuerat sermo huiuscemodi de Susanna et facta est dies crastina
\v 28 cumque venisset populus ad virum eius Ioachim venerunt et duo presbyteri pleni iniqua cogitatione adversum Susannam ut interficerent eam
\v 29 et dixerunt coram populo mittite ad Susannam filiam Chelciae uxorem Ioachim et statim miserunt
\v 30 et venit cum parentibus et filiis et universis cognatis suis
\v 31 porro Susanna erat delicata nimis et pulchra specie
\v 32 at iniqui illi iusserunt ut discoperiretur erat enim cooperta ut vel sic satiarentur decore eius
\v 33 flebant igitur sui et omnes qui noverant eam
\v 34 consurgentes autem duo presbyteri in medio populi posuerunt manus super caput eius
\v 35 quae flens suspexit ad caelum erat enim cor eius fiduciam habens in Domino
\v 36 et dixerunt presbyteri cum deambularemus in pomerio soli ingressa est haec cum duabus puellis et clausit ostia pomerii et dimisit puellas
\v 37 venitque ad eam adulescens qui erat absconditus et concubuit cum ea
\v 38 porro nos cum essemus in angulo pomerii videntes iniquitatem cucurrimus ad eos et vidimus eos pariter commisceri
\v 39 et illum quidem non quivimus conprehendere quia fortior nobis erat et apertis ostiis exilivit
\v 40 hanc autem cum adprehendissemus interrogavimus quisnam esset adulescens et noluit indicare nobis huius rei testes sumus
\v 41 credidit eis multitudo quasi senibus populi et iudicibus et condemnaverunt eam ad mortem
\v 42 exclamavit autem voce magna Susanna et dixit Deus aeterne qui absconditorum es cognitor qui nosti omnia antequam fiant
\v 43 tu scis quoniam falsum contra me tulerunt testimonium et ecce morior cum nihil horum fecerim quae isti malitiose conposuerunt adversum me
\v 44 exaudivit autem Dominus vocem eius
\v 45 cumque duceretur ad mortem suscitavit Deus spiritum sanctum pueri iunioris cuius nomen Danihel
\v 46 et exclamavit voce magna mundus ego sum a sanguine huius
\v 47 et conversus omnis populus ad eum dixit quis est sermo iste quem tu locutus es
\v 48 qui cum staret in medio eorum ait sic fatui filii Israhel non iudicantes neque quod verum est cognoscentes condemnastis filiam Israhel
\v 49 revertimini ad iudicium quia falsum testimonium locuti sunt adversum eam
\v 50 reversus est ergo populus cum festinatione et dixerunt ei senes veni et sede in medio nostrum et indica nobis quia tibi dedit Deus honorem senectutis
\v 51 et dixit ad eos Danihel separate illos ab invicem procul et diiudicabo eos
\v 52 cum ergo divisi essent alter ab altero vocavit unum de eis et dixit ad eum inveterate dierum malorum nunc venerunt peccata tua quae operabaris prius
\v 53 iudicans iudicia iniusta innocentes opprimens et dimittens noxios dicente Domino innocentem et iustum non interficies
\v 54 nunc ergo si vidisti eam dic sub qua arbore videris eos loquentes sibi qui ait sub scino
\v 55 dixit autem Danihel recte mentitus es in caput tuum ecce enim angelus Dei accepta sententia ab eo scindet te medium
\v 56 et amoto eo iussit venire alium et dixit ei semen Chanaan et non Iuda species decepit te et concupiscentia subvertit cor tuum
\v 57 sic faciebatis filiabus Israhel et illae timentes loquebantur vobis sed non filia Iuda sustinuit iniquitatem vestram
\v 58 nunc ergo dic mihi sub qua arbore conprehenderis eos loquentes sibi qui ait sub prino
\v 59 dixit autem ei Danihel recte mentitus es et tu in caput tuum manet enim angelus Dei gladium habens ut secet te medium et interficiat vos
\v 60 exclamavit itaque omnis coetus voce magna et benedixerunt Deo qui salvat sperantes in se
\v 61 et consurrexerunt adversum duos presbyteros convicerat enim eos Danihel ex ore suo falsum dixisse testimonium feceruntque eis sicuti male egerant adversum proximum
\v 62 ut facerent secundum legem Mosi et interfecerunt eos et salvatus est sanguis innoxius in die illa
\v 63 Chelcias autem et uxor eius laudaverunt Deum pro filia sua Susanna cum Ioachim marito eius et cognatis omnibus quia non esset inventa in ea res turpis
\v 64 Danihel autem factus est magnus in conspectu populi a die illa et deinceps
\v 65 et rex Astyages adpositus est ad patres suos et suscepit Cyrus Perses regnum eius
";
    private const string TargetUsfmSusanna =
        @"\id SUS - TRG
\c 1
\v 1
\v 2
\v 3
\v 4
\v 5
\v 6
\v 7
\v 8
\v 9
\v 10
\v 11
\v 12
\v 13
\v 14
\v 15
\v 16
\v 17
\v 18
\v 19
\v 20
\v 21
\v 22
\v 23
\v 24
\v 25
\v 26
\v 27
\v 28
\v 29
\v 30
\v 31
\v 32
\v 33
\v 34
\v 35
\v 36
\v 37
\v 38
\v 39
\v 40
\v 41
\v 42
\v 43
\v 44
\v 45
\v 46
\v 47
\v 48
\v 49
\v 50
\v 51
\v 52
\v 53
\v 54
\v 55
\v 56
\v 57
\v 58
\v 59
\v 60
\v 61
\v 62
\v 63
\v 64
\v 65";

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
    public async Task GetUsfmAsync_Susanna()
    {
        TestEnvironment env = new();

        string usfm = await env.GetUsfmAsync(
            PretranslationUsfmTextOrigin.OnlyPretranslated,
            PretranslationUsfmTemplate.Target,
            "engine2",
            "corpus2",
            "SUS"
        );

        Assert.That(
            usfm,
            Is.EqualTo(
                    @"\id SUS - TRG
\c 1
\v 1
\v 2
\v 3
\v 4
\v 5
\v 6
\v 7
\v 8
\v 9
\v 10
\v 11
\v 12
\v 13
\v 14
\v 15
\v 16
\v 17
\v 18
\v 19
\v 20
\v 21
\v 22
\v 23
\v 24
\v 25
\v 26
\v 27
\v 28
\v 29
\v 30
\v 31
\v 32
\v 33
\v 34
\v 35
\v 36
\v 37
\v 38
\v 39
\v 40
\v 41
\v 42
\v 43
\v 44
\v 45
\v 46
\v 47
\v 48
\v 49
\v 50
\v 51
\v 52
\v 53
\v 54
\v 55
\v 56
\v 57
\v 58
\v 59
\v 60
\v 61
\v 62
\v 63
\v 64 Translation of penultimate verse
\v 65 Translation of final verse
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

        Assert.That(usfm, Is.EqualTo(TargetUsfmMatthew).IgnoreLineEndings());
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
                    },
                    new()
                    {
                        Id = "engine2",
                        Owner = "owner1",
                        SourceLanguage = "en",
                        TargetLanguage = "en",
                        Type = "nmt",
                        ModelRevision = 1,
                        Corpora =
                        [
                            new()
                            {
                                Id = "corpus2",
                                SourceLanguage = "en",
                                TargetLanguage = "en",
                                SourceFiles =
                                [
                                    new()
                                    {
                                        Id = "file3",
                                        Filename = "file3.zip",
                                        Format = Shared.Contracts.FileFormat.Paratext,
                                        TextId = "project2"
                                    }
                                ],
                                TargetFiles =
                                [
                                    new()
                                    {
                                        Id = "file4",
                                        Filename = "file4.zip",
                                        Format = Shared.Contracts.FileFormat.Paratext,
                                        TextId = "project2"
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
                        EngineRef = "engine2",
                        ModelRevision = 1,
                        CorpusRef = "corpus2",
                        TextId = "SUS",
                        Refs = ["SUS 1:64"],
                        Translation = "Translation of penultimate verse"
                    },
                    new()
                    {
                        Id = "pt4",
                        EngineRef = "engine2",
                        ModelRevision = 1,
                        CorpusRef = "corpus2",
                        TextId = "SUS",
                        Refs = ["SUS 1:65"],
                        Translation = "Translation of final verse"
                    }
                ]
            );
            ScriptureDataFileService = Substitute.For<IScriptureDataFileService>();
            ScriptureDataFileService.GetParatextProjectSettings("file1.zip").Returns(CreateProjectSettings("SRC"));
            ScriptureDataFileService.GetParatextProjectSettings("file2.zip").Returns(CreateProjectSettings("TRG"));
            ScriptureDataFileService.GetParatextProjectSettings("file3.zip").Returns(CreateProjectSettings("SRC"));
            ScriptureDataFileService.GetParatextProjectSettings("file4.zip").Returns(CreateProjectSettings("TRG"));
            ScriptureDataFileService
                .ReadParatextProjectBookAsync("file1.zip", "MAT")
                .Returns(Task.FromResult<string?>(SourceUsfmMatthew));
            ScriptureDataFileService
                .ReadParatextProjectBookAsync("file2.zip", "MAT")
                .Returns(Task.FromResult<string?>(null));
            ScriptureDataFileService
                .ReadParatextProjectBookAsync("file3.zip", "SUS")
                .Returns(Task.FromResult<string?>(SourceUsfmSusanna));
            ScriptureDataFileService
                .ReadParatextProjectBookAsync("file4.zip", "SUS")
                .Returns(Task.FromResult<string?>(TargetUsfmSusanna));
            Service = new PretranslationService(Pretranslations, Engines, ScriptureDataFileService);
        }

        public PretranslationService Service { get; }
        public MemoryRepository<Pretranslation> Pretranslations { get; }
        public MemoryRepository<Engine> Engines { get; }
        public IScriptureDataFileService ScriptureDataFileService { get; }

        public async Task<string> GetUsfmAsync(
            PretranslationUsfmTextOrigin textOrigin,
            PretranslationUsfmTemplate template,
            string? engineId = null,
            string? corpusId = null,
            string? textId = null
        )
        {
            return (
                await Service.GetUsfmAsync(
                    engineId: engineId ?? "engine1",
                    modelRevision: 1,
                    corpusId: corpusId ?? "corpus1",
                    textId: textId ?? "MAT",
                    textOrigin: textOrigin,
                    template: template
                )
            ).Replace("\r\n", "\n");
        }

        public void AddMatthewToTarget()
        {
            ScriptureDataFileService
                .ReadParatextProjectBookAsync("file2.zip", "MAT")
                .Returns(Task.FromResult<string?>(TargetUsfmMatthew));
        }

        private static ParatextProjectSettings CreateProjectSettings(string name, ScrVers? versification = null)
        {
            return new ParatextProjectSettings(
                name: name,
                fullName: name,
                encoding: Encoding.UTF8,
                versification: versification ?? ScrVers.English,
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
