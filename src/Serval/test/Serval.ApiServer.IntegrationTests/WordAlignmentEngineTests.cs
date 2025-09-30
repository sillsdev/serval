using Google.Protobuf.WellKnownTypes;
using Serval.WordAlignment.Models;
using Serval.WordAlignment.V1;
using SIL.ServiceToolkit.Services;
using static Serval.ApiServer.Utils;
using Phase = Serval.Client.Phase;
using PhaseStage = Serval.Client.PhaseStage;

namespace Serval.ApiServer;

[TestFixture]
[Category("Integration")]
public class WordAlignmentEngineTests
{
    private static readonly WordAlignmentParallelCorpusConfig TestParallelCorpusConfig =
        new()
        {
            Name = "TestCorpus",
            SourceCorpusIds = [SOURCE_CORPUS_ID_1],
            TargetCorpusIds = [TARGET_CORPUS_ID],
        };

    private static readonly WordAlignmentParallelCorpusConfig TestMixedParallelCorpusConfig =
        new()
        {
            Name = "TestCorpus",
            SourceCorpusIds = [SOURCE_CORPUS_ID_1, SOURCE_CORPUS_ID_2],
            TargetCorpusIds = [TARGET_CORPUS_ID],
        };

    private static readonly WordAlignmentParallelCorpusConfig TestParallelCorpusConfigScripture =
        new()
        {
            Name = "TestCorpus",
            SourceCorpusIds = [SOURCE_CORPUS_ZIP_ID],
            TargetCorpusIds = [TARGET_CORPUS_ZIP_ID],
        };
    private static readonly WordAlignmentParallelCorpusConfig TestParallelCorpusConfigEmptySource =
        new()
        {
            Name = "TestCorpus",
            SourceCorpusIds = [EMPTY_CORPUS_ID],
            TargetCorpusIds = [TARGET_CORPUS_ID],
        };

    private static readonly WordAlignmentParallelCorpusConfig TestParallelCorpusConfigNoCorpora =
        new()
        {
            Name = "TestCorpus",
            SourceCorpusIds = [],
            TargetCorpusIds = [],
        };

    private const string ECHO_ENGINE1_ID = "e00000000000000000000001";
    private const string ECHO_ENGINE2_ID = "e00000000000000000000002";
    private const string ECHO_ENGINE3_ID = "e00000000000000000000003";
    private const string STATISTICAL_ENGINE_ID = "be0000000000000000000001";
    private const string FILE1_SRC_ID = "f00000000000000000000001";
    private const string FILE1_FILENAME = "file_a";
    private const string FILE2_TRG_ID = "f00000000000000000000002";
    private const string FILE2_FILENAME = "file_b";
    private const string FILE3_SRC_ZIP_ID = "f00000000000000000000003";
    private const string FILE3_FILENAME = "file_c";
    private const string FILE4_TRG_ZIP_ID = "f00000000000000000000004";
    private const string FILE4_FILENAME = "file_d";
    private const string SOURCE_CORPUS_ID_1 = "cc0000000000000000000001";
    private const string SOURCE_CORPUS_ID_2 = "cc0000000000000000000002";
    private const string TARGET_CORPUS_ID = "cc0000000000000000000003";
    private const string SOURCE_CORPUS_ZIP_ID = "cc0000000000000000000004";
    private const string TARGET_CORPUS_ZIP_ID = "cc0000000000000000000005";
    private const string EMPTY_CORPUS_ID = "cc0000000000000000000006";

    private const string DOES_NOT_EXIST_ENGINE_ID = "e00000000000000000000004";
    private const string DOES_NOT_EXIST_CORPUS_ID = "c00000000000000000000001";

    private TestEnvironment _env;

    [SetUp]
    public async Task SetUp()
    {
        _env = new TestEnvironment();
        var e0 = new Engine
        {
            Id = ECHO_ENGINE1_ID,
            Name = "e0",
            SourceLanguage = "en",
            TargetLanguage = "en",
            Type = "EchoWordAlignment",
            Owner = "client1",
            ParallelCorpora = [],
            ModelRevision = 1
        };
        var e1 = new Engine
        {
            Id = ECHO_ENGINE2_ID,
            Name = "e1",
            SourceLanguage = "en",
            TargetLanguage = "en",
            Type = "EchoWordAlignment",
            Owner = "client1",
            ParallelCorpora = [],
            ModelRevision = 0
        };
        var e2 = new Engine
        {
            Id = ECHO_ENGINE3_ID,
            Name = "e2",
            SourceLanguage = "en",
            TargetLanguage = "en",
            Type = "EchoWordAlignment",
            Owner = "client2",
            ParallelCorpora = [],
            ModelRevision = 1
        };
        var se0 = new Engine
        {
            Id = STATISTICAL_ENGINE_ID,
            Name = "se0",
            SourceLanguage = "en",
            TargetLanguage = "es",
            Type = "Statistical",
            Owner = "client1",
            ParallelCorpora = [],
            ModelRevision = 1
        };

        await _env.Engines.InsertAllAsync([e0, e1, e2, se0]);

        var srcFile = new DataFiles.Models.DataFile
        {
            Id = FILE1_SRC_ID,
            Owner = "client1",
            Name = "src.txt",
            Filename = FILE1_FILENAME,
            Format = Shared.Contracts.FileFormat.Text
        };
        var trgFile = new DataFiles.Models.DataFile
        {
            Id = FILE2_TRG_ID,
            Owner = "client1",
            Name = "trg.txt",
            Filename = FILE2_FILENAME,
            Format = Shared.Contracts.FileFormat.Text
        };
        var srcParatextFile = new DataFiles.Models.DataFile
        {
            Id = FILE3_SRC_ZIP_ID,
            Owner = "client1",
            Name = "src.zip",
            Filename = FILE3_FILENAME,
            Format = Shared.Contracts.FileFormat.Paratext
        };
        var trgParatextFile = new DataFiles.Models.DataFile
        {
            Id = FILE4_TRG_ZIP_ID,
            Owner = "client1",
            Name = "trg.zip",
            Filename = FILE4_FILENAME,
            Format = Shared.Contracts.FileFormat.Paratext
        };
        await _env.DataFiles.InsertAllAsync([srcFile, trgFile, srcParatextFile, trgParatextFile]);

        var srcCorpus = new DataFiles.Models.Corpus
        {
            Id = SOURCE_CORPUS_ID_1,
            Language = "en",
            Owner = "client1",
            Files = [new() { FileRef = srcFile.Id, TextId = "all" }]
        };
        var srcCorpus2 = new DataFiles.Models.Corpus
        {
            Id = SOURCE_CORPUS_ID_2,
            Language = "en",
            Owner = "client1",
            Files = [new() { FileRef = srcFile.Id, TextId = "all" }]
        };
        var trgCorpus = new DataFiles.Models.Corpus
        {
            Id = TARGET_CORPUS_ID,
            Language = "en",
            Owner = "client1",
            Files = [new() { FileRef = trgFile.Id, TextId = "all" }]
        };
        var srcScriptureCorpus = new DataFiles.Models.Corpus
        {
            Id = SOURCE_CORPUS_ZIP_ID,
            Language = "en",
            Owner = "client1",
            Files = [new() { FileRef = trgParatextFile.Id, TextId = "all" }]
        };
        var trgScriptureCorpus = new DataFiles.Models.Corpus
        {
            Id = TARGET_CORPUS_ZIP_ID,
            Language = "en",
            Owner = "client1",
            Files = [new() { FileRef = srcParatextFile.Id, TextId = "all" }]
        };
        var emptyCorpus = new DataFiles.Models.Corpus
        {
            Id = EMPTY_CORPUS_ID,
            Language = "en",
            Owner = "client1",
            Files = []
        };

        await _env.Corpora.InsertAllAsync(
            [srcCorpus, srcCorpus2, trgCorpus, srcScriptureCorpus, trgScriptureCorpus, emptyCorpus]
        );
    }

    [Test]
    [TestCase(new[] { Scopes.ReadWordAlignmentEngines }, 200)]
    [TestCase(new[] { Scopes.ReadFiles }, 403)] //Arbitrary unrelated privilege
    public async Task GetAllAsync(IEnumerable<string> scope, int expectedStatusCode)
    {
        WordAlignmentEnginesClient client = _env.CreateWordAlignmentEnginesClient(scope);
        switch (expectedStatusCode)
        {
            case 200:
                ICollection<WordAlignmentEngine> results = await client.GetAllAsync();
                Assert.That(results, Has.Count.EqualTo(3)); //Only three are owned by client1
                Assert.That(results.All(eng => eng.SourceLanguage.Equals("en")));
                break;
            case 403:
                ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.GetAllAsync();
                });
                Assert.That(ex?.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
            default:
                Assert.Fail("Unanticipated expectedStatusCode. Check test case for typo.");
                break;
        }
    }

    [Test]
    [TestCase(new[] { Scopes.ReadWordAlignmentEngines }, 200, ECHO_ENGINE1_ID)]
    [TestCase(new[] { Scopes.ReadFiles }, 403, ECHO_ENGINE1_ID)] //Arbitrary unrelated privilege
    [TestCase(new[] { Scopes.ReadWordAlignmentEngines }, 403, ECHO_ENGINE3_ID)] //Engine is not owned
    [TestCase(new[] { Scopes.ReadWordAlignmentEngines }, 404, DOES_NOT_EXIST_ENGINE_ID)]
    [TestCase(new[] { Scopes.ReadWordAlignmentEngines }, 404, "phony_id")]
    public async Task GetByIdAsync(IEnumerable<string> scope, int expectedStatusCode, string engineId)
    {
        WordAlignmentEnginesClient client = _env.CreateWordAlignmentEnginesClient(scope);
        switch (expectedStatusCode)
        {
            case 200:
                WordAlignmentEngine result = await client.GetAsync(engineId);
                Assert.That(result.Name, Is.EqualTo("e0"));
                break;
            case 403:
            case 404:
                ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.GetAsync(engineId);
                });
                Assert.That(ex?.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
            default:
                Assert.Fail("Unanticipated expectedStatusCode. Check test case for typo.");
                break;
        }
    }

    [Test]
    [TestCase(new[] { Scopes.CreateWordAlignmentEngines, Scopes.ReadWordAlignmentEngines }, 201, "EchoWordAlignment")]
    [TestCase(new[] { Scopes.CreateWordAlignmentEngines }, 400, "NotARealKindOfMT")]
    [TestCase(new[] { Scopes.ReadFiles }, 403, "EchoWordAlignment")] //Arbitrary unrelated privilege
    public async Task CreateEngineAsync(IEnumerable<string> scope, int expectedStatusCode, string engineType)
    {
        WordAlignmentEnginesClient client = _env.CreateWordAlignmentEnginesClient(scope);
        switch (expectedStatusCode)
        {
            case 201:
                WordAlignmentEngine result = await client.CreateAsync(
                    new WordAlignmentEngineConfig
                    {
                        Name = "test",
                        SourceLanguage = "en",
                        TargetLanguage = "en",
                        Type = engineType
                    }
                );
                Assert.That(result.Name, Is.EqualTo("test"));
                WordAlignmentEngine? engine = await client.GetAsync(result.Id);
                Assert.That(engine, Is.Not.Null);
                Assert.That(engine.Name, Is.EqualTo("test"));
                break;
            case 400:
            {
                ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.CreateAsync(
                        new WordAlignmentEngineConfig
                        {
                            Name = "test",
                            SourceLanguage = "en",
                            TargetLanguage = "es",
                            Type = engineType
                        }
                    );
                });
                Assert.That(ex?.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
            }
            case 403:
            {
                ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.CreateAsync(
                        new WordAlignmentEngineConfig
                        {
                            Name = "test",
                            SourceLanguage = "en",
                            TargetLanguage = "en",
                            Type = engineType
                        }
                    );
                });
                Assert.That(ex?.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
            }
            default:
                Assert.Fail("Unanticipated expectedStatusCode. Check test case for typo.");
                break;
        }
    }

    [Test]
    [TestCase(new[] { Scopes.DeleteWordAlignmentEngines, Scopes.ReadWordAlignmentEngines }, 200, ECHO_ENGINE1_ID)]
    [TestCase(new[] { Scopes.ReadWordAlignmentEngines }, 403, ECHO_ENGINE1_ID)] //Arbitrary unrelated privilege
    [TestCase(new[] { Scopes.DeleteWordAlignmentEngines }, 404, DOES_NOT_EXIST_ENGINE_ID)]
    public async Task DeleteEngineByIdAsync(IEnumerable<string> scope, int expectedStatusCode, string engineId)
    {
        WordAlignmentEnginesClient client = _env.CreateWordAlignmentEnginesClient(scope);
        switch (expectedStatusCode)
        {
            case 200:
                await client.DeleteAsync(engineId);
                ICollection<WordAlignmentEngine> results = await client.GetAllAsync();
                Assert.That(results, Has.Count.EqualTo(2)); //Only two are owned by client1
                Assert.That(results.All(eng => eng.SourceLanguage.Equals("en")));
                break;
            case 403:
            case 404:
                ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.DeleteAsync(engineId);
                });
                Assert.That(ex?.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
            default:
                Assert.Fail("Unanticipated expectedStatusCode. Check test case for typo.");
                break;
        }
    }

    [Test]
    [TestCase(new[] { Scopes.ReadWordAlignmentEngines, Scopes.UpdateWordAlignmentEngines }, 200, ECHO_ENGINE1_ID)]
    [TestCase(
        new[] { Scopes.ReadWordAlignmentEngines, Scopes.UpdateWordAlignmentEngines },
        404,
        DOES_NOT_EXIST_ENGINE_ID
    )]
    [TestCase(new[] { Scopes.ReadWordAlignmentEngines, Scopes.UpdateWordAlignmentEngines }, 409, ECHO_ENGINE1_ID)]
    [TestCase(new[] { Scopes.ReadFiles }, 403, ECHO_ENGINE1_ID)] //Arbitrary unrelated privilege
    public async Task GetWordAlignmentForSegmentPairWithEngineByIdAsync(
        IEnumerable<string> scope,
        int expectedStatusCode,
        string engineId
    )
    {
        WordAlignmentEnginesClient client = _env.CreateWordAlignmentEnginesClient(scope);
        switch (expectedStatusCode)
        {
            case 200:
                await _env.Builds.InsertAsync(
                    new Build { EngineRef = engineId, State = Shared.Contracts.JobState.Completed }
                );
                Client.WordAlignmentResult result = await client.AlignAsync(
                    engineId,
                    new WordAlignmentRequest { SourceSegment = "This is a test.", TargetSegment = "This is a test." }
                );
                Assert.That(result.SourceTokens, Is.EqualTo("This is a test .".Split()));
                Assert.That(result.TargetTokens, Is.EqualTo("This is a test .".Split()));
                break;
            case 409:
            {
                _env.EchoClient.GetWordAlignmentAsync(
                    Arg.Any<GetWordAlignmentRequest>(),
                    null,
                    null,
                    Arg.Any<CancellationToken>()
                )
                    .Returns(CreateAsyncUnaryCall<GetWordAlignmentResponse>(StatusCode.Aborted));
                ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.AlignAsync(
                        engineId,
                        new WordAlignmentRequest
                        {
                            SourceSegment = "This is a test.",
                            TargetSegment = "This is a test."
                        }
                    );
                });
                Assert.That(ex?.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
            }
            case 403:
            case 404:
            {
                ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.AlignAsync(
                        engineId,
                        new WordAlignmentRequest
                        {
                            SourceSegment = "This is a test.",
                            TargetSegment = "This is a test."
                        }
                    );
                });
                Assert.That(ex?.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
            }
            default:
                Assert.Fail("Unanticipated expectedStatusCode. Check test case for typo.");
                break;
        }
    }

    [Test]
    public async Task AddParallelCorpusToEngineByIdAsync()
    {
        WordAlignmentEnginesClient client = _env.CreateWordAlignmentEnginesClient(
            new[] { Scopes.UpdateWordAlignmentEngines }
        );
        WordAlignmentParallelCorpus result = await client.AddParallelCorpusAsync(
            ECHO_ENGINE1_ID,
            TestParallelCorpusConfig
        );
        Assert.Multiple(() =>
        {
            Assert.That(result.SourceCorpora.First().Id, Is.EqualTo(SOURCE_CORPUS_ID_1));
            Assert.That(result.TargetCorpora.First().Id, Is.EqualTo(TARGET_CORPUS_ID));
        });
        Engine? engine = await _env.Engines.GetAsync(ECHO_ENGINE1_ID);
        if (engine == null)
        {
            Assert.Fail("Engine not found");
            return;
        }
        Assert.Multiple(() =>
        {
            Assert.That(engine.ParallelCorpora[0].SourceCorpora[0].Files[0].Filename, Is.EqualTo(FILE1_FILENAME));
            Assert.That(engine.ParallelCorpora[0].TargetCorpora[0].Files[0].Filename, Is.EqualTo(FILE2_FILENAME));
        });
    }

    public void AddParallelCorpusToEngineById_NoSuchEngine()
    {
        WordAlignmentEnginesClient client = _env.CreateWordAlignmentEnginesClient(
            new[] { Scopes.UpdateWordAlignmentEngines }
        );

        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            await client.AddParallelCorpusAsync(DOES_NOT_EXIST_ENGINE_ID, TestParallelCorpusConfig);
        });
        Assert.That(ex?.StatusCode, Is.EqualTo(404));
    }

    [Test]
    public void AddParallelCorpusToEngineById_NotAuthorized()
    {
        WordAlignmentEnginesClient client = _env.CreateWordAlignmentEnginesClient(new[] { Scopes.ReadFiles });

        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            await client.AddParallelCorpusAsync(ECHO_ENGINE1_ID, TestParallelCorpusConfig);
        });
        Assert.That(ex?.StatusCode, Is.EqualTo(403));
    }

    [Test]
    public async Task UpdateParallelCorpusByIdForEngineByIdAsync()
    {
        WordAlignmentEnginesClient client = _env.CreateWordAlignmentEnginesClient();

        WordAlignmentParallelCorpus result = await client.AddParallelCorpusAsync(
            ECHO_ENGINE1_ID,
            TestParallelCorpusConfig
        );
        var updateConfig = new WordAlignmentParallelCorpusUpdateConfig
        {
            SourceCorpusIds = [SOURCE_CORPUS_ID_1],
            TargetCorpusIds = [TARGET_CORPUS_ID]
        };
        await client.UpdateParallelCorpusAsync(ECHO_ENGINE1_ID, result.Id, updateConfig);
        Engine? engine = await _env.Engines.GetAsync(ECHO_ENGINE1_ID);
        if (engine == null)
        {
            Assert.Fail("Engine not found");
            return;
        }
        Assert.Multiple(() =>
        {
            Assert.That(engine.ParallelCorpora[0].SourceCorpora[0].Files[0].Filename, Is.EqualTo(FILE1_FILENAME));
            Assert.That(engine.ParallelCorpora[0].TargetCorpora[0].Files[0].Filename, Is.EqualTo(FILE2_FILENAME));
        });
    }

    [Test]
    public void UpdateParallelCorpusByIdForEngineById_NoSuchCorpus()
    {
        WordAlignmentEnginesClient client = _env.CreateWordAlignmentEnginesClient();

        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            var updateConfig = new WordAlignmentParallelCorpusUpdateConfig
            {
                SourceCorpusIds = [SOURCE_CORPUS_ID_1],
                TargetCorpusIds = [TARGET_CORPUS_ID]
            };
            await client.UpdateParallelCorpusAsync(ECHO_ENGINE1_ID, DOES_NOT_EXIST_CORPUS_ID, updateConfig);
        });
        Assert.That(ex?.StatusCode, Is.EqualTo(404));
    }

    [Test]
    public void UpdateParallelCorpusByIdForEngineById_NoSuchEngine()
    {
        WordAlignmentEnginesClient client = _env.CreateWordAlignmentEnginesClient();

        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            var updateConfig = new WordAlignmentParallelCorpusUpdateConfig
            {
                SourceCorpusIds = [SOURCE_CORPUS_ID_1],
                TargetCorpusIds = [TARGET_CORPUS_ID]
            };
            await client.UpdateParallelCorpusAsync(DOES_NOT_EXIST_ENGINE_ID, SOURCE_CORPUS_ID_1, updateConfig);
        });
        Assert.That(ex?.StatusCode, Is.EqualTo(404));
    }

    [Test]
    public void UpdateParallelCorpusByIdForEngineById_NotAuthorized()
    {
        WordAlignmentEnginesClient client = _env.CreateWordAlignmentEnginesClient(new[] { Scopes.ReadFiles });

        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            var updateConfig = new WordAlignmentParallelCorpusUpdateConfig
            {
                SourceCorpusIds = [SOURCE_CORPUS_ID_1],
                TargetCorpusIds = [TARGET_CORPUS_ID]
            };
            await client.UpdateParallelCorpusAsync(ECHO_ENGINE1_ID, DOES_NOT_EXIST_CORPUS_ID, updateConfig);
        });
        Assert.That(ex?.StatusCode, Is.EqualTo(403));
    }

    [Test]
    public async Task GetAllParallelCorporaForEngineByIdAsync()
    {
        WordAlignmentEnginesClient client = _env.CreateWordAlignmentEnginesClient();

        WordAlignmentParallelCorpus result = await client.AddParallelCorpusAsync(
            ECHO_ENGINE1_ID,
            TestParallelCorpusConfig
        );
        WordAlignmentParallelCorpus resultAfterAdd = (await client.GetAllParallelCorporaAsync(ECHO_ENGINE1_ID)).First();
        Assert.Multiple(() =>
        {
            Assert.That(resultAfterAdd.Id, Is.EqualTo(result.Id));
            Assert.That(resultAfterAdd.SourceCorpora.First().Id, Is.EqualTo(result.SourceCorpora.First().Id));
        });
    }

    [Test]
    public void GetAllParallelCorporaForEngineById_NoSuchEngine()
    {
        WordAlignmentEnginesClient client = _env.CreateWordAlignmentEnginesClient();

        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            WordAlignmentParallelCorpus result = (
                await client.GetAllParallelCorporaAsync(DOES_NOT_EXIST_ENGINE_ID)
            ).First();
        });
        Assert.That(ex?.StatusCode, Is.EqualTo(404));
    }

    [Test]
    public void GetAllParallelCorporaForEngineById_NotAuthorized()
    {
        WordAlignmentEnginesClient client = _env.CreateWordAlignmentEnginesClient(new[] { Scopes.ReadFiles });

        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            WordAlignmentParallelCorpus result = (await client.GetAllParallelCorporaAsync(ECHO_ENGINE1_ID)).First();
        });
        Assert.That(ex?.StatusCode, Is.EqualTo(403));
    }

    [Test]
    public async Task GetParallelCorpusByIdForEngineByIdAsync()
    {
        WordAlignmentEnginesClient client = _env.CreateWordAlignmentEnginesClient();
        WordAlignmentParallelCorpus result = await client.AddParallelCorpusAsync(
            ECHO_ENGINE1_ID,
            TestParallelCorpusConfig
        );

        Assert.That(result, Is.Not.Null);
        WordAlignmentParallelCorpus resultAfterAdd = await client.GetParallelCorpusAsync(ECHO_ENGINE1_ID, result.Id);
        Assert.Multiple(() =>
        {
            Assert.That(resultAfterAdd.Id, Is.EqualTo(result.Id));
            Assert.That(resultAfterAdd.SourceCorpora[0].Id, Is.EqualTo(result.SourceCorpora[0].Id));
        });
    }

    [Test]
    public void GetParallelCorpusByIdForEngineById_NoCorpora()
    {
        WordAlignmentEnginesClient client = _env.CreateWordAlignmentEnginesClient();

        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            WordAlignmentParallelCorpus result_afterAdd = await client.GetParallelCorpusAsync(
                ECHO_ENGINE1_ID,
                DOES_NOT_EXIST_CORPUS_ID
            );
        });
        Assert.That(ex?.StatusCode, Is.EqualTo(404));
    }

    [Test]
    public void GetParallelCorpusByIdForEngineById_NoSuchEngine()
    {
        WordAlignmentEnginesClient client = _env.CreateWordAlignmentEnginesClient();

        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            WordAlignmentParallelCorpus result_afterAdd = await client.GetParallelCorpusAsync(
                DOES_NOT_EXIST_ENGINE_ID,
                SOURCE_CORPUS_ID_1
            );
        });
        Assert.That(ex?.StatusCode, Is.EqualTo(404));
    }

    [Test]
    public async Task GetParallelCorpusByIdForEngineById_NoSuchCorpus()
    {
        WordAlignmentEnginesClient client = _env.CreateWordAlignmentEnginesClient();
        WordAlignmentParallelCorpus result = await client.AddParallelCorpusAsync(
            ECHO_ENGINE1_ID,
            TestParallelCorpusConfig
        );

        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            WordAlignmentParallelCorpus result_afterAdd = await client.GetParallelCorpusAsync(
                ECHO_ENGINE1_ID,
                DOES_NOT_EXIST_CORPUS_ID
            );
        });
        Assert.That(ex?.StatusCode, Is.EqualTo(404));
    }

    [Test]
    public void GetParallelCorpusByIdForEngineById_NotAuthorized()
    {
        WordAlignmentEnginesClient client = _env.CreateWordAlignmentEnginesClient(new[] { Scopes.ReadFiles });

        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            WordAlignmentParallelCorpus result_afterAdd = await client.GetParallelCorpusAsync(
                ECHO_ENGINE1_ID,
                DOES_NOT_EXIST_CORPUS_ID
            );
        });
        Assert.That(ex?.StatusCode, Is.EqualTo(403));
    }

    [Test]
    public async Task DeleteParallelCorpusByIdForEngineByIdAsync()
    {
        WordAlignmentEnginesClient client = _env.CreateWordAlignmentEnginesClient();

        WordAlignmentParallelCorpus result = await client.AddParallelCorpusAsync(
            ECHO_ENGINE1_ID,
            TestParallelCorpusConfig
        );
        await client.DeleteParallelCorpusAsync(ECHO_ENGINE1_ID, result.Id);
        ICollection<WordAlignmentParallelCorpus> resultsAfterDelete = await client.GetAllParallelCorporaAsync(
            ECHO_ENGINE1_ID
        );
        Assert.That(resultsAfterDelete, Has.Count.EqualTo(0));
    }

    [Test]
    public void DeleteParallelCorpusByIdForEngineById_NoSuchCorpus()
    {
        WordAlignmentEnginesClient client = _env.CreateWordAlignmentEnginesClient();

        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            await client.DeleteParallelCorpusAsync(ECHO_ENGINE1_ID, DOES_NOT_EXIST_CORPUS_ID);
        });
        Assert.That(ex?.StatusCode, Is.EqualTo(404));
    }

    [Test]
    public void DeleteParallelCorpusByIdForEngineById_NoSuchEngine()
    {
        WordAlignmentEnginesClient client = _env.CreateWordAlignmentEnginesClient();

        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            await client.DeleteParallelCorpusAsync(DOES_NOT_EXIST_ENGINE_ID, SOURCE_CORPUS_ID_1);
        });
        Assert.That(ex?.StatusCode, Is.EqualTo(404));
    }

    [Test]
    public void DeleteParallelCorpusByIdForEngineById_NotAuthorized()
    {
        WordAlignmentEnginesClient client = _env.CreateWordAlignmentEnginesClient(new[] { Scopes.ReadFiles });

        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            await client.DeleteParallelCorpusAsync(ECHO_ENGINE1_ID, SOURCE_CORPUS_ID_1);
        });
        Assert.That(ex?.StatusCode, Is.EqualTo(403));
    }

    [Test]
    public async Task GetAllWordAlignmentsAsync_Exists()
    {
        WordAlignmentEnginesClient client = _env.CreateWordAlignmentEnginesClient();
        WordAlignmentParallelCorpus addedCorpus = await client.AddParallelCorpusAsync(
            ECHO_ENGINE1_ID,
            TestParallelCorpusConfig
        );

        await _env.Engines.UpdateAsync(ECHO_ENGINE1_ID, u => u.Set(e => e.ModelRevision, 1));
        var wordAlignment = new WordAlignment.Models.WordAlignment
        {
            CorpusRef = addedCorpus.Id,
            TextId = "all",
            EngineRef = ECHO_ENGINE1_ID,
            Refs = ["ref1", "ref2"],
            SourceTokens = ["This", "is", "a", "test", "."],
            TargetTokens = ["This", "is", "a", "test", "."],
            Alignment = CreateNAlignedWordPair(5),
            ModelRevision = 1
        };
        await _env.WordAlignments.InsertAsync(wordAlignment);

        ICollection<Client.WordAlignment> results = await client.GetAllWordAlignmentsAsync(
            ECHO_ENGINE1_ID,
            addedCorpus.Id
        );
        Assert.That(results.All(p => p.TextId == "all"), Is.True);
    }

    [Test]
    public void GetAllWordAlignmentsAsync_EngineDoesNotExist()
    {
        WordAlignmentEnginesClient client = _env.CreateWordAlignmentEnginesClient();

        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(
            () => client.GetAllWordAlignmentsAsync(DOES_NOT_EXIST_ENGINE_ID, "cccccccccccccccccccccccc")
        );
        Assert.That(ex?.StatusCode, Is.EqualTo(404));
    }

    [Test]
    public void GetAllWordAlignmentsAsync_CorpusDoesNotExist()
    {
        WordAlignmentEnginesClient client = _env.CreateWordAlignmentEnginesClient();

        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(
            () => client.GetAllWordAlignmentsAsync(ECHO_ENGINE1_ID, "cccccccccccccccccccccccc")
        );
        Assert.That(ex?.StatusCode, Is.EqualTo(404));
    }

    [Test]
    public async Task GetAllWordAlignmentsAsync_EngineNotBuilt()
    {
        WordAlignmentEnginesClient client = _env.CreateWordAlignmentEnginesClient();
        WordAlignmentParallelCorpus addedCorpus = await client.AddParallelCorpusAsync(
            ECHO_ENGINE2_ID,
            TestParallelCorpusConfig
        );

        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(
            () => client.GetAllWordAlignmentsAsync(ECHO_ENGINE2_ID, addedCorpus.Id)
        );
        Assert.That(ex?.StatusCode, Is.EqualTo(409));
    }

    [Test]
    public async Task GetAllWordAlignmentsAsync_TextIdExists()
    {
        WordAlignmentEnginesClient client = _env.CreateWordAlignmentEnginesClient();
        WordAlignmentParallelCorpus addedCorpus = await client.AddParallelCorpusAsync(
            ECHO_ENGINE1_ID,
            TestParallelCorpusConfig
        );

        await _env.Engines.UpdateAsync(ECHO_ENGINE1_ID, u => u.Set(e => e.ModelRevision, 1));
        var wordAlignment = new WordAlignment.Models.WordAlignment
        {
            CorpusRef = addedCorpus.Id,
            TextId = "all",
            EngineRef = ECHO_ENGINE1_ID,
            Refs = ["ref1", "ref2"],
            SourceTokens = ["This", "is", "a", "test", "."],
            TargetTokens = ["This", "is", "a", "test", "."],
            Alignment = CreateNAlignedWordPair(5),
            ModelRevision = 1
        };
        await _env.WordAlignments.InsertAsync(wordAlignment);

        ICollection<Client.WordAlignment> results = await client.GetAllWordAlignmentsAsync(
            ECHO_ENGINE1_ID,
            addedCorpus.Id,
            "all"
        );
        Assert.That(results.All(p => p.TextId == "all"), Is.True);
    }

    [Test]
    public async Task GetAllWordAlignmentsAsync_TextIdDoesNotExist()
    {
        WordAlignmentEnginesClient client = _env.CreateWordAlignmentEnginesClient();
        WordAlignmentParallelCorpus addedCorpus = await client.AddParallelCorpusAsync(
            ECHO_ENGINE1_ID,
            TestParallelCorpusConfig
        );

        await _env.Engines.UpdateAsync(ECHO_ENGINE1_ID, u => u.Set(e => e.ModelRevision, 1));
        var wordAlignment = new WordAlignment.Models.WordAlignment
        {
            CorpusRef = addedCorpus.Id,
            TextId = "all",
            EngineRef = ECHO_ENGINE1_ID,
            Refs = ["ref1", "ref2"],
            SourceTokens = ["This", "is", "a", "test", "."],
            TargetTokens = ["This", "is", "a", "test", "."],
            Alignment = CreateNAlignedWordPair(5),
            ModelRevision = 1
        };
        await _env.WordAlignments.InsertAsync(wordAlignment);
        ICollection<Client.WordAlignment> results = await client.GetAllWordAlignmentsAsync(
            ECHO_ENGINE1_ID,
            addedCorpus.Id,
            "not_the_right_id"
        );
        Assert.That(results, Is.Empty);
    }

    [Test]
    [TestCase(new[] { Scopes.ReadWordAlignmentEngines }, 200, STATISTICAL_ENGINE_ID)]
    [TestCase(new[] { Scopes.ReadWordAlignmentEngines }, 404, DOES_NOT_EXIST_ENGINE_ID, false)]
    [TestCase(new[] { Scopes.ReadFiles }, 403, ECHO_ENGINE1_ID)] //Arbitrary unrelated privilege
    public async Task GetAllBuildsForEngineByIdAsync(
        IEnumerable<string> scope,
        int expectedStatusCode,
        string engineId,
        bool addBuild = true
    )
    {
        WordAlignmentEnginesClient client = _env.CreateWordAlignmentEnginesClient(scope);
        Build? build = null;
        if (addBuild)
        {
            build = new Build { EngineRef = engineId };
            await _env.Builds.InsertAsync(build);
        }
        switch (expectedStatusCode)
        {
            case 200:
                ICollection<WordAlignmentBuild> results = await client.GetAllBuildsAsync(engineId);
                Assert.That(results, Is.Not.Empty);
                Assert.Multiple(() =>
                {
                    Assert.That(results.First().Revision, Is.EqualTo(1));
                    Assert.That(results.First().Id, Is.EqualTo(build?.Id));
                    Assert.That(results.First().State, Is.EqualTo(JobState.Pending));
                });
                break;
            case 403:
            case 404:
                ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.GetAllBuildsAsync(engineId);
                });
                break;
            default:
                Assert.Fail("Unanticipated expectedStatusCode. Check test case for typo.");
                break;
        }
    }

    [Test]
    [TestCase(new[] { Scopes.ReadWordAlignmentEngines }, 200, STATISTICAL_ENGINE_ID)]
    [TestCase(new[] { Scopes.ReadWordAlignmentEngines }, 408, STATISTICAL_ENGINE_ID, true)]
    [TestCase(new[] { Scopes.ReadWordAlignmentEngines }, 404, DOES_NOT_EXIST_ENGINE_ID, false)]
    [TestCase(new[] { Scopes.ReadWordAlignmentEngines }, 404, STATISTICAL_ENGINE_ID, false)]
    [TestCase(new[] { Scopes.ReadFiles }, 403, ECHO_ENGINE1_ID)] //Arbitrary unrelated privilege
    public async Task GetBuildByIdForEngineByIdAsync(
        IEnumerable<string> scope,
        int expectedStatusCode,
        string engineId,
        bool addBuild = true
    )
    {
        WordAlignmentEnginesClient client = _env.CreateWordAlignmentEnginesClient(scope);
        Build? build = null;
        if (addBuild)
        {
            build = new Build { EngineRef = engineId };
            await _env.Builds.InsertAsync(build);
        }

        switch (expectedStatusCode)
        {
            case 200:
            {
                Assert.That(build, Is.Not.Null);
                WordAlignmentBuild result = await client.GetBuildAsync(engineId, build.Id);
                Assert.Multiple(() =>
                {
                    Assert.That(result.Revision, Is.EqualTo(1));
                    Assert.That(result.Id, Is.EqualTo(build.Id));
                    Assert.That(result.State, Is.EqualTo(JobState.Pending));
                });
                break;
            }
            case 403:
            case 404:
            {
                ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.GetBuildAsync(engineId, "bbbbbbbbbbbbbbbbbbbbbbbb");
                });
                Assert.That(ex?.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
            }
            case 408:
            {
                Assert.That(build, Is.Not.Null);
                ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.GetBuildAsync(engineId, build.Id, 3);
                });
                Assert.That(ex?.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
            }
            default:
                Assert.Fail("Unanticipated expectedStatusCode. Check test case for typo.");
                break;
        }
    }

    [Test]
    [TestCase(
        new[] { Scopes.UpdateWordAlignmentEngines, Scopes.CreateWordAlignmentEngines, Scopes.ReadWordAlignmentEngines },
        201,
        ECHO_ENGINE1_ID
    )]
    [TestCase(
        new[] { Scopes.UpdateWordAlignmentEngines, Scopes.CreateWordAlignmentEngines, Scopes.ReadWordAlignmentEngines },
        404,
        DOES_NOT_EXIST_ENGINE_ID
    )]
    [TestCase(
        new[] { Scopes.UpdateWordAlignmentEngines, Scopes.CreateWordAlignmentEngines, Scopes.ReadWordAlignmentEngines },
        400,
        ECHO_ENGINE1_ID
    )]
    [TestCase(new[] { Scopes.ReadFiles }, 403, ECHO_ENGINE1_ID)] //Arbitrary unrelated privilege
    public async Task StartBuildForEngineByIdAsync(IEnumerable<string> scope, int expectedStatusCode, string engineId)
    {
        WordAlignmentEnginesClient client = _env.CreateWordAlignmentEnginesClient(scope);
        TrainingCorpusConfig tcc;
        WordAlignmentCorpusConfig wacc;
        WordAlignmentBuildConfig tbc;
        switch (expectedStatusCode)
        {
            case 201:
                WordAlignmentParallelCorpus addedCorpus = await client.AddParallelCorpusAsync(
                    engineId,
                    TestParallelCorpusConfig
                );
                tcc = new TrainingCorpusConfig
                {
                    ParallelCorpusId = addedCorpus.Id,
                    SourceFilters =
                    [
                        new ParallelCorpusFilterConfig { CorpusId = SOURCE_CORPUS_ID_1, TextIds = ["all"] }
                    ],
                    TargetFilters = [new ParallelCorpusFilterConfig { CorpusId = TARGET_CORPUS_ID, TextIds = ["all"] }]
                };
                wacc = new WordAlignmentCorpusConfig
                {
                    ParallelCorpusId = addedCorpus.Id,
                    SourceFilters =
                    [
                        new ParallelCorpusFilterConfig { CorpusId = SOURCE_CORPUS_ID_1, TextIds = ["all"] }
                    ],
                    TargetFilters = [new ParallelCorpusFilterConfig { CorpusId = TARGET_CORPUS_ID, TextIds = ["all"] }]
                };
                tbc = new WordAlignmentBuildConfig
                {
                    WordAlignOn = [wacc],
                    TrainOn = [tcc],
                    Options = """
                        {"max_steps":10,
                        "use_key_terms":false,
                        "some_double":10.5,
                        "some_nested": {"more_nested": {"other_double":10.5}},
                        "some_string":"string"}
                        """
                };
                WordAlignmentBuild resultAfterStart;
                Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    resultAfterStart = await client.GetCurrentBuildAsync(engineId);
                });

                WordAlignmentBuild build = await client.StartBuildAsync(engineId, tbc);
                Assert.That(build, Is.Not.Null);

                build = await client.GetCurrentBuildAsync(engineId);
                Assert.That(build, Is.Not.Null);

                Assert.That(build.DeploymentVersion, Is.Not.Null);

                break;
            case 400:
            case 403:
            case 404:

                tcc = new TrainingCorpusConfig
                {
                    ParallelCorpusId = "cccccccccccccccccccccccc",
                    SourceFilters =
                    [
                        new ParallelCorpusFilterConfig { CorpusId = "ccccccccccccccccccccccc1", TextIds = ["all"] }
                    ],
                    TargetFilters =
                    [
                        new ParallelCorpusFilterConfig { CorpusId = "ccccccccccccccccccccccc1", TextIds = ["all"] }
                    ]
                };
                wacc = new WordAlignmentCorpusConfig
                {
                    ParallelCorpusId = "cccccccccccccccccccccccc",
                    SourceFilters =
                    [
                        new ParallelCorpusFilterConfig { CorpusId = "ccccccccccccccccccccccc1", TextIds = ["all"] }
                    ],
                    TargetFilters =
                    [
                        new ParallelCorpusFilterConfig { CorpusId = "ccccccccccccccccccccccc1", TextIds = ["all"] }
                    ]
                };
                tbc = new WordAlignmentBuildConfig { WordAlignOn = [wacc], TrainOn = [tcc] };
                ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.StartBuildAsync(engineId, tbc);
                });
                Assert.That(ex?.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
            default:
                Assert.Fail("Unanticipated expectedStatusCode. Check test case for typo.");
                break;
        }
    }

    [Test]
    public void AddParallelCorpusAsync_EmptyParallelCorpus()
    {
        WordAlignmentEnginesClient client = _env.CreateWordAlignmentEnginesClient();
        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            WordAlignmentParallelCorpus addedCorpus = await client.AddParallelCorpusAsync(
                ECHO_ENGINE1_ID,
                TestParallelCorpusConfigEmptySource
            );
        });
        Assert.That(ex, Is.Not.Null);
        Assert.That(ex.StatusCode, Is.EqualTo(400));
    }

    [Test]
    public async Task StartBuildForEngineAsync_NoCorporaAligning()
    {
        WordAlignmentEnginesClient client = _env.CreateWordAlignmentEnginesClient();
        WordAlignmentParallelCorpus addedCorpus = await client.AddParallelCorpusAsync(
            ECHO_ENGINE1_ID,
            TestParallelCorpusConfigNoCorpora
        );
        WordAlignmentCorpusConfig wacc = new() { ParallelCorpusId = addedCorpus.Id };
        WordAlignmentBuildConfig tbc = new() { WordAlignOn = [wacc] };
        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            await client.StartBuildAsync(ECHO_ENGINE1_ID, tbc);
        });
        Assert.That(ex, Is.Not.Null);
        Assert.That(ex.StatusCode, Is.EqualTo(400));
    }

    [Test]
    public async Task StartBuildForEngineAsync_NoCorporaTraining()
    {
        WordAlignmentEnginesClient client = _env.CreateWordAlignmentEnginesClient();
        WordAlignmentParallelCorpus addedCorpus = await client.AddParallelCorpusAsync(
            ECHO_ENGINE1_ID,
            TestParallelCorpusConfigNoCorpora
        );
        TrainingCorpusConfig tcc = new() { ParallelCorpusId = addedCorpus.Id };
        WordAlignmentBuildConfig tbc = new() { TrainOn = [tcc], };
        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            await client.StartBuildAsync(ECHO_ENGINE1_ID, tbc);
        });
        Assert.That(ex, Is.Not.Null);
        Assert.That(ex.StatusCode, Is.EqualTo(400));
    }

    [Test]
    public async Task StartBuildForEngineAsync_EmptyCorpus()
    {
        WordAlignmentEnginesClient client = _env.CreateWordAlignmentEnginesClient();
        WordAlignmentParallelCorpus addedCorpus = await client.AddParallelCorpusAsync(
            ECHO_ENGINE1_ID,
            TestParallelCorpusConfig
        );
        TrainingCorpusConfig tcc = new() { ParallelCorpusId = addedCorpus.Id };
        WordAlignmentBuildConfig tbc = new() { TrainOn = [tcc], };
        string dataFileId = FILE1_SRC_ID;
        //Below code is copy-pasted from EngineService.DeleteAllCorpusFilesAsync
        await _env.Engines.UpdateAllAsync(
            e =>
                e.ParallelCorpora.Any(c =>
                    c.SourceCorpora.Any(sc => sc.Files.Any(f => f.Id == dataFileId))
                    || c.TargetCorpora.Any(tc => tc.Files.Any(f => f.Id == dataFileId))
                ),
            u =>
            {
                u.RemoveAll(
                    e => e.ParallelCorpora.AllElements().SourceCorpora.AllElements().Files,
                    f => f.Id == dataFileId
                );
                u.RemoveAll(
                    e => e.ParallelCorpora.AllElements().TargetCorpora.AllElements().Files,
                    f => f.Id == dataFileId
                );
            }
        );
        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            await client.StartBuildAsync(ECHO_ENGINE1_ID, tbc);
        });
        Assert.That(ex, Is.Not.Null);
        Assert.That(ex.StatusCode, Is.EqualTo(400));
    }

    [Test]
    public async Task StartBuildForEngineAsync_UnparsableOptions()
    {
        WordAlignmentEnginesClient client = _env.CreateWordAlignmentEnginesClient();
        WordAlignmentParallelCorpus addedCorpus = await client.AddParallelCorpusAsync(
            ECHO_ENGINE1_ID,
            TestParallelCorpusConfig
        );
        TrainingCorpusConfig tcc =
            new()
            {
                ParallelCorpusId = addedCorpus.Id,
                SourceFilters = [new ParallelCorpusFilterConfig { CorpusId = SOURCE_CORPUS_ID_1, TextIds = ["all"] }],
                TargetFilters = [new ParallelCorpusFilterConfig { CorpusId = TARGET_CORPUS_ID, TextIds = ["all"] }]
            };
        WordAlignmentCorpusConfig wacc =
            new()
            {
                ParallelCorpusId = addedCorpus.Id,
                SourceFilters = [new ParallelCorpusFilterConfig { CorpusId = SOURCE_CORPUS_ID_1, TextIds = ["all"] }],
                TargetFilters = [new ParallelCorpusFilterConfig { CorpusId = TARGET_CORPUS_ID, TextIds = ["all"] }]
            };
        WordAlignmentBuildConfig tbc =
            new()
            {
                WordAlignOn = [wacc],
                TrainOn = [tcc],
                Options = "unparsable json"
            };
        WordAlignmentBuild resultAfterStart;
        Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            resultAfterStart = await client.GetCurrentBuildAsync(ECHO_ENGINE1_ID);
        });

        Assert.That(
            () => client.StartBuildAsync(ECHO_ENGINE1_ID, tbc),
            Throws.TypeOf<ServalApiException>().With.Message.Contains("Unable to parse field 'options'")
        );
    }

    [Test]
    [TestCase(new[] { Scopes.ReadWordAlignmentEngines }, 200, ECHO_ENGINE1_ID)]
    [TestCase(new[] { Scopes.ReadWordAlignmentEngines }, 408, ECHO_ENGINE1_ID)]
    [TestCase(new[] { Scopes.ReadWordAlignmentEngines }, 204, ECHO_ENGINE1_ID, false)]
    [TestCase(new[] { Scopes.ReadWordAlignmentEngines }, 404, DOES_NOT_EXIST_ENGINE_ID, false)]
    [TestCase(new[] { Scopes.ReadFiles }, 403, ECHO_ENGINE1_ID, false)] //Arbitrary unrelated privilege
    public async Task GetCurrentBuildForEngineByIdAsync(
        IEnumerable<string> scope,
        int expectedStatusCode,
        string engineId,
        bool addBuild = true
    )
    {
        WordAlignmentEnginesClient client = _env.CreateWordAlignmentEnginesClient(scope);
        Build? build = null;
        if (addBuild)
        {
            build = new Build
            {
                EngineRef = engineId,
                Phases =
                [
                    new BuildPhase
                    {
                        Stage = BuildPhaseStage.Train,
                        Step = 1,
                        StepCount = 2
                    }
                ]
            };
            await _env.Builds.InsertAsync(build);
        }

        switch (expectedStatusCode)
        {
            case 200:
            {
                Assert.That(build, Is.Not.Null);
                WordAlignmentBuild result = await client.GetCurrentBuildAsync(engineId);
                Assert.That(result.Id, Is.EqualTo(build.Id));
                Assert.That(
                    result.Phases![0],
                    Is.EqualTo(
                        new Phase
                        {
                            Stage = PhaseStage.Train,
                            Step = 1,
                            StepCount = 2
                        }
                    )
                );
                break;
            }
            case 204:
            case 403:
            case 404:
            {
                ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.GetCurrentBuildAsync(engineId);
                });
                Assert.That(ex?.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
            }
            case 408:
            {
                ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.GetCurrentBuildAsync(engineId, minRevision: 3);
                });
                Assert.That(ex?.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
            }
            default:
                Assert.Fail("Unanticipated expectedStatusCode. Check test case for typo.");
                break;
        }
    }

    [Test]
    [TestCase(new[] { Scopes.UpdateWordAlignmentEngines }, 200, ECHO_ENGINE1_ID)]
    [TestCase(new[] { Scopes.UpdateWordAlignmentEngines }, 404, DOES_NOT_EXIST_ENGINE_ID, false)]
    [TestCase(new[] { Scopes.UpdateWordAlignmentEngines }, 204, ECHO_ENGINE1_ID, false)]
    [TestCase(new[] { Scopes.ReadFiles }, 403, ECHO_ENGINE1_ID, false)] //Arbitrary unrelated privilege
    public async Task CancelCurrentBuildForEngineByIdAsync(
        IEnumerable<string> scope,
        int expectedStatusCode,
        string engineId,
        bool addBuild = true
    )
    {
        WordAlignmentEnginesClient client = _env.CreateWordAlignmentEnginesClient(scope);

        string buildId = "b00000000000000000000000";
        if (addBuild)
        {
            _env.EchoClient.CancelBuildAsync(
                Arg.Is(new CancelBuildRequest() { EngineId = engineId, EngineType = "EchoWordAlignment" }),
                null,
                null,
                Arg.Any<CancellationToken>()
            )
                .Returns(CreateAsyncUnaryCall(new CancelBuildResponse() { BuildId = buildId }));
            var build = new Build { Id = buildId, EngineRef = engineId };
            await _env.Builds.InsertAsync(build);
        }

        switch (expectedStatusCode)
        {
            case 200:
                WordAlignmentBuild build = await client.CancelBuildAsync(engineId);
                Assert.That(build.Id, Is.EqualTo(buildId));
                break;
            case 204:
            case 403:
            case 404:
                ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.CancelBuildAsync(engineId);
                });
                Assert.That(ex?.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
            default:
                Assert.Fail("Unanticipated expectedStatusCode. Check test case for typo.");
                break;
        }
    }

    [Test]
    public async Task StartBuildAsync_ParallelCorpus()
    {
        WordAlignmentEnginesClient client = _env.CreateWordAlignmentEnginesClient();
        WordAlignmentParallelCorpus addedCorpus = await client.AddParallelCorpusAsync(
            STATISTICAL_ENGINE_ID,
            TestParallelCorpusConfig
        );
        TrainingCorpusConfig tcc =
            new()
            {
                ParallelCorpusId = addedCorpus.Id,
                SourceFilters = [new() { CorpusId = SOURCE_CORPUS_ID_1, TextIds = ["all"] }],
                TargetFilters = [new() { CorpusId = TARGET_CORPUS_ID, TextIds = ["all"] }]
            };
        ;
        WordAlignmentCorpusConfig wacc =
            new()
            {
                ParallelCorpusId = addedCorpus.Id,
                SourceFilters = [new() { CorpusId = SOURCE_CORPUS_ID_1, TextIds = ["all"] }],
                TargetFilters = [new() { CorpusId = TARGET_CORPUS_ID, TextIds = ["all"] }]
            };
        ;
        WordAlignmentBuildConfig tbc = new WordAlignmentBuildConfig
        {
            WordAlignOn = [wacc],
            TrainOn = [tcc],
            Options = """
                {"max_steps":10,
                "use_key_terms":false,
                "some_double":10.5,
                "some_nested": {"more_nested": {"other_double":10.5}},
                "some_string":"string"}
                """
        };
        WordAlignmentBuild resultAfterStart;
        Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            resultAfterStart = await client.GetCurrentBuildAsync(STATISTICAL_ENGINE_ID);
        });

        WordAlignmentBuild build = await client.StartBuildAsync(STATISTICAL_ENGINE_ID, tbc);
        Assert.That(build, Is.Not.Null);

        build = await client.GetCurrentBuildAsync(STATISTICAL_ENGINE_ID);
        Assert.That(build, Is.Not.Null);
    }

    [Test]
    public async Task StartBuildAsync_ParallelCorpus_NoFilter()
    {
        WordAlignmentEnginesClient client = _env.CreateWordAlignmentEnginesClient();
        WordAlignmentParallelCorpus addedCorpus = await client.AddParallelCorpusAsync(
            STATISTICAL_ENGINE_ID,
            TestParallelCorpusConfig
        );
        TrainingCorpusConfig tcc =
            new()
            {
                ParallelCorpusId = addedCorpus.Id,
                SourceFilters = [new() { CorpusId = SOURCE_CORPUS_ID_1 }],
                TargetFilters = [new() { CorpusId = TARGET_CORPUS_ID }]
            };
        ;
        WordAlignmentCorpusConfig wacc =
            new()
            {
                ParallelCorpusId = addedCorpus.Id,
                SourceFilters = [new() { CorpusId = SOURCE_CORPUS_ID_1 }],
                TargetFilters = [new() { CorpusId = TARGET_CORPUS_ID }]
            };
        ;
        WordAlignmentBuildConfig tbc = new WordAlignmentBuildConfig
        {
            WordAlignOn = [wacc],
            TrainOn = [tcc],
            Options = """
                {"max_steps":10,
                "use_key_terms":false,
                "some_double":10.5,
                "some_nested": {"more_nested": {"other_double":10.5}},
                "some_string":"string"}
                """
        };
        WordAlignmentBuild resultAfterStart;
        Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            resultAfterStart = await client.GetCurrentBuildAsync(STATISTICAL_ENGINE_ID);
        });

        WordAlignmentBuild build = await client.StartBuildAsync(STATISTICAL_ENGINE_ID, tbc);
        Assert.That(build, Is.Not.Null);
        Assert.That(build.TrainOn, Is.Not.Null);
        Assert.That(build.TrainOn.Count, Is.EqualTo(1));
        Assert.That(build.TrainOn[0].SourceFilters, Is.Not.Null);
        Assert.That(build.TrainOn[0].TargetFilters, Is.Not.Null);
        Assert.That(build.WordAlignOn, Is.Not.Null);
        Assert.That(build.WordAlignOn.Count, Is.EqualTo(1));
        Assert.That(build.WordAlignOn[0].SourceFilters, Is.Not.Null);
        Assert.That(build.WordAlignOn[0].TargetFilters, Is.Not.Null);

        build = await client.GetCurrentBuildAsync(STATISTICAL_ENGINE_ID);
        Assert.That(build, Is.Not.Null);
    }

    [Test]
    public async Task StartBuildAsync_ParallelCorpus_WordAlignNoCorpusSpecified()
    {
        WordAlignmentEnginesClient client = _env.CreateWordAlignmentEnginesClient();
        WordAlignmentParallelCorpus addedParallelCorpus = await client.AddParallelCorpusAsync(
            STATISTICAL_ENGINE_ID,
            TestMixedParallelCorpusConfig
        );
        WordAlignmentCorpusConfig wacc = new() { };
        TrainingCorpusConfig tcc = new() { ParallelCorpusId = addedParallelCorpus.Id };
        WordAlignmentBuildConfig tbc = new WordAlignmentBuildConfig { WordAlignOn = [wacc], TrainOn = [tcc] };
        WordAlignmentBuild resultAfterStart;
        Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            resultAfterStart = await client.StartBuildAsync(STATISTICAL_ENGINE_ID, tbc);
        });
    }

    [Test]
    public async Task StartBuildAsync_ParallelCorpus_WordAlignFilterOnMultipleSources()
    {
        WordAlignmentEnginesClient client = _env.CreateWordAlignmentEnginesClient();
        WordAlignmentParallelCorpus addedParallelCorpus = await client.AddParallelCorpusAsync(
            STATISTICAL_ENGINE_ID,
            TestParallelCorpusConfig
        );
        WordAlignmentCorpusConfig wacc =
            new()
            {
                ParallelCorpusId = addedParallelCorpus.Id,
                SourceFilters =
                [
                    new ParallelCorpusFilterConfig() { CorpusId = SOURCE_CORPUS_ID_1 },
                    new ParallelCorpusFilterConfig() { CorpusId = SOURCE_CORPUS_ID_2 }
                ]
            };
        TrainingCorpusConfig tcc = new() { ParallelCorpusId = addedParallelCorpus.Id };
        WordAlignmentBuildConfig tbc = new WordAlignmentBuildConfig { WordAlignOn = [wacc], TrainOn = [tcc] };
        Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            await client.StartBuildAsync(STATISTICAL_ENGINE_ID, tbc);
        });
    }

    [Test]
    public async Task StartBuildAsync_ParallelCorpus_TrainOnNoCorpusSpecified()
    {
        WordAlignmentEnginesClient client = _env.CreateWordAlignmentEnginesClient();
        WordAlignmentParallelCorpus addedParallelCorpus = await client.AddParallelCorpusAsync(
            STATISTICAL_ENGINE_ID,
            TestParallelCorpusConfig
        );
        WordAlignmentCorpusConfig wacc = new() { ParallelCorpusId = addedParallelCorpus.Id };
        TrainingCorpusConfig tcc = new() { };
        WordAlignmentBuildConfig tbc = new WordAlignmentBuildConfig { WordAlignOn = [wacc], TrainOn = [tcc] };
        WordAlignmentBuild resultAfterStart;
        Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            resultAfterStart = await client.StartBuildAsync(STATISTICAL_ENGINE_ID, tbc);
        });
    }

    [Test]
    public async Task TryToQueueMultipleBuildsPerSingleUser()
    {
        WordAlignmentEnginesClient client = _env.CreateWordAlignmentEnginesClient();
        string engineId = STATISTICAL_ENGINE_ID;
        int expectedStatusCode = 409;
        WordAlignmentParallelCorpus addedCorpus = await client.AddParallelCorpusAsync(
            engineId,
            TestParallelCorpusConfig
        );
        WordAlignmentCorpusConfig wacc = new() { ParallelCorpusId = addedCorpus.Id };
        var tbc = new WordAlignmentBuildConfig { WordAlignOn = [wacc] };
        WordAlignmentBuild build = await client.StartBuildAsync(engineId, tbc);
        _env.StatisticalClient.StartBuildAsync(Arg.Any<StartBuildRequest>(), null, null, Arg.Any<CancellationToken>())
            .Returns(CreateAsyncUnaryCall<Empty>(StatusCode.Aborted));
        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            build = await client.StartBuildAsync(engineId, tbc);
        });
        Assert.That(ex, Is.Not.Null);
        Assert.That(ex.StatusCode, Is.EqualTo(expectedStatusCode));
    }

    [Test]
    public async Task GetWordAlignmentsByTextId()
    {
        WordAlignmentEnginesClient client = _env.CreateWordAlignmentEnginesClient();
        WordAlignmentParallelCorpus addedCorpus = await client.AddParallelCorpusAsync(
            ECHO_ENGINE1_ID,
            TestParallelCorpusConfigScripture
        );

        await _env.Engines.UpdateAsync(ECHO_ENGINE1_ID, u => u.Set(e => e.ModelRevision, 1));
        var wordAlignment = new WordAlignment.Models.WordAlignment
        {
            CorpusRef = addedCorpus.Id,
            TextId = "MAT",
            EngineRef = ECHO_ENGINE1_ID,
            Refs = ["MAT 1:1"],
            SourceTokens = ["This", "is", "a", "test", "."],
            TargetTokens = ["This", "is", "a", "test", "."],
            Alignment = CreateNAlignedWordPair(5),
            ModelRevision = 1
        };
        await _env.WordAlignments.InsertAsync(wordAlignment);

        IList<Client.WordAlignment> wordAlignments = await client.GetAllWordAlignmentsAsync(
            ECHO_ENGINE1_ID,
            addedCorpus.Id,
            "MAT"
        );
        Assert.That(wordAlignments, Has.Count.EqualTo(1));
        Assert.That(wordAlignments[0].SourceTokens, Is.EqualTo(new[] { "This", "is", "a", "test", "." }));
    }

    [Test]
    public void GetWordAlignmentsByTextId_EngineDoesNotExist()
    {
        WordAlignmentEnginesClient client = _env.CreateWordAlignmentEnginesClient();
        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            await client.GetAllWordAlignmentsAsync(DOES_NOT_EXIST_ENGINE_ID, DOES_NOT_EXIST_CORPUS_ID, "MAT");
        });
        Assert.That(ex?.StatusCode, Is.EqualTo(404));
    }

    [Test]
    public async Task DataFileUpdate_Propagated()
    {
        WordAlignmentEnginesClient client = _env.CreateWordAlignmentEnginesClient();
        DataFilesClient dataFilesClient = _env.CreateDataFilesClient();
        CorporaClient corporaClient = _env.CreateCorporaClient();
        await client.AddParallelCorpusAsync(ECHO_ENGINE1_ID, TestParallelCorpusConfig);

        // Get the original files
        DataFile orgFileFromClient = await dataFilesClient.GetAsync(FILE1_SRC_ID);
        DataFiles.Models.DataFile orgFileFromRepo = (await _env.DataFiles.GetAsync(FILE1_SRC_ID))!;
        DataFiles.Models.Corpus orgCorpusFromRepo = (await _env.Corpora.GetAsync(TARGET_CORPUS_ID))!;
        Assert.That(orgFileFromClient.Name, Is.EqualTo(orgFileFromRepo.Name));
        Assert.That(orgCorpusFromRepo.Files[0].FileRef, Is.EqualTo(FILE2_TRG_ID));

        // Update the file
        await dataFilesClient.UpdateAsync(FILE1_SRC_ID, new FileParameter(new MemoryStream([1, 2, 3]), "test.txt"));
        await corporaClient.UpdateAsync(
            TARGET_CORPUS_ID,
            [new CorpusFileConfig { FileId = FILE4_TRG_ZIP_ID, TextId = "all" }]
        );

        // Confirm the change is propagated everywhere
        DataFiles.Models.DataFile newFileFromRepo = (await _env.DataFiles.GetAsync(FILE1_SRC_ID))!;
        Assert.That(newFileFromRepo.Filename, Is.Not.EqualTo(orgFileFromRepo.Filename));

        Engine newEngine = (await _env.Engines.GetAsync(ECHO_ENGINE1_ID))!;

        // Updated parallel corpus file filename
        Assert.That(
            newEngine.ParallelCorpora[0].SourceCorpora[0].Files[0].Filename,
            Is.EqualTo(newFileFromRepo.Filename)
        );

        // Updated set of new corpus files
        Assert.That(newEngine.ParallelCorpora[0].TargetCorpora[0].Id, Is.EqualTo(TARGET_CORPUS_ID));
        Assert.That(newEngine.ParallelCorpora[0].TargetCorpora[0].Files[0].Id, Is.EqualTo(FILE4_TRG_ZIP_ID));
        Assert.That(newEngine.ParallelCorpora[0].TargetCorpora[0].Files[0].Filename, Is.EqualTo(FILE4_FILENAME));
        Assert.That(newEngine.ParallelCorpora[0].TargetCorpora[0].Files.Count, Is.EqualTo(1));
    }

    [TearDown]
    public void TearDown()
    {
        _env.Dispose();
    }

    private static IReadOnlyList<WordAlignment.Models.AlignedWordPair> CreateNAlignedWordPair(int numberOfAlignedWords)
    {
        var alignedWordPairs = new List<WordAlignment.Models.AlignedWordPair>();
        for (int i = 0; i < numberOfAlignedWords; i++)
        {
            alignedWordPairs.Add(new WordAlignment.Models.AlignedWordPair { SourceIndex = i, TargetIndex = i });
        }
        return alignedWordPairs;
    }

    private class TestEnvironment : DisposableBase
    {
        private readonly IServiceScope _scope;
        private readonly MongoClient _mongoClient;

        public TestEnvironment()
        {
            _mongoClient = new MongoClient();
            ResetDatabases();

            Factory = new ServalWebApplicationFactory();
            _scope = Factory.Services.CreateScope();
            Engines = _scope.ServiceProvider.GetRequiredService<IRepository<Engine>>();
            DataFiles = _scope.ServiceProvider.GetRequiredService<IRepository<DataFiles.Models.DataFile>>();
            Corpora = _scope.ServiceProvider.GetRequiredService<IRepository<DataFiles.Models.Corpus>>();
            WordAlignments = _scope.ServiceProvider.GetRequiredService<
                IRepository<WordAlignment.Models.WordAlignment>
            >();
            Builds = _scope.ServiceProvider.GetRequiredService<IRepository<Build>>();

            EchoClient = Substitute.For<WordAlignmentEngineApi.WordAlignmentEngineApiClient>();
            EchoClient
                .CreateAsync(Arg.Any<CreateRequest>(), null, null, Arg.Any<CancellationToken>())
                .Returns(CreateAsyncUnaryCall(new Empty()));
            EchoClient
                .DeleteAsync(Arg.Any<DeleteRequest>(), null, null, Arg.Any<CancellationToken>())
                .Returns(CreateAsyncUnaryCall(new Empty()));
            EchoClient
                .StartBuildAsync(Arg.Any<StartBuildRequest>(), null, null, Arg.Any<CancellationToken>())
                .Returns(CreateAsyncUnaryCall(new Empty()));
            EchoClient
                .CancelBuildAsync(Arg.Any<CancelBuildRequest>(), null, null, Arg.Any<CancellationToken>())
                .Returns(CreateAsyncUnaryCall(new CancelBuildResponse()));
            var wordAlignmentResult = new WordAlignment.V1.WordAlignmentResult
            {
                SourceTokens = { "This is a test .".Split() },
                TargetTokens = { "This is a test .".Split() },
                Alignment =
                {
                    new WordAlignment.V1.AlignedWordPair { SourceIndex = 0, TargetIndex = 0 },
                    new WordAlignment.V1.AlignedWordPair { SourceIndex = 1, TargetIndex = 1 },
                    new WordAlignment.V1.AlignedWordPair { SourceIndex = 2, TargetIndex = 2 },
                    new WordAlignment.V1.AlignedWordPair { SourceIndex = 3, TargetIndex = 3 },
                    new WordAlignment.V1.AlignedWordPair { SourceIndex = 4, TargetIndex = 4 }
                }
            };
            var wordAlignmentResponse = new GetWordAlignmentResponse { Result = wordAlignmentResult };
            EchoClient
                .GetWordAlignmentAsync(Arg.Any<GetWordAlignmentRequest>(), null, null, Arg.Any<CancellationToken>())
                .Returns(CreateAsyncUnaryCall(wordAlignmentResponse));
            EchoClient
                .GetQueueSizeAsync(Arg.Any<GetQueueSizeRequest>(), null, null, Arg.Any<CancellationToken>())
                .Returns(CreateAsyncUnaryCall(new GetQueueSizeResponse() { Size = 0 }));

            StatisticalClient = Substitute.For<WordAlignmentEngineApi.WordAlignmentEngineApiClient>();
            StatisticalClient
                .CreateAsync(Arg.Any<CreateRequest>(), null, null, Arg.Any<CancellationToken>())
                .Returns(CreateAsyncUnaryCall(new Empty()));
            StatisticalClient
                .DeleteAsync(Arg.Any<DeleteRequest>(), null, null, Arg.Any<CancellationToken>())
                .Returns(CreateAsyncUnaryCall(new Empty()));
            StatisticalClient
                .StartBuildAsync(Arg.Any<StartBuildRequest>(), null, null, Arg.Any<CancellationToken>())
                .Returns(CreateAsyncUnaryCall(new Empty()));
            StatisticalClient
                .CancelBuildAsync(Arg.Any<CancelBuildRequest>(), null, null, Arg.Any<CancellationToken>())
                .Returns(CreateAsyncUnaryCall(new CancelBuildResponse()));
            StatisticalClient
                .GetWordAlignmentAsync(Arg.Any<GetWordAlignmentRequest>(), null, null, Arg.Any<CancellationToken>())
                .Returns(CreateAsyncUnaryCall<GetWordAlignmentResponse>(StatusCode.Unimplemented));
        }

        public ServalWebApplicationFactory Factory { get; }
        public IRepository<Engine> Engines { get; }
        public IRepository<DataFiles.Models.DataFile> DataFiles { get; }
        public IRepository<DataFiles.Models.Corpus> Corpora { get; }
        public IRepository<WordAlignment.Models.WordAlignment> WordAlignments { get; }
        public IRepository<Build> Builds { get; }
        public WordAlignmentEngineApi.WordAlignmentEngineApiClient EchoClient { get; }
        public WordAlignmentEngineApi.WordAlignmentEngineApiClient StatisticalClient { get; }

        public WordAlignmentEnginesClient CreateWordAlignmentEnginesClient(IEnumerable<string>? scope = null)
        {
            scope ??= Scopes.All;
            HttpClient httpClient = Factory
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureTestServices(services =>
                    {
                        GrpcClientFactory grpcClientFactory = Substitute.For<GrpcClientFactory>();
                        grpcClientFactory
                            .CreateClient<WordAlignmentEngineApi.WordAlignmentEngineApiClient>("EchoWordAlignment")
                            .Returns(EchoClient);
                        grpcClientFactory
                            .CreateClient<WordAlignmentEngineApi.WordAlignmentEngineApiClient>("Statistical")
                            .Returns(StatisticalClient);
                        services.AddSingleton(grpcClientFactory);
                        services.AddTransient(CreateFileSystem);
                    });
                })
                .CreateClient();
            httpClient.DefaultRequestHeaders.Add("Scope", string.Join(" ", scope));
            return new WordAlignmentEnginesClient(httpClient);
        }

        public DataFilesClient CreateDataFilesClient()
        {
            IEnumerable<string> scope = [Scopes.DeleteFiles, Scopes.ReadFiles, Scopes.UpdateFiles, Scopes.CreateFiles];
            HttpClient httpClient = Factory
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureTestServices(services =>
                    {
                        services.AddTransient(CreateFileSystem);
                    });
                })
                .CreateClient();
            if (scope is not null)
                httpClient.DefaultRequestHeaders.Add("Scope", string.Join(" ", scope));
            return new DataFilesClient(httpClient);
        }

        public CorporaClient CreateCorporaClient()
        {
            IEnumerable<string> scope = [Scopes.DeleteFiles, Scopes.ReadFiles, Scopes.UpdateFiles, Scopes.CreateFiles];
            HttpClient httpClient = Factory.WithWebHostBuilder(_ => { }).CreateClient();
            if (scope is not null)
                httpClient.DefaultRequestHeaders.Add("Scope", string.Join(" ", scope));
            return new CorporaClient(httpClient);
        }

        public void ResetDatabases()
        {
            _mongoClient.DropDatabase("serval_test");
            _mongoClient.DropDatabase("serval_test_jobs");
        }

        private static IFileSystem CreateFileSystem(IServiceProvider sp)
        {
            IFileSystem fileSystem = Substitute.For<IFileSystem>();
            IOptionsMonitor<DataFileOptions> dataFileOptions = sp.GetRequiredService<
                IOptionsMonitor<DataFileOptions>
            >();
            fileSystem
                .OpenZipFile(GetFilePath(dataFileOptions, FILE3_FILENAME))
                .Returns(ci =>
                {
                    IZipContainer source = CreateZipContainer("SRC");
                    source.EntryExists("MATSRC.SFM").Returns(true);
                    string usfm =
                        $@"\id MAT - SRC
\h Matthew
\c 1
\p
\v 1 Chapter one, verse one.
\v 2 Chapter one, verse two.
";
                    source.OpenEntry("MATSRC.SFM").Returns(ci => new MemoryStream(Encoding.UTF8.GetBytes(usfm)));
                    return source;
                });
            fileSystem
                .OpenZipFile(GetFilePath(dataFileOptions, FILE4_FILENAME))
                .Returns(ci =>
                {
                    IZipContainer target = CreateZipContainer("TRG");
                    target.EntryExists("MATTRG.SFM").Returns(false);
                    return target;
                });
            fileSystem.OpenWrite(Arg.Any<string>()).Returns(ci => new MemoryStream());
            return fileSystem;
        }

        private static IZipContainer CreateZipContainer(string name)
        {
            IZipContainer container = Substitute.For<IZipContainer>();
            container.EntryExists("Settings.xml").Returns(true);
            XElement settingsXml =
                new(
                    "ScriptureText",
                    new XElement("StyleSheet", "usfm.sty"),
                    new XElement("Name", name),
                    new XElement("FullName", name),
                    new XElement("Encoding", "65001"),
                    new XElement(
                        "Naming",
                        new XAttribute("PrePart", ""),
                        new XAttribute("PostPart", $"{name}.SFM"),
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

        private static string GetFilePath(IOptionsMonitor<DataFileOptions> dataFileOptions, string fileName)
        {
            return Path.Combine(dataFileOptions.CurrentValue.FilesDirectory, fileName);
        }

        protected override void DisposeManagedResources()
        {
            _scope.Dispose();
            Factory.Dispose();
            ResetDatabases();
        }
    }
}
