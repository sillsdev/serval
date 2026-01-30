using Google.Protobuf.WellKnownTypes;
using Serval.Translation.Models;
using Serval.Translation.V1;
using SIL.ServiceToolkit.Services;
using static Serval.ApiServer.Utils;
using Phase = Serval.Client.Phase;
using PhaseStage = Serval.Client.PhaseStage;
using Queue = Serval.Client.Queue;

namespace Serval.ApiServer;

#pragma warning disable CS0612 // Type or member is obsolete

[TestFixture]
[Category("Integration")]
public class TranslationEngineTests
{
    private static readonly TranslationCorpusConfig TestCorpusConfig =
        new()
        {
            Name = "TestCorpus",
            SourceLanguage = "en",
            TargetLanguage = "en",
            SourceFiles =
            {
                new TranslationCorpusFileConfig { FileId = FILE1_SRC_ID, TextId = "all" }
            },
            TargetFiles =
            {
                new TranslationCorpusFileConfig { FileId = FILE2_TRG_ID, TextId = "all" }
            }
        };
    private static readonly TranslationParallelCorpusConfig TestParallelCorpusConfig =
        new()
        {
            Name = "TestCorpus",
            SourceCorpusIds = [SOURCE_CORPUS_ID_1],
            TargetCorpusIds = [TARGET_CORPUS_ID],
        };

    private static readonly TranslationParallelCorpusConfig TestMixedParallelCorpusConfig =
        new()
        {
            Name = "TestCorpus",
            SourceCorpusIds = [SOURCE_CORPUS_ID_1, SOURCE_CORPUS_ID_2],
            TargetCorpusIds = [TARGET_CORPUS_ID],
        };
    private static readonly TranslationCorpusConfig TestCorpusConfigNonEcho =
        new()
        {
            Name = "TestCorpus",
            SourceLanguage = "en",
            TargetLanguage = "es",
            SourceFiles =
            {
                new TranslationCorpusFileConfig { FileId = FILE1_SRC_ID, TextId = "all" }
            },
            TargetFiles =
            {
                new TranslationCorpusFileConfig { FileId = FILE2_TRG_ID, TextId = "all" }
            }
        };

    private static readonly TranslationParallelCorpusConfig TestParallelCorpusConfigEmptySource =
        new()
        {
            Name = "TestCorpus",
            SourceCorpusIds = [EMPTY_CORPUS_ID],
            TargetCorpusIds = [TARGET_CORPUS_ID],
        };

    private static readonly TranslationParallelCorpusConfig TestParallelCorpusConfigNoCorpora =
        new()
        {
            Name = "TestCorpus",
            SourceCorpusIds = [],
            TargetCorpusIds = [],
        };
    private static readonly TranslationParallelCorpusConfig TestParallelCorpusConfigScripture =
        new()
        {
            Name = "TestCorpus",
            SourceCorpusIds = [SOURCE_CORPUS_ID_PT],
            TargetCorpusIds = [TARGET_CORPUS_ID_PT],
        };

    private const string ECHO_ENGINE1_ID = "e00000000000000000000001";
    private const string ECHO_ENGINE2_ID = "e00000000000000000000002";
    private const string ECHO_ENGINE3_ID = "e00000000000000000000003";
    private const string SMT_ENGINE1_ID = "be0000000000000000000001";
    private const string NMT_ENGINE1_ID = "ce0000000000000000000001";
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
    private const string SOURCE_CORPUS_ID_PT = "cc0000000000000000000003";
    private const string TARGET_CORPUS_ID = "cc0000000000000000000004";
    private const string TARGET_CORPUS_ID_PT = "cc0000000000000000000005";
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
            Type = "Echo",
            Owner = "client1",
            Corpora = [],
            ModelRevision = 1
        };
        var e1 = new Engine
        {
            Id = ECHO_ENGINE2_ID,
            Name = "e1",
            SourceLanguage = "en",
            TargetLanguage = "en",
            Type = "Echo",
            Owner = "client1",
            Corpora = [],
            ModelRevision = 0
        };
        var e2 = new Engine
        {
            Id = ECHO_ENGINE3_ID,
            Name = "e2",
            SourceLanguage = "en",
            TargetLanguage = "en",
            Type = "Echo",
            Owner = "client2",
            Corpora = [],
            ModelRevision = 1
        };
        var be0 = new Engine
        {
            Id = SMT_ENGINE1_ID,
            Name = "be0",
            SourceLanguage = "en",
            TargetLanguage = "es",
            Type = "SMTTransfer",
            Owner = "client1",
            Corpora = [],
            ModelRevision = 1
        };
        var ce0 = new Engine
        {
            Id = NMT_ENGINE1_ID,
            Name = "ce0",
            SourceLanguage = "en",
            TargetLanguage = "es",
            Type = "Nmt",
            Owner = "client1",
            Corpora = [],
            ModelRevision = 1
        };

        await _env.Engines.InsertAllAsync([e0, e1, e2, be0, ce0]);

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
        var srcCorpusParatext = new DataFiles.Models.Corpus
        {
            Id = SOURCE_CORPUS_ID_PT,
            Language = "en",
            Owner = "client1",
            Files = [new() { FileRef = srcParatextFile.Id }]
        };
        var trgCorpus = new DataFiles.Models.Corpus
        {
            Id = TARGET_CORPUS_ID,
            Language = "en",
            Owner = "client1",
            Files = [new() { FileRef = trgFile.Id, TextId = "all" }]
        };
        var trgCorpusParatext = new DataFiles.Models.Corpus
        {
            Id = TARGET_CORPUS_ID_PT,
            Language = "en",
            Owner = "client1",
            Files = [new() { FileRef = trgParatextFile.Id }]
        };
        var emptyCorpus = new DataFiles.Models.Corpus
        {
            Id = EMPTY_CORPUS_ID,
            Language = "en",
            Owner = "client1",
            Files = []
        };
        await _env.Corpora.InsertAllAsync(
            [srcCorpus, srcCorpus2, srcCorpusParatext, trgCorpus, emptyCorpus, trgCorpusParatext]
        );
    }

    [Test]
    [TestCase(new[] { Scopes.ReadTranslationEngines }, 200)]
    [TestCase(new[] { Scopes.ReadFiles }, 403)] //Arbitrary unrelated privilege
    public async Task GetAllAsync(IEnumerable<string> scope, int expectedStatusCode)
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient(scope);
        switch (expectedStatusCode)
        {
            case 200:
                ICollection<TranslationEngine> results = await client.GetAllAsync();
                Assert.That(results, Has.Count.EqualTo(4));
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
    [TestCase(new[] { Scopes.ReadTranslationEngines }, 200, ECHO_ENGINE1_ID)]
    [TestCase(new[] { Scopes.ReadFiles }, 403, ECHO_ENGINE1_ID)] //Arbitrary unrelated privilege
    [TestCase(new[] { Scopes.ReadTranslationEngines }, 403, ECHO_ENGINE3_ID)] //Engine is not owned
    [TestCase(new[] { Scopes.ReadTranslationEngines }, 404, DOES_NOT_EXIST_ENGINE_ID)]
    [TestCase(new[] { Scopes.ReadTranslationEngines }, 404, "phony_id")]
    public async Task GetByIdAsync(IEnumerable<string> scope, int expectedStatusCode, string engineId)
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient(scope);
        switch (expectedStatusCode)
        {
            case 200:
                TranslationEngine result = await client.GetAsync(engineId);
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
    [TestCase(new[] { Scopes.CreateTranslationEngines, Scopes.ReadTranslationEngines }, 201, "Echo")]
    [TestCase(new[] { Scopes.CreateTranslationEngines }, 400, "NotARealKindOfMT")]
    [TestCase(new[] { Scopes.ReadFiles }, 403, "Echo")] //Arbitrary unrelated privilege
    public async Task CreateEngineAsync(IEnumerable<string> scope, int expectedStatusCode, string engineType)
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient(scope);
        switch (expectedStatusCode)
        {
            case 201:
                TranslationEngine result = await client.CreateAsync(
                    new TranslationEngineConfig
                    {
                        Name = "test",
                        SourceLanguage = "en",
                        TargetLanguage = "en",
                        Type = engineType
                    }
                );
                Assert.That(result.Name, Is.EqualTo("test"));
                TranslationEngine? engine = await client.GetAsync(result.Id);
                Assert.That(engine, Is.Not.Null);
                Assert.That(engine.Name, Is.EqualTo("test"));
                break;
            case 400:
            {
                ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.CreateAsync(
                        new TranslationEngineConfig
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
                        new TranslationEngineConfig
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
    [TestCase(new[] { Scopes.DeleteTranslationEngines, Scopes.ReadTranslationEngines }, 200, ECHO_ENGINE1_ID)]
    [TestCase(new[] { Scopes.ReadTranslationEngines }, 403, ECHO_ENGINE1_ID)] //Arbitrary unrelated privilege
    [TestCase(new[] { Scopes.DeleteTranslationEngines }, 404, DOES_NOT_EXIST_ENGINE_ID)]
    public async Task DeleteEngineByIdAsync(IEnumerable<string> scope, int expectedStatusCode, string engineId)
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient(scope);
        switch (expectedStatusCode)
        {
            case 200:
                await client.DeleteAsync(engineId);
                ICollection<TranslationEngine> results = await client.GetAllAsync();
                Assert.That(results, Has.Count.EqualTo(3));
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
    [TestCase(new[] { Scopes.ReadTranslationEngines, Scopes.UpdateTranslationEngines }, 200, ECHO_ENGINE1_ID)]
    [TestCase(new[] { Scopes.ReadTranslationEngines, Scopes.UpdateTranslationEngines }, 404, DOES_NOT_EXIST_ENGINE_ID)]
    [TestCase(new[] { Scopes.ReadTranslationEngines, Scopes.UpdateTranslationEngines }, 409, ECHO_ENGINE1_ID)]
    [TestCase(new[] { Scopes.ReadFiles }, 403, ECHO_ENGINE1_ID)] //Arbitrary unrelated privilege
    public async Task TranslateSegmentWithEngineByIdAsync(
        IEnumerable<string> scope,
        int expectedStatusCode,
        string engineId
    )
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient(scope);
        switch (expectedStatusCode)
        {
            case 200:
                await _env.Builds.InsertAsync(
                    new Build
                    {
                        EngineRef = engineId,
                        Owner = "client1",
                        State = Shared.Contracts.JobState.Completed
                    }
                );
                Client.TranslationResult result = await client.TranslateAsync(engineId, "This is a test .");
                Assert.That(result.Translation, Is.EqualTo("This is a test ."));
                Assert.That(result.Sources, Has.Count.EqualTo(5));
                Assert.That(
                    result.Sources,
                    Has.All.EquivalentTo(new[] { Client.TranslationSource.Primary, Client.TranslationSource.Secondary })
                );
                break;
            case 409:
            {
                _env.EchoClient.TranslateAsync(Arg.Any<TranslateRequest>(), null, null, Arg.Any<CancellationToken>())
                    .Returns(CreateAsyncUnaryCall<TranslateResponse>(StatusCode.FailedPrecondition));
                ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.TranslateAsync(engineId, "This is a test .");
                });
                Assert.That(ex?.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
            }
            case 403:
            case 404:
            {
                ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.TranslateAsync(engineId, "This is a test .");
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
    [TestCase(new[] { Scopes.ReadTranslationEngines, Scopes.UpdateTranslationEngines }, 200, ECHO_ENGINE1_ID)]
    [TestCase(new[] { Scopes.ReadTranslationEngines }, 404, DOES_NOT_EXIST_ENGINE_ID)]
    [TestCase(new[] { Scopes.ReadTranslationEngines, Scopes.UpdateTranslationEngines }, 409, ECHO_ENGINE1_ID)]
    [TestCase(new[] { Scopes.ReadFiles }, 403, ECHO_ENGINE1_ID)] //Arbitrary unrelated privilege
    public async Task TranslateNSegmentWithEngineByIdAsync(
        IEnumerable<string> scope,
        int expectedStatusCode,
        string engineId
    )
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient(scope);
        switch (expectedStatusCode)
        {
            case 200:
                await _env.Builds.InsertAsync(
                    new Build
                    {
                        EngineRef = engineId,
                        Owner = "client1",
                        State = Shared.Contracts.JobState.Completed
                    }
                );
                ICollection<Client.TranslationResult> results = await client.TranslateNAsync(
                    engineId,
                    1,
                    "This is a test ."
                );
                Assert.That(results, Has.Count.EqualTo(1));
                Assert.That(results.First().Translation, Is.EqualTo("This is a test ."));
                Assert.That(results.First().Sources, Has.Count.EqualTo(5));
                Assert.That(
                    results.First().Sources,
                    Has.All.EquivalentTo(new[] { Client.TranslationSource.Primary, Client.TranslationSource.Secondary })
                );
                break;
            case 409:
            {
                _env.EchoClient.TranslateAsync(Arg.Any<TranslateRequest>(), null, null, Arg.Any<CancellationToken>())
                    .Returns(CreateAsyncUnaryCall<TranslateResponse>(StatusCode.FailedPrecondition));
                ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.TranslateNAsync(engineId, 1, "This is a test .");
                });
                Assert.That(ex?.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
            }
            case 403:
            case 404:
            {
                ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.TranslateNAsync(engineId, 1, "This is a test .");
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
    [TestCase(new[] { Scopes.ReadTranslationEngines, Scopes.UpdateTranslationEngines }, 200, ECHO_ENGINE1_ID)]
    [TestCase(new[] { Scopes.ReadTranslationEngines, Scopes.UpdateTranslationEngines }, 404, DOES_NOT_EXIST_ENGINE_ID)]
    [TestCase(new[] { Scopes.ReadTranslationEngines, Scopes.UpdateTranslationEngines }, 409, ECHO_ENGINE1_ID)]
    [TestCase(new[] { Scopes.ReadFiles }, 403, ECHO_ENGINE1_ID)] //Arbitrary unrelated privilege
    public async Task GetWordGraphForSegmentByIdAsync(
        IEnumerable<string> scope,
        int expectedStatusCode,
        string engineId
    )
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient(scope);
        switch (expectedStatusCode)
        {
            case 200:
                TranslationCorpus addedCorpus = await client.AddCorpusAsync(engineId, TestCorpusConfig);
                await _env.Builds.InsertAsync(
                    new Build
                    {
                        EngineRef = engineId,
                        Owner = "client1",
                        State = Shared.Contracts.JobState.Completed
                    }
                );
                Client.WordGraph wg = await client.GetWordGraphAsync(engineId, "This is a test .");
                Assert.Multiple(() =>
                {
                    Assert.That(wg.FinalStates[0], Is.EqualTo(4));
                    Assert.That(wg.Arcs, Has.Count.EqualTo(4));
                });
                break;
            case 409:
            {
                _env.EchoClient.GetWordGraphAsync(
                    Arg.Any<GetWordGraphRequest>(),
                    null,
                    null,
                    Arg.Any<CancellationToken>()
                )
                    .Returns(CreateAsyncUnaryCall<GetWordGraphResponse>(StatusCode.FailedPrecondition));
                ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.GetWordGraphAsync(engineId, "This is a test .");
                });
                Assert.That(ex?.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
            }
            case 403:
            case 404:
            {
                ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.GetWordGraphAsync(engineId, "This is a test .");
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
    [TestCase(new[] { Scopes.UpdateTranslationEngines }, 200, ECHO_ENGINE1_ID)]
    [TestCase(new[] { Scopes.UpdateTranslationEngines }, 404, DOES_NOT_EXIST_ENGINE_ID)]
    [TestCase(new[] { Scopes.UpdateTranslationEngines }, 409, ECHO_ENGINE1_ID)]
    [TestCase(new[] { Scopes.ReadFiles }, 403, ECHO_ENGINE1_ID)] //Arbitrary unrelated privilege
    public async Task TrainEngineByIdOnSegmentPairAsync(
        IEnumerable<string> scope,
        int expectedStatusCode,
        string engineId
    )
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient(scope);
        var sp = new SegmentPair
        {
            SourceSegment = "This is a test .",
            TargetSegment = "This is a test .",
            SentenceStart = true
        };
        switch (expectedStatusCode)
        {
            case 200:
                TranslationCorpus addedCorpus = await client.AddCorpusAsync(engineId, TestCorpusConfig);
                await _env.Builds.InsertAsync(
                    new Build
                    {
                        EngineRef = engineId,
                        Owner = "client1",
                        State = Shared.Contracts.JobState.Completed
                    }
                );
                await client.TrainSegmentAsync(engineId, sp);
                break;
            case 409:
            {
                _env.EchoClient.TrainSegmentPairAsync(
                    Arg.Any<TrainSegmentPairRequest>(),
                    null,
                    null,
                    Arg.Any<CancellationToken>()
                )
                    .Returns(CreateAsyncUnaryCall<Empty>(StatusCode.FailedPrecondition));
                ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.TrainSegmentAsync(engineId, sp);
                });
                Assert.That(ex?.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
            }
            case 403:
            case 404:
            {
                ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.TrainSegmentAsync(engineId, sp);
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
    [TestCase(new[] { Scopes.UpdateTranslationEngines }, 201, ECHO_ENGINE1_ID)]
    [TestCase(new[] { Scopes.UpdateTranslationEngines }, 404, DOES_NOT_EXIST_ENGINE_ID)]
    [TestCase(new[] { Scopes.ReadFiles }, 403, ECHO_ENGINE1_ID)] //Arbitrary unrelated privilege
    public async Task AddCorpusToEngineByIdAsync(IEnumerable<string> scope, int expectedStatusCode, string engineId)
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient(scope);
        switch (expectedStatusCode)
        {
            case 201:
            {
                TranslationCorpus result = await client.AddCorpusAsync(engineId, TestCorpusConfig);
                Assert.Multiple(() =>
                {
                    Assert.That(result.Name, Is.EqualTo("TestCorpus"));
                    Assert.That(result.SourceFiles.First().File.Id, Is.EqualTo(FILE1_SRC_ID));
                    Assert.That(result.TargetFiles.First().File.Id, Is.EqualTo(FILE2_TRG_ID));
                });
                Engine? engine = await _env.Engines.GetAsync(engineId);
                Assert.That(engine, Is.Not.Null);
                Assert.Multiple(() =>
                {
                    Assert.That(engine.Corpora[0].SourceFiles[0].Filename, Is.EqualTo(FILE1_FILENAME));
                    Assert.That(engine.Corpora[0].TargetFiles[0].Filename, Is.EqualTo(FILE2_FILENAME));
                });
                break;
            }
            case 403:
            case 404:
                ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.AddCorpusAsync(engineId, TestCorpusConfig);
                });
                Assert.That(ex?.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
            default:
                Assert.Fail("Unanticipated expectedStatusCode. Check test case for typo.");
                break;
        }
    }

    [Test]
    [TestCase(
        new[] { Scopes.UpdateTranslationEngines, Scopes.CreateTranslationEngines, Scopes.ReadTranslationEngines },
        200,
        ECHO_ENGINE1_ID
    )]
    [TestCase(
        new[] { Scopes.UpdateTranslationEngines, Scopes.CreateTranslationEngines, Scopes.ReadTranslationEngines },
        404,
        ECHO_ENGINE1_ID
    )]
    [TestCase(
        new[] { Scopes.UpdateTranslationEngines, Scopes.CreateTranslationEngines, Scopes.ReadTranslationEngines },
        404,
        DOES_NOT_EXIST_ENGINE_ID
    )]
    [TestCase(new[] { Scopes.ReadFiles }, 403, ECHO_ENGINE1_ID)] //Arbitrary unrelated privilege
    public async Task UpdateCorpusByIdForEngineByIdAsync(
        IEnumerable<string> scope,
        int expectedStatusCode,
        string engineId
    )
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient(scope);
        switch (expectedStatusCode)
        {
            case 200:
            {
                TranslationCorpus result = await client.AddCorpusAsync(engineId, TestCorpusConfig);
                TranslationCorpusFileConfig[] src = new[]
                {
                    new TranslationCorpusFileConfig { FileId = FILE2_TRG_ID, TextId = "all" }
                };
                TranslationCorpusFileConfig[] trg = new[]
                {
                    new TranslationCorpusFileConfig { FileId = FILE1_SRC_ID, TextId = "all" }
                };
                var updateConfig = new TranslationCorpusUpdateConfig { SourceFiles = src, TargetFiles = trg };
                await client.UpdateCorpusAsync(engineId, result.Id, updateConfig);
                Engine? engine = await _env.Engines.GetAsync(engineId);
                Assert.That(engine, Is.Not.Null);
                Assert.Multiple(() =>
                {
                    Assert.That(engine.Corpora[0].SourceFiles[0].Filename, Is.EqualTo(FILE2_FILENAME));
                    Assert.That(engine.Corpora[0].TargetFiles[0].Filename, Is.EqualTo(FILE1_FILENAME));
                });
                break;
            }
            case 400:
            case 403:
            case 404:
                ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    TranslationCorpusFileConfig[] src = new[]
                    {
                        new TranslationCorpusFileConfig { FileId = FILE2_TRG_ID, TextId = "all" }
                    };
                    TranslationCorpusFileConfig[] trg = new[]
                    {
                        new TranslationCorpusFileConfig { FileId = FILE1_SRC_ID, TextId = "all" }
                    };
                    var updateConfig = new TranslationCorpusUpdateConfig { SourceFiles = src, TargetFiles = trg };
                    await client.UpdateCorpusAsync(engineId, DOES_NOT_EXIST_CORPUS_ID, updateConfig);
                });
                Assert.That(ex?.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
            default:
                Assert.Fail("Unanticipated expectedStatusCode. Check test case for typo.");
                break;
        }
    }

    [Test]
    [TestCase(
        new[] { Scopes.UpdateTranslationEngines, Scopes.CreateTranslationEngines, Scopes.ReadTranslationEngines },
        200,
        ECHO_ENGINE1_ID
    )]
    [TestCase(
        new[] { Scopes.UpdateTranslationEngines, Scopes.CreateTranslationEngines, Scopes.ReadTranslationEngines },
        404,
        DOES_NOT_EXIST_ENGINE_ID
    )]
    [TestCase(new[] { Scopes.ReadFiles }, 403, ECHO_ENGINE1_ID)] //Arbitrary unrelated privilege
    public async Task GetAllCorporaForEngineByIdAsync(
        IEnumerable<string> scope,
        int expectedStatusCode,
        string engineId
    )
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient(scope);
        switch (expectedStatusCode)
        {
            case 200:
                TranslationCorpus result = await client.AddCorpusAsync(engineId, TestCorpusConfig);
                TranslationCorpus resultAfterAdd = (await client.GetAllCorporaAsync(engineId)).First();
                Assert.Multiple(() =>
                {
                    Assert.That(resultAfterAdd.Name, Is.EqualTo(result.Name));
                    Assert.That(resultAfterAdd.SourceLanguage, Is.EqualTo(result.SourceLanguage));
                    Assert.That(resultAfterAdd.TargetLanguage, Is.EqualTo(result.TargetLanguage));
                });
                break;
            case 403:
            case 404:
                ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    TranslationCorpus result = (await client.GetAllCorporaAsync(engineId)).First();
                });
                Assert.That(ex?.StatusCode, Is.EqualTo(expectedStatusCode));
                break;

            default:
                Assert.Fail("Unanticipated expectedStatusCode. Check test case for typo.");
                break;
        }
    }

    [Test]
    [TestCase(new[] { Scopes.UpdateTranslationEngines, Scopes.ReadTranslationEngines }, 200, ECHO_ENGINE1_ID, true)]
    [TestCase(new[] { Scopes.UpdateTranslationEngines, Scopes.ReadTranslationEngines }, 404, ECHO_ENGINE1_ID)]
    [TestCase(new[] { Scopes.UpdateTranslationEngines, Scopes.ReadTranslationEngines }, 404, DOES_NOT_EXIST_ENGINE_ID)]
    [TestCase(new[] { Scopes.UpdateTranslationEngines, Scopes.ReadTranslationEngines }, 404, ECHO_ENGINE1_ID, true)]
    [TestCase(new[] { Scopes.ReadFiles }, 403, ECHO_ENGINE1_ID)] //Arbitrary unrelated privilege
    public async Task GetCorpusByIdForEngineByIdAsync(
        IEnumerable<string> scope,
        int expectedStatusCode,
        string engineId,
        bool addCorpus = false
    )
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient(scope);
        TranslationCorpus? result = null;
        if (addCorpus)
            result = await client.AddCorpusAsync(engineId, TestCorpusConfig);
        switch (expectedStatusCode)
        {
            case 200:
            {
                Assert.That(result, Is.Not.Null);
                TranslationCorpus resultAfterAdd = await client.GetCorpusAsync(engineId, result.Id);
                Assert.Multiple(() =>
                {
                    Assert.That(resultAfterAdd.Name, Is.EqualTo(result.Name));
                    Assert.That(resultAfterAdd.SourceLanguage, Is.EqualTo(result.SourceLanguage));
                    Assert.That(resultAfterAdd.TargetLanguage, Is.EqualTo(result.TargetLanguage));
                });
                break;
            }
            case 403:
            case 404:
                ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    TranslationCorpus result_afterAdd = await client.GetCorpusAsync(engineId, DOES_NOT_EXIST_CORPUS_ID);
                });
                Assert.That(ex?.StatusCode, Is.EqualTo(expectedStatusCode));
                break;

            default:
                Assert.Fail("Unanticipated expectedStatusCode. Check test case for typo.");
                break;
        }
    }

    [Test]
    [TestCase(new[] { Scopes.UpdateTranslationEngines, Scopes.ReadTranslationEngines }, 200, ECHO_ENGINE1_ID)]
    [TestCase(new[] { Scopes.UpdateTranslationEngines, Scopes.ReadTranslationEngines }, 404, ECHO_ENGINE1_ID)]
    [TestCase(new[] { Scopes.UpdateTranslationEngines, Scopes.ReadTranslationEngines }, 404, DOES_NOT_EXIST_ENGINE_ID)]
    [TestCase(new[] { Scopes.ReadFiles }, 403, ECHO_ENGINE1_ID)] //Arbitrary unrelated privilege
    public async Task DeleteCorpusByIdForEngineByIdAsync(
        IEnumerable<string> scope,
        int expectedStatusCode,
        string engineId
    )
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient(scope);
        switch (expectedStatusCode)
        {
            case 200:
                TranslationCorpus result = await client.AddCorpusAsync(engineId, TestCorpusConfig);
                await client.DeleteCorpusAsync(engineId, result.Id, deleteFiles: false);
                ICollection<TranslationCorpus> resultsAfterDelete = await client.GetAllCorporaAsync(engineId);
                Assert.That(resultsAfterDelete, Has.Count.EqualTo(0));
                break;
            case 403:
            case 404:
                ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.DeleteCorpusAsync(engineId, DOES_NOT_EXIST_CORPUS_ID, deleteFiles: false);
                });
                Assert.That(ex?.StatusCode, Is.EqualTo(expectedStatusCode));
                break;

            default:
                Assert.Fail("Unanticipated expectedStatusCode. Check test case for typo.");
                break;
        }
    }

    [Test]
    public async Task AddParallelCorpusToEngineByIdAsync()
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient(
            new[] { Scopes.UpdateTranslationEngines }
        );
        TranslationParallelCorpus result = await client.AddParallelCorpusAsync(
            ECHO_ENGINE1_ID,
            TestParallelCorpusConfig
        );
        Assert.Multiple(() =>
        {
            Assert.That(result.SourceCorpora.First().Id, Is.EqualTo(SOURCE_CORPUS_ID_1));
            Assert.That(result.TargetCorpora.First().Id, Is.EqualTo(TARGET_CORPUS_ID));
        });
        Engine? engine = await _env.Engines.GetAsync(ECHO_ENGINE1_ID);
        Assert.That(engine, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(engine.ParallelCorpora[0].SourceCorpora[0].Files[0].Filename, Is.EqualTo(FILE1_FILENAME));
            Assert.That(engine.ParallelCorpora[0].TargetCorpora[0].Files[0].Filename, Is.EqualTo(FILE2_FILENAME));
        });
    }

    public void AddParallelCorpusToEngineById_NoSuchEngine()
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient(
            new[] { Scopes.UpdateTranslationEngines }
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
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient(new[] { Scopes.ReadFiles });

        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            await client.AddParallelCorpusAsync(ECHO_ENGINE1_ID, TestParallelCorpusConfig);
        });
        Assert.That(ex?.StatusCode, Is.EqualTo(403));
    }

    [Test]
    public async Task UpdateParallelCorpusByIdForEngineByIdAsync()
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient();

        TranslationParallelCorpus result = await client.AddParallelCorpusAsync(
            ECHO_ENGINE1_ID,
            TestParallelCorpusConfig
        );
        var updateConfig = new TranslationParallelCorpusUpdateConfig
        {
            SourceCorpusIds = [SOURCE_CORPUS_ID_1],
            TargetCorpusIds = [TARGET_CORPUS_ID]
        };
        await client.UpdateParallelCorpusAsync(ECHO_ENGINE1_ID, result.Id, updateConfig);
        Engine? engine = await _env.Engines.GetAsync(ECHO_ENGINE1_ID);
        Assert.That(engine, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(engine.ParallelCorpora[0].SourceCorpora[0].Files[0].Filename, Is.EqualTo(FILE1_FILENAME));
            Assert.That(engine.ParallelCorpora[0].TargetCorpora[0].Files[0].Filename, Is.EqualTo(FILE2_FILENAME));
        });
    }

    [Test]
    public void UpdateParallelCorpusByIdForEngineById_NoSuchCorpus()
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient();

        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            var updateConfig = new TranslationParallelCorpusUpdateConfig
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
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient();

        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            var updateConfig = new TranslationParallelCorpusUpdateConfig
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
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient(new[] { Scopes.ReadFiles });

        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            var updateConfig = new TranslationParallelCorpusUpdateConfig
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
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient();

        TranslationParallelCorpus result = await client.AddParallelCorpusAsync(
            ECHO_ENGINE1_ID,
            TestParallelCorpusConfig
        );
        TranslationParallelCorpus resultAfterAdd = (await client.GetAllParallelCorporaAsync(ECHO_ENGINE1_ID)).First();
        Assert.Multiple(() =>
        {
            Assert.That(resultAfterAdd.Id, Is.EqualTo(result.Id));
            Assert.That(resultAfterAdd.SourceCorpora.First().Id, Is.EqualTo(result.SourceCorpora.First().Id));
        });
    }

    [Test]
    public void GetAllParallelCorporaForEngineById_NoSuchEngine()
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient();

        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            TranslationParallelCorpus result = (
                await client.GetAllParallelCorporaAsync(DOES_NOT_EXIST_ENGINE_ID)
            ).First();
        });
        Assert.That(ex?.StatusCode, Is.EqualTo(404));
    }

    [Test]
    public void GetAllParallelCorporaForEngineById_NotAuthorized()
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient(new[] { Scopes.ReadFiles });

        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            TranslationParallelCorpus result = (await client.GetAllParallelCorporaAsync(ECHO_ENGINE1_ID)).First();
        });
        Assert.That(ex?.StatusCode, Is.EqualTo(403));
    }

    [Test]
    public async Task GetParallelCorpusByIdForEngineByIdAsync()
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient();
        TranslationParallelCorpus result = await client.AddParallelCorpusAsync(
            ECHO_ENGINE1_ID,
            TestParallelCorpusConfig
        );

        Assert.That(result, Is.Not.Null);
        TranslationParallelCorpus resultAfterAdd = await client.GetParallelCorpusAsync(ECHO_ENGINE1_ID, result.Id);
        Assert.Multiple(() =>
        {
            Assert.That(resultAfterAdd.Id, Is.EqualTo(result.Id));
            Assert.That(resultAfterAdd.SourceCorpora[0].Id, Is.EqualTo(result.SourceCorpora[0].Id));
        });
    }

    [Test]
    public void GetParallelCorpusByIdForEngineById_NoCorpora()
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient();

        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            TranslationParallelCorpus result_afterAdd = await client.GetParallelCorpusAsync(
                ECHO_ENGINE1_ID,
                DOES_NOT_EXIST_CORPUS_ID
            );
        });
        Assert.That(ex?.StatusCode, Is.EqualTo(404));
    }

    [Test]
    public void GetParallelCorpusByIdForEngineById_NoSuchEngine()
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient();

        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            TranslationParallelCorpus result_afterAdd = await client.GetParallelCorpusAsync(
                DOES_NOT_EXIST_ENGINE_ID,
                SOURCE_CORPUS_ID_1
            );
        });
        Assert.That(ex?.StatusCode, Is.EqualTo(404));
    }

    [Test]
    public async Task GetParallelCorpusByIdForEngineById_NoSuchCorpus()
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient();
        TranslationParallelCorpus result = await client.AddParallelCorpusAsync(
            ECHO_ENGINE1_ID,
            TestParallelCorpusConfig
        );

        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            TranslationParallelCorpus result_afterAdd = await client.GetParallelCorpusAsync(
                ECHO_ENGINE1_ID,
                DOES_NOT_EXIST_CORPUS_ID
            );
        });
        Assert.That(ex?.StatusCode, Is.EqualTo(404));
    }

    [Test]
    public void GetParallelCorpusByIdForEngineById_NotAuthorized()
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient(new[] { Scopes.ReadFiles });

        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            TranslationParallelCorpus result_afterAdd = await client.GetParallelCorpusAsync(
                ECHO_ENGINE1_ID,
                DOES_NOT_EXIST_CORPUS_ID
            );
        });
        Assert.That(ex?.StatusCode, Is.EqualTo(403));
    }

    [Test]
    public async Task DeleteParallelCorpusByIdForEngineByIdAsync()
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient();

        TranslationParallelCorpus result = await client.AddParallelCorpusAsync(
            ECHO_ENGINE1_ID,
            TestParallelCorpusConfig
        );
        await client.DeleteParallelCorpusAsync(ECHO_ENGINE1_ID, result.Id);
        ICollection<TranslationParallelCorpus> resultsAfterDelete = await client.GetAllParallelCorporaAsync(
            ECHO_ENGINE1_ID
        );
        Assert.That(resultsAfterDelete, Has.Count.EqualTo(0));
    }

    [Test]
    public void DeleteParallelCorpusByIdForEngineById_NoSuchCorpus()
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient();

        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            await client.DeleteParallelCorpusAsync(ECHO_ENGINE1_ID, DOES_NOT_EXIST_CORPUS_ID);
        });
        Assert.That(ex?.StatusCode, Is.EqualTo(404));
    }

    [Test]
    public void DeleteParallelCorpusByIdForEngineById_NoSuchEngine()
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient();

        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            await client.DeleteParallelCorpusAsync(DOES_NOT_EXIST_ENGINE_ID, SOURCE_CORPUS_ID_1);
        });
        Assert.That(ex?.StatusCode, Is.EqualTo(404));
    }

    [Test]
    public void DeleteParallelCorpusByIdForEngineById_NotAuthorized()
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient(new[] { Scopes.ReadFiles });

        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            await client.DeleteParallelCorpusAsync(ECHO_ENGINE1_ID, SOURCE_CORPUS_ID_1);
        });
        Assert.That(ex?.StatusCode, Is.EqualTo(403));
    }

    [Test]
    public async Task DeleteCorpusAndFilesAsync()
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient();
        TranslationCorpus result = await client.AddCorpusAsync(ECHO_ENGINE1_ID, TestCorpusConfig);
        await client.DeleteCorpusAsync(ECHO_ENGINE1_ID, result.Id, deleteFiles: true);
        ICollection<TranslationCorpus> resultsAfterDelete = await client.GetAllCorporaAsync(ECHO_ENGINE1_ID);
        Assert.That(resultsAfterDelete, Has.Count.EqualTo(0));
        Assert.That(await _env.DataFiles.GetAllAsync(), Has.Count.EqualTo(2)); //Paratext files still exist
    }

    [Test]
    public async Task DeleteCorpusButNotFilesAsync()
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient();
        TranslationCorpus result = await client.AddCorpusAsync(ECHO_ENGINE1_ID, TestCorpusConfig);
        await client.DeleteCorpusAsync(ECHO_ENGINE1_ID, result.Id, deleteFiles: false);
        ICollection<TranslationCorpus> resultsAfterDelete = await client.GetAllCorporaAsync(ECHO_ENGINE1_ID);
        Assert.That(resultsAfterDelete, Has.Count.EqualTo(0));
        Assert.That(await _env.DataFiles.GetAllAsync(), Has.Count.EqualTo(4)); //Paratext & Text files still exist
    }

    [Test]
    public async Task GetAllPretranslationsAsync_Exists()
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient();
        TranslationParallelCorpus addedCorpus = await client.AddParallelCorpusAsync(
            ECHO_ENGINE1_ID,
            TestParallelCorpusConfig
        );

        await _env.Engines.UpdateAsync(ECHO_ENGINE1_ID, u => u.Set(e => e.ModelRevision, 1));
        var pret = new Translation.Models.Pretranslation
        {
            CorpusRef = addedCorpus.Id,
            TextId = "all",
            EngineRef = ECHO_ENGINE1_ID,
            SourceRefs = ["ref1", "ref2"],
            TargetRefs = ["ref1", "ref2"],
            Refs = ["ref1", "ref2"],
            Translation = "translation",
            ModelRevision = 1
        };
        await _env.Pretranslations.InsertAsync(pret);

        ICollection<Client.Pretranslation> results = await client.GetAllPretranslationsAsync(
            ECHO_ENGINE1_ID,
            addedCorpus.Id
        );
        Assert.That(results.All(p => p.TextId == "all"), Is.True);
    }

    [Test]
    public void GetAllPretranslationsAsync_EngineDoesNotExist()
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient();

        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(
            () => client.GetAllPretranslationsAsync(DOES_NOT_EXIST_ENGINE_ID, "cccccccccccccccccccccccc")
        );
        Assert.That(ex?.StatusCode, Is.EqualTo(404));
    }

    [Test]
    public void GetAllPretranslationsAsync_CorpusDoesNotExist()
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient();

        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(
            () => client.GetAllPretranslationsAsync(ECHO_ENGINE1_ID, "cccccccccccccccccccccccc")
        );
        Assert.That(ex?.StatusCode, Is.EqualTo(404));
    }

    [Test]
    public async Task GetAllPretranslationsAsync_EngineNotBuilt()
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient();
        TranslationParallelCorpus addedCorpus = await client.AddParallelCorpusAsync(
            ECHO_ENGINE2_ID,
            TestParallelCorpusConfig
        );

        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(
            () => client.GetAllPretranslationsAsync(ECHO_ENGINE2_ID, addedCorpus.Id)
        );
        Assert.That(ex?.StatusCode, Is.EqualTo(409));
    }

    [Test]
    public async Task GetAllPretranslationsAsync_TextIdExists()
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient();
        TranslationParallelCorpus addedCorpus = await client.AddParallelCorpusAsync(
            ECHO_ENGINE1_ID,
            TestParallelCorpusConfig
        );

        await _env.Engines.UpdateAsync(ECHO_ENGINE1_ID, u => u.Set(e => e.ModelRevision, 1));
        var pret = new Translation.Models.Pretranslation
        {
            CorpusRef = addedCorpus.Id,
            TextId = "all",
            EngineRef = ECHO_ENGINE1_ID,
            SourceRefs = ["ref1", "ref2"],
            TargetRefs = ["ref1", "ref2"],
            Refs = ["ref1", "ref2"],
            Translation = "translation",
            ModelRevision = 1
        };
        await _env.Pretranslations.InsertAsync(pret);

        ICollection<Client.Pretranslation> results = await client.GetAllPretranslationsAsync(
            ECHO_ENGINE1_ID,
            addedCorpus.Id,
            "all"
        );
        Assert.That(results.All(p => p.TextId == "all"), Is.True);
    }

    [Test]
    public async Task GetAllPretranslationsAsync_TextIdDoesNotExist()
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient();
        TranslationParallelCorpus addedCorpus = await client.AddParallelCorpusAsync(
            ECHO_ENGINE1_ID,
            TestParallelCorpusConfig
        );

        await _env.Engines.UpdateAsync(ECHO_ENGINE1_ID, u => u.Set(e => e.ModelRevision, 1));
        var pret = new Translation.Models.Pretranslation
        {
            CorpusRef = addedCorpus.Id,
            TextId = "all",
            EngineRef = ECHO_ENGINE1_ID,
            SourceRefs = ["ref1", "ref2"],
            TargetRefs = ["ref1", "ref2"],
            Refs = ["ref1", "ref2"],
            Translation = "translation",
            ModelRevision = 1
        };
        await _env.Pretranslations.InsertAsync(pret);

        ICollection<Client.Pretranslation> results = await client.GetAllPretranslationsAsync(
            ECHO_ENGINE1_ID,
            addedCorpus.Id,
            "not_the_right_id"
        );
        Assert.That(results, Is.Empty);
    }

    [Test]
    [TestCase(new[] { Scopes.ReadTranslationEngines }, 200, ECHO_ENGINE1_ID, true)]
    [TestCase(new[] { Scopes.ReadTranslationEngines }, 200, ECHO_ENGINE3_ID, false)] // Engine is not owned
    [TestCase(new[] { Scopes.ReadFiles }, 403, ECHO_ENGINE1_ID, false)] // Arbitrary unrelated privilege
    public async Task GetAllBuildsCreatedAfterAsync(
        IEnumerable<string> scope,
        int expectedStatusCode,
        string createBuildForEngineId,
        bool buildOwnedByClient
    )
    {
        TranslationBuildsClient client = _env.CreateTranslationBuildsClient(scope);
        Build? build = new Build
        {
            EngineRef = createBuildForEngineId,
            Owner = buildOwnedByClient ? "client1" : "client2",
            DateCreated = DateTime.UtcNow
        };
        await _env.Builds.InsertAsync(build);
        switch (expectedStatusCode)
        {
            case 200:
                ICollection<TranslationBuild> results = await client.GetAllBuildsCreatedAfterAsync(
                    DateTime.UtcNow.AddHours(-1)
                );
                if (buildOwnedByClient)
                {
                    Assert.That(results, Is.Not.Empty);
                    Assert.Multiple(() =>
                    {
                        Assert.That(results.First().Revision, Is.EqualTo(1));
                        Assert.That(results.First().Id, Is.EqualTo(build?.Id));
                        Assert.That(results.First().State, Is.EqualTo(JobState.Pending));
                    });
                }
                else
                {
                    Assert.That(results, Is.Empty);
                }
                break;
            case 403:
                ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.GetAllBuildsCreatedAfterAsync(DateTime.UtcNow);
                });
                break;
            default:
                Assert.Fail("Unanticipated expectedStatusCode. Check test case for typo.");
                break;
        }
    }

    [Test]
    [TestCase(new[] { Scopes.ReadTranslationEngines }, 200, SMT_ENGINE1_ID)]
    [TestCase(new[] { Scopes.ReadTranslationEngines }, 404, DOES_NOT_EXIST_ENGINE_ID, false)]
    [TestCase(new[] { Scopes.ReadFiles }, 403, ECHO_ENGINE1_ID)] //Arbitrary unrelated privilege
    public async Task GetAllBuildsForEngineByIdAsync(
        IEnumerable<string> scope,
        int expectedStatusCode,
        string engineId,
        bool addBuild = true
    )
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient(scope);
        Build? build = null;
        if (addBuild)
        {
            build = new Build { EngineRef = engineId, Owner = "client1" };
            await _env.Builds.InsertAsync(build);
        }
        switch (expectedStatusCode)
        {
            case 200:
                ICollection<TranslationBuild> results = await client.GetAllBuildsAsync(engineId);
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
    [TestCase(new[] { Scopes.ReadTranslationEngines }, 200, SMT_ENGINE1_ID)]
    [TestCase(new[] { Scopes.ReadTranslationEngines }, 408, SMT_ENGINE1_ID, true)]
    [TestCase(new[] { Scopes.ReadTranslationEngines }, 404, DOES_NOT_EXIST_ENGINE_ID, false)]
    [TestCase(new[] { Scopes.ReadTranslationEngines }, 404, SMT_ENGINE1_ID, false)]
    [TestCase(new[] { Scopes.ReadFiles }, 403, ECHO_ENGINE1_ID)] //Arbitrary unrelated privilege
    public async Task GetBuildByIdForEngineByIdAsync(
        IEnumerable<string> scope,
        int expectedStatusCode,
        string engineId,
        bool addBuild = true
    )
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient(scope);
        Build? build = null;
        if (addBuild)
        {
            build = new Build { EngineRef = engineId, Owner = "client1" };
            await _env.Builds.InsertAsync(build);
        }

        switch (expectedStatusCode)
        {
            case 200:
            {
                Assert.That(build, Is.Not.Null);
                TranslationBuild result = await client.GetBuildAsync(engineId, build.Id);
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
        new[] { Scopes.UpdateTranslationEngines, Scopes.CreateTranslationEngines, Scopes.ReadTranslationEngines },
        201,
        ECHO_ENGINE1_ID
    )]
    [TestCase(
        new[] { Scopes.UpdateTranslationEngines, Scopes.CreateTranslationEngines, Scopes.ReadTranslationEngines },
        404,
        DOES_NOT_EXIST_ENGINE_ID
    )]
    [TestCase(
        new[] { Scopes.UpdateTranslationEngines, Scopes.CreateTranslationEngines, Scopes.ReadTranslationEngines },
        400,
        ECHO_ENGINE1_ID
    )]
    [TestCase(new[] { Scopes.ReadFiles }, 403, ECHO_ENGINE1_ID)] //Arbitrary unrelated privilege
    public async Task StartBuildForEngineByIdAsync(IEnumerable<string> scope, int expectedStatusCode, string engineId)
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient(scope);
        PretranslateCorpusConfig ptcc;
        TrainingCorpusConfig tcc;
        TranslationBuildConfig tbc;
        switch (expectedStatusCode)
        {
            case 201:
                TranslationCorpus addedCorpus = await client.AddCorpusAsync(engineId, TestCorpusConfig);
                ptcc = new PretranslateCorpusConfig { CorpusId = addedCorpus.Id, TextIds = ["all"] };
                tcc = new() { CorpusId = addedCorpus.Id, TextIds = ["all"] };
                tbc = new TranslationBuildConfig
                {
                    Pretranslate = [ptcc],
                    TrainOn = [tcc],
                    Options = """
                        {"max_steps":10,
                        "use_key_terms":false,
                        "some_double":10.5,
                        "some_nested": {"more_nested": {"other_double":10.5}},
                        "some_string":"string"}
                        """
                };
                TranslationBuild resultAfterStart;
                Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    resultAfterStart = await client.GetCurrentBuildAsync(engineId);
                });

                TranslationBuild build = await client.StartBuildAsync(engineId, tbc);
                Assert.That(build, Is.Not.Null);

                build = await client.GetCurrentBuildAsync(engineId);
                Assert.That(build, Is.Not.Null);

                Assert.That(build.DeploymentVersion, Is.Not.Null);

                break;
            case 400:
            case 403:
            case 404:
                ptcc = new PretranslateCorpusConfig { CorpusId = "cccccccccccccccccccccccc", TextIds = ["all"] };
                tbc = new TranslationBuildConfig { Pretranslate = [ptcc] };
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
    public void AddParallelCorpusAsync_EmptyCorpus()
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient();
        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            TranslationParallelCorpus addedCorpus = await client.AddParallelCorpusAsync(
                ECHO_ENGINE1_ID,
                TestParallelCorpusConfigEmptySource
            );
        });
        Assert.That(ex, Is.Not.Null);
        Assert.That(ex.StatusCode, Is.EqualTo(400));
    }

    [Test]
    public async Task StartBuildForEngineAsync_NoCorporaTranslating()
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient();
        TranslationParallelCorpus addedCorpus = await client.AddParallelCorpusAsync(
            ECHO_ENGINE1_ID,
            TestParallelCorpusConfigNoCorpora
        );
        PretranslateCorpusConfig pcc = new() { ParallelCorpusId = addedCorpus.Id };
        TranslationBuildConfig tbc = new() { Pretranslate = [pcc] };
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
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient();
        TranslationParallelCorpus addedCorpus = await client.AddParallelCorpusAsync(
            ECHO_ENGINE1_ID,
            TestParallelCorpusConfigNoCorpora
        );
        TrainingCorpusConfig tcc = new() { ParallelCorpusId = addedCorpus.Id };
        TranslationBuildConfig tbc = new() { TrainOn = [tcc], };
        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            await client.StartBuildAsync(ECHO_ENGINE1_ID, tbc);
        });
        Assert.That(ex, Is.Not.Null);
        Assert.That(ex.StatusCode, Is.EqualTo(400));
    }

    [Test]
    public async Task StartBuildForEngineAsync_EmptyParallelCorpus()
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient();
        TranslationParallelCorpus addedCorpus = await client.AddParallelCorpusAsync(
            ECHO_ENGINE1_ID,
            TestParallelCorpusConfig
        );
        TrainingCorpusConfig tcc = new() { ParallelCorpusId = addedCorpus.Id };
        TranslationBuildConfig tbc = new() { TrainOn = [tcc], };
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
    public async Task StartBuildForEngineAsync_EmptyCorpus()
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient();
        TranslationCorpus addedCorpus = await client.AddCorpusAsync(ECHO_ENGINE1_ID, TestCorpusConfig);
        TrainingCorpusConfig tcc = new() { CorpusId = addedCorpus.Id };
        TranslationBuildConfig tbc = new() { TrainOn = [tcc], };
        //Below code is copy-pasted from EngineService.DeleteAllCorpusFilesAsync
        async Task DeleteFilesFromCorpora(string dataFileId)
        {
            await _env.Engines.UpdateAllAsync(
                e =>
                    e.Corpora.Any(c =>
                        c.SourceFiles.Any(f => f.Id == dataFileId) || c.TargetFiles.Any(f => f.Id == dataFileId)
                    ),
                u =>
                {
                    u.RemoveAll(e => e.Corpora.AllElements().SourceFiles, f => f.Id == dataFileId);
                    u.RemoveAll(e => e.Corpora.AllElements().TargetFiles, f => f.Id == dataFileId);
                }
            );
        }
        await DeleteFilesFromCorpora(FILE1_SRC_ID);
        await DeleteFilesFromCorpora(FILE2_TRG_ID);
        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            await client.StartBuildAsync(ECHO_ENGINE1_ID, tbc);
        });
        Assert.That(ex, Is.Not.Null);
        Assert.That(ex.StatusCode, Is.EqualTo(400));
    }

    [Test]
    public async Task GetPretranslatedUsfmAsync_NoCorpora()
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient();
        TranslationParallelCorpus addedCorpus = await client.AddParallelCorpusAsync(
            ECHO_ENGINE1_ID,
            TestParallelCorpusConfigNoCorpora
        );

        await _env.Engines.UpdateAsync(ECHO_ENGINE1_ID, u => u.Set(e => e.ModelRevision, 1));

        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            await client.GetPretranslatedUsfmAsync(ECHO_ENGINE1_ID, addedCorpus.Id, "MAT");
        });
        Assert.That(ex, Is.Not.Null);
        Assert.That(ex.StatusCode, Is.EqualTo(400));
    }

    [Test]
    public async Task GetPretranslatedUsfmAsync_EmptyParallelCorpus()
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient();
        TranslationParallelCorpus addedCorpus = await client.AddParallelCorpusAsync(
            ECHO_ENGINE1_ID,
            TestParallelCorpusConfig
        );

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

        await _env.Engines.UpdateAsync(ECHO_ENGINE1_ID, u => u.Set(e => e.ModelRevision, 1));

        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            await client.GetPretranslatedUsfmAsync(ECHO_ENGINE1_ID, addedCorpus.Id, "MAT");
        });
        Assert.That(ex, Is.Not.Null);
        Assert.That(ex.StatusCode, Is.EqualTo(400));
    }

    [Test]
    public async Task GetCorpusPretranslatedUsfmAsync_EmptyCorpus()
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient();
        TranslationCorpus addedCorpus = await client.AddCorpusAsync(ECHO_ENGINE1_ID, TestCorpusConfig);
        TrainingCorpusConfig tcc = new() { CorpusId = addedCorpus.Id };
        //Below code is copy-pasted from EngineService.DeleteAllCorpusFilesAsync
        async Task DeleteFilesFromCorpora(string dataFileId)
        {
            await _env.Engines.UpdateAllAsync(
                e =>
                    e.Corpora.Any(c =>
                        c.SourceFiles.Any(f => f.Id == dataFileId) || c.TargetFiles.Any(f => f.Id == dataFileId)
                    ),
                u =>
                {
                    u.RemoveAll(e => e.Corpora.AllElements().SourceFiles, f => f.Id == dataFileId);
                    u.RemoveAll(e => e.Corpora.AllElements().TargetFiles, f => f.Id == dataFileId);
                }
            );
        }
        await DeleteFilesFromCorpora(FILE1_SRC_ID);
        await DeleteFilesFromCorpora(FILE2_TRG_ID);

        await _env.Engines.UpdateAsync(ECHO_ENGINE1_ID, u => u.Set(e => e.ModelRevision, 1));

        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            await client.GetCorpusPretranslatedUsfmAsync(ECHO_ENGINE1_ID, addedCorpus.Id, "MAT");
        });
        Assert.That(ex, Is.Not.Null);
        Assert.That(ex.StatusCode, Is.EqualTo(400));
    }

    [TestCase]
    public async Task StartBuildForEngineAsync_UnparsableOptions()
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient();
        TranslationCorpus addedCorpus = await client.AddCorpusAsync(ECHO_ENGINE1_ID, TestCorpusConfig);
        PretranslateCorpusConfig ptcc = new() { CorpusId = addedCorpus.Id, TextIds = ["all"] };
        TrainingCorpusConfig tcc = new() { CorpusId = addedCorpus.Id, TextIds = ["all"] };
        TranslationBuildConfig tbc =
            new()
            {
                Pretranslate = [ptcc],
                TrainOn = [tcc],
                Options = "unparsable json"
            };
        TranslationBuild resultAfterStart;
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
    [TestCase(new[] { Scopes.ReadTranslationEngines }, 200, ECHO_ENGINE1_ID)]
    [TestCase(new[] { Scopes.ReadTranslationEngines }, 404, DOES_NOT_EXIST_ENGINE_ID)]
    [TestCase(new[] { Scopes.ReadFiles }, 403, ECHO_ENGINE1_ID)] //Arbitrary unrelated privilege
    public async Task GetDownloadableUrl(IEnumerable<string> scope, int expectedStatusCode, string engineId)
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient(scope);

        switch (expectedStatusCode)
        {
            case 200:
            {
                Client.ModelDownloadUrl result = await client.GetModelDownloadUrlAsync(engineId);
                Assert.Multiple(() =>
                {
                    Assert.That(result.ExpiresAt, Is.GreaterThan((DateTimeOffset)DateTime.UtcNow));
                    Assert.That(result.ModelRevision, Is.EqualTo(1));
                    Assert.That(result.Url, Is.Not.Null);
                });
                break;
            }
            case 403:
            case 404:
            {
                ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    Client.ModelDownloadUrl result = await client.GetModelDownloadUrlAsync(engineId);
                });
                Assert.That(ex?.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
            }
        }
    }

    [Test]
    [TestCase(new[] { Scopes.ReadTranslationEngines }, 200, ECHO_ENGINE1_ID)]
    [TestCase(new[] { Scopes.ReadTranslationEngines }, 408, ECHO_ENGINE1_ID)]
    [TestCase(new[] { Scopes.ReadTranslationEngines }, 204, ECHO_ENGINE1_ID, false)]
    [TestCase(new[] { Scopes.ReadTranslationEngines }, 404, DOES_NOT_EXIST_ENGINE_ID, false)]
    [TestCase(new[] { Scopes.ReadFiles }, 403, ECHO_ENGINE1_ID, false)] //Arbitrary unrelated privilege
    public async Task GetCurrentBuildForEngineByIdAsync(
        IEnumerable<string> scope,
        int expectedStatusCode,
        string engineId,
        bool addBuild = true
    )
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient(scope);
        Build? build = null;
        if (addBuild)
        {
            build = new Build
            {
                EngineRef = engineId,
                Owner = "client1",
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
                TranslationBuild result = await client.GetCurrentBuildAsync(engineId);
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
    [TestCase(new[] { Scopes.UpdateTranslationEngines }, 200, ECHO_ENGINE1_ID)]
    [TestCase(new[] { Scopes.UpdateTranslationEngines }, 404, DOES_NOT_EXIST_ENGINE_ID, false)]
    [TestCase(new[] { Scopes.UpdateTranslationEngines }, 204, ECHO_ENGINE1_ID, false)]
    [TestCase(new[] { Scopes.ReadFiles }, 403, ECHO_ENGINE1_ID, false)] //Arbitrary unrelated privilege
    public async Task CancelCurrentBuildForEngineByIdAsync(
        IEnumerable<string> scope,
        int expectedStatusCode,
        string engineId,
        bool addBuild = true
    )
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient(scope);

        string buildId = "b00000000000000000000000";
        if (addBuild)
        {
            _env.EchoClient.CancelBuildAsync(
                Arg.Is(new CancelBuildRequest() { EngineId = engineId, EngineType = "Echo" }),
                null,
                null,
                Arg.Any<CancellationToken>()
            )
                .Returns(CreateAsyncUnaryCall(new CancelBuildResponse() { BuildId = buildId }));
            var build = new Build
            {
                Id = buildId,
                EngineRef = engineId,
                Owner = "client1"
            };
            await _env.Builds.InsertAsync(build);
        }

        switch (expectedStatusCode)
        {
            case 200:
                TranslationBuild build = await client.CancelBuildAsync(engineId);
                Assert.That(build.Id, Is.EqualTo("b00000000000000000000000"));
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
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient();
        TranslationParallelCorpus addedCorpus = await client.AddParallelCorpusAsync(
            NMT_ENGINE1_ID,
            TestParallelCorpusConfig
        );
        PretranslateCorpusConfig ptcc =
            new()
            {
                ParallelCorpusId = addedCorpus.Id,
                SourceFilters = [new() { CorpusId = SOURCE_CORPUS_ID_1, TextIds = ["all"] }]
            };
        TrainingCorpusConfig tcc =
            new()
            {
                ParallelCorpusId = addedCorpus.Id,
                SourceFilters = [new() { CorpusId = SOURCE_CORPUS_ID_1, TextIds = ["all"] }],
                TargetFilters = [new() { CorpusId = TARGET_CORPUS_ID, TextIds = ["all"] }]
            };
        ;
        TranslationBuildConfig tbc = new TranslationBuildConfig
        {
            Pretranslate = [ptcc],
            TrainOn = [tcc],
            Options = """
                {"max_steps":10,
                "use_key_terms":false,
                "some_double":10.5,
                "some_nested": {"more_nested": {"other_double":10.5}},
                "some_string":"string"}
                """
        };
        TranslationBuild resultAfterStart;
        Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            resultAfterStart = await client.GetCurrentBuildAsync(NMT_ENGINE1_ID);
        });

        TranslationBuild build = await client.StartBuildAsync(NMT_ENGINE1_ID, tbc);
        Assert.That(build, Is.Not.Null);

        build = await client.GetCurrentBuildAsync(NMT_ENGINE1_ID);
        Assert.That(build, Is.Not.Null);
    }

    [Test]
    public async Task StartBuildAsync_Corpus_NoFilter()
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient();
        TranslationCorpus addedCorpus = await client.AddCorpusAsync(NMT_ENGINE1_ID, TestCorpusConfig);
        PretranslateCorpusConfig ptcc =
            new() { CorpusId = addedCorpus.Id, SourceFilters = [new() { CorpusId = SOURCE_CORPUS_ID_1 }] };
        TrainingCorpusConfig tcc =
            new()
            {
                CorpusId = addedCorpus.Id,
                SourceFilters = [new() { CorpusId = SOURCE_CORPUS_ID_1 }],
                TargetFilters = [new() { CorpusId = TARGET_CORPUS_ID }]
            };
        ;
        TranslationBuildConfig tbc = new TranslationBuildConfig
        {
            Pretranslate = [ptcc],
            TrainOn = [tcc],
            Options = """
                {"max_steps":10,
                "use_key_terms":false,
                "some_double":10.5,
                "some_nested": {"more_nested": {"other_double":10.5}},
                "some_string":"string"}
                """
        };
        TranslationBuild resultAfterStart;
        Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            resultAfterStart = await client.GetCurrentBuildAsync(NMT_ENGINE1_ID);
        });

        TranslationBuild build = await client.StartBuildAsync(NMT_ENGINE1_ID, tbc);
        Assert.That(build, Is.Not.Null);
        Assert.That(build.TrainOn, Is.Not.Null);
        Assert.That(build.TrainOn.Count, Is.EqualTo(1));
        Assert.That(build.TrainOn[0].TextIds, Is.Null);
        Assert.That(build.TrainOn[0].ScriptureRange, Is.Null);
        Assert.That(build.Pretranslate, Is.Not.Null);
        Assert.That(build.Pretranslate.Count, Is.EqualTo(1));
        Assert.That(build.Pretranslate[0].TextIds, Is.Null);
        Assert.That(build.Pretranslate[0].ScriptureRange, Is.Null);

        build = await client.GetCurrentBuildAsync(NMT_ENGINE1_ID);
        Assert.That(build, Is.Not.Null);
    }

    [Test]
    public async Task StartBuildAsync_ParallelCorpus_NoFilter()
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient();
        TranslationParallelCorpus addedCorpus = await client.AddParallelCorpusAsync(
            NMT_ENGINE1_ID,
            TestParallelCorpusConfig
        );
        PretranslateCorpusConfig ptcc =
            new() { ParallelCorpusId = addedCorpus.Id, SourceFilters = [new() { CorpusId = SOURCE_CORPUS_ID_1 }] };
        TrainingCorpusConfig tcc =
            new()
            {
                ParallelCorpusId = addedCorpus.Id,
                SourceFilters = [new() { CorpusId = SOURCE_CORPUS_ID_1 }],
                TargetFilters = [new() { CorpusId = TARGET_CORPUS_ID }]
            };
        ;
        TranslationBuildConfig tbc = new TranslationBuildConfig
        {
            Pretranslate = [ptcc],
            TrainOn = [tcc],
            Options = """
                {"max_steps":10,
                "use_key_terms":false,
                "some_double":10.5,
                "some_nested": {"more_nested": {"other_double":10.5}},
                "some_string":"string"}
                """
        };
        TranslationBuild resultAfterStart;
        Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            resultAfterStart = await client.GetCurrentBuildAsync(NMT_ENGINE1_ID);
        });

        TranslationBuild build = await client.StartBuildAsync(NMT_ENGINE1_ID, tbc);
        Assert.That(build, Is.Not.Null);
        Assert.That(build.TrainOn, Is.Not.Null);
        Assert.That(build.TrainOn.Count, Is.EqualTo(1));
        Assert.That(build.TrainOn[0].TextIds, Is.Null);
        Assert.That(build.TrainOn[0].ScriptureRange, Is.Null);
        Assert.That(build.Pretranslate, Is.Not.Null);
        Assert.That(build.Pretranslate.Count, Is.EqualTo(1));
        Assert.That(build.Pretranslate[0].TextIds, Is.Null);
        Assert.That(build.Pretranslate[0].ScriptureRange, Is.Null);

        build = await client.GetCurrentBuildAsync(NMT_ENGINE1_ID);
        Assert.That(build, Is.Not.Null);
    }

    [Test]
    public async Task StartBuildAsync_ParallelCorpus_PretranslateParallelAndNormalCorpus()
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient();
        TranslationCorpus addedCorpus = await client.AddCorpusAsync(NMT_ENGINE1_ID, TestCorpusConfig);
        TranslationParallelCorpus addedParallelCorpus = await client.AddParallelCorpusAsync(
            NMT_ENGINE1_ID,
            TestParallelCorpusConfig
        );
        PretranslateCorpusConfig ptcc = new() { CorpusId = addedCorpus.Id, ParallelCorpusId = addedParallelCorpus.Id };
        TrainingCorpusConfig tcc = new() { ParallelCorpusId = addedParallelCorpus.Id };
        TranslationBuildConfig tbc = new TranslationBuildConfig { Pretranslate = [ptcc], TrainOn = [tcc] };
        TranslationBuild resultAfterStart;
        Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            resultAfterStart = await client.StartBuildAsync(NMT_ENGINE1_ID, tbc);
        });
    }

    [Test]
    public async Task StartBuildAsync_ParallelCorpus_TrainOnParallelAndNormalCorpus()
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient();
        TranslationCorpus addedCorpus = await client.AddCorpusAsync(NMT_ENGINE1_ID, TestCorpusConfig);
        TranslationParallelCorpus addedParallelCorpus = await client.AddParallelCorpusAsync(
            NMT_ENGINE1_ID,
            TestParallelCorpusConfig
        );
        PretranslateCorpusConfig ptcc = new() { ParallelCorpusId = addedParallelCorpus.Id };
        TrainingCorpusConfig tcc = new() { CorpusId = addedCorpus.Id, ParallelCorpusId = addedParallelCorpus.Id };
        TranslationBuildConfig tbc = new TranslationBuildConfig { Pretranslate = [ptcc], TrainOn = [tcc] };
        TranslationBuild resultAfterStart;
        Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            resultAfterStart = await client.StartBuildAsync(NMT_ENGINE1_ID, tbc);
        });
    }

    [Test]
    public async Task StartBuildAsync_ParallelCorpus_PretranslateNoCorpusSpecified()
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient();
        TranslationParallelCorpus addedParallelCorpus = await client.AddParallelCorpusAsync(
            NMT_ENGINE1_ID,
            TestMixedParallelCorpusConfig
        );
        PretranslateCorpusConfig ptcc = new() { };
        TrainingCorpusConfig tcc = new() { ParallelCorpusId = addedParallelCorpus.Id };
        TranslationBuildConfig tbc = new TranslationBuildConfig { Pretranslate = [ptcc], TrainOn = [tcc] };
        TranslationBuild resultAfterStart;
        Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            resultAfterStart = await client.StartBuildAsync(NMT_ENGINE1_ID, tbc);
        });
    }

    [Test]
    public async Task StartBuildAsync_ParallelCorpus_PretranslateFilterOnMultipleSources()
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient();
        TranslationParallelCorpus addedParallelCorpus = await client.AddParallelCorpusAsync(
            NMT_ENGINE1_ID,
            TestParallelCorpusConfig
        );
        PretranslateCorpusConfig ptcc =
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
        TranslationBuildConfig tbc = new TranslationBuildConfig { Pretranslate = [ptcc], TrainOn = [tcc] };
        Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            await client.StartBuildAsync(NMT_ENGINE1_ID, tbc);
        });
    }

    [Test]
    public async Task StartBuildAsync_ParallelCorpus_TrainOnNoCorpusSpecified()
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient();
        TranslationParallelCorpus addedParallelCorpus = await client.AddParallelCorpusAsync(
            NMT_ENGINE1_ID,
            TestParallelCorpusConfig
        );
        PretranslateCorpusConfig ptcc = new() { ParallelCorpusId = addedParallelCorpus.Id };
        TrainingCorpusConfig tcc = new() { };
        TranslationBuildConfig tbc = new TranslationBuildConfig { Pretranslate = [ptcc], TrainOn = [tcc] };
        TranslationBuild resultAfterStart;
        Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            resultAfterStart = await client.StartBuildAsync(NMT_ENGINE1_ID, tbc);
        });
    }

    [Test]
    public async Task TryToQueueMultipleBuildsPerSingleUser()
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient();
        string engineId = NMT_ENGINE1_ID;
        int expectedStatusCode = 409;
        TranslationCorpus addedCorpus = await client.AddCorpusAsync(engineId, TestCorpusConfigNonEcho);
        var ptcc = new PretranslateCorpusConfig { CorpusId = addedCorpus.Id, TextIds = ["all"] };
        var tbc = new TranslationBuildConfig { Pretranslate = [ptcc] };
        TranslationBuild build = await client.StartBuildAsync(engineId, tbc);
        _env.NmtClient.StartBuildAsync(Arg.Any<StartBuildRequest>(), null, null, Arg.Any<CancellationToken>())
            .Returns(CreateAsyncUnaryCall<Empty>(StatusCode.FailedPrecondition));
        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            build = await client.StartBuildAsync(engineId, tbc);
        });
        Assert.That(ex, Is.Not.Null);
        Assert.That(ex.StatusCode, Is.EqualTo(expectedStatusCode));
    }

    [Test]
    public async Task GetPretranslatedUsfmAsync_BookExists()
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient();
        TranslationParallelCorpus addedCorpus = await client.AddParallelCorpusAsync(
            ECHO_ENGINE1_ID,
            TestParallelCorpusConfigScripture
        );

        await _env.Engines.UpdateAsync(ECHO_ENGINE1_ID, u => u.Set(e => e.ModelRevision, 1));
        await _env.Builds.InsertAsync(
            new Build
            {
                Id = "b10000000000000000000000",
                EngineRef = ECHO_ENGINE1_ID,
                Owner = "client1",
                Revision = 1,
                DateFinished = DateTime.UnixEpoch,
            }
        );
        var pret = new Translation.Models.Pretranslation
        {
            CorpusRef = addedCorpus.Id,
            TextId = "MAT",
            EngineRef = ECHO_ENGINE1_ID,
            SourceRefs = ["MAT 1:1"],
            TargetRefs = ["MAT 1:1"],
            Refs = ["MAT 1:1"],
            Translation = "translation",
            ModelRevision = 1
        };
        await _env.Pretranslations.InsertAsync(pret);

        string usfm = await client.GetPretranslatedUsfmAsync(ECHO_ENGINE1_ID, addedCorpus.Id, "MAT");
        Assert.That(
            usfm.Replace("\r\n", "\n"),
            Is.EqualTo(
                @"\id MAT - TRG
\rem This draft of MAT was generated using AI on 1970-01-01 00:00:00Z. It should be reviewed and edited carefully.
\rem Paragraph breaks and embed markers were moved to the end of the verse. Style markers were removed.
\h
\c 1
\p
\v 1 translation
\v 2
".Replace("\r\n", "\n")
            )
        );
    }

    [Test]
    public async Task GetPretranslationsByTextId()
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient();
        TranslationParallelCorpus addedCorpus = await client.AddParallelCorpusAsync(
            ECHO_ENGINE1_ID,
            TestParallelCorpusConfigScripture
        );

        await _env.Engines.UpdateAsync(ECHO_ENGINE1_ID, u => u.Set(e => e.ModelRevision, 1));
        var pret = new Translation.Models.Pretranslation
        {
            CorpusRef = addedCorpus.Id,
            TextId = "MAT",
            EngineRef = ECHO_ENGINE1_ID,
            SourceRefs = ["MAT 1:1"],
            TargetRefs = ["MAT 1:1"],
            Refs = ["MAT 1:1"],
            Translation = "translation",
            ModelRevision = 1
        };
        await _env.Pretranslations.InsertAsync(pret);

        IList<Client.Pretranslation> pretranslations = await client.GetPretranslationsByTextIdAsync(
            ECHO_ENGINE1_ID,
            addedCorpus.Id,
            "MAT"
        );
        Assert.That(pretranslations, Has.Count.EqualTo(1));
        Assert.That(pretranslations[0].Translation, Is.EqualTo("translation"));
    }

    [Test]
    public void GetPretranslationsByTextId_EngineDoesNotExist()
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient();
        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            await client.GetPretranslationsByTextIdAsync(DOES_NOT_EXIST_ENGINE_ID, DOES_NOT_EXIST_CORPUS_ID, "MAT");
        });
        Assert.That(ex?.StatusCode, Is.EqualTo(404));
    }

    [Test]
    public async Task GetPretranslatedUsfmAsync_BookDoesNotExist()
    {
        TranslationEnginesClient client = _env.CreateTranslationEnginesClient();
        TranslationParallelCorpus addedCorpus = await client.AddParallelCorpusAsync(
            ECHO_ENGINE1_ID,
            TestParallelCorpusConfigScripture
        );

        await _env.Engines.UpdateAsync(ECHO_ENGINE1_ID, u => u.Set(e => e.ModelRevision, 1));
        await _env.Builds.InsertAsync(
            new Build
            {
                Id = "b10000000000000000000000",
                EngineRef = ECHO_ENGINE1_ID,
                Owner = "client1",
                Revision = 1,
                DateFinished = DateTime.UnixEpoch,
            }
        );

        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(
            () => client.GetPretranslatedUsfmAsync(ECHO_ENGINE1_ID, addedCorpus.Id, "MRK")
        );
        Assert.That(ex?.StatusCode, Is.EqualTo(204));
    }

    [Test]
    [TestCase("Nmt")]
    [TestCase("Echo")]
    public async Task GetQueueAsync(string engineType)
    {
        TranslationEngineTypesClient client = _env.CreateTranslationEngineTypesClient();
        Queue queue = await client.GetQueueAsync(engineType);
        Assert.That(queue.Size, Is.EqualTo(0));
    }

    [Test]
    public void GetQueueAsync_NotAuthorized()
    {
        TranslationEngineTypesClient client = _env.CreateTranslationEngineTypesClient([Scopes.ReadFiles]);
        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            Queue queue = await client.GetQueueAsync("Echo");
        });
        Assert.That(ex, Is.Not.Null);
        Assert.That(ex.StatusCode, Is.EqualTo(403));
    }

    [Test]
    [TestCase("Nmt")]
    [TestCase("SmtTransfer")]
    [TestCase("Echo")]
    public async Task GetLanguageInfoAsync(string engineType)
    {
        TranslationEngineTypesClient client = _env.CreateTranslationEngineTypesClient();
        Client.LanguageInfo languageInfo = await client.GetLanguageInfoAsync(engineType, "Alphabet");
        Assert.Multiple(() =>
        {
            Assert.That(languageInfo.InternalCode, Is.EqualTo("abc_123"));
            Assert.That(languageInfo.IsNative, Is.EqualTo(true));
        });
    }

    [Test]
    public void GetLanguageInfo_Error()
    {
        TranslationEngineTypesClient client = _env.CreateTranslationEngineTypesClient([Scopes.ReadFiles]);
        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            Client.LanguageInfo languageInfo = await client.GetLanguageInfoAsync("Nmt", "abc");
        });
        Assert.That(ex, Is.Not.Null);
        Assert.That(ex.StatusCode, Is.EqualTo(403));
    }

    [Test]
    public async Task DataFileUpdate_Propagated()
    {
        TranslationEnginesClient translationClient = _env.CreateTranslationEnginesClient();
        DataFilesClient dataFilesClient = _env.CreateDataFilesClient();
        CorporaClient corporaClient = _env.CreateCorporaClient();
        await translationClient.AddCorpusAsync(ECHO_ENGINE1_ID, TestCorpusConfig);
        await translationClient.AddParallelCorpusAsync(ECHO_ENGINE2_ID, TestParallelCorpusConfig);

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

        Engine newEngine1 = (await _env.Engines.GetAsync(ECHO_ENGINE1_ID))!;
        Engine newEngine2 = (await _env.Engines.GetAsync(ECHO_ENGINE2_ID))!;

        // Updated (legacy) Corpus file filename
        Assert.That(newEngine1.Corpora[0].SourceFiles[0].Filename, Is.EqualTo(newFileFromRepo.Filename));
        Assert.That(newEngine1.Corpora[0].TargetFiles[0].Filename, Is.EqualTo(FILE2_FILENAME));

        // Updated parallel corpus file filename
        Assert.That(
            newEngine2.ParallelCorpora[0].SourceCorpora[0].Files[0].Filename,
            Is.EqualTo(newFileFromRepo.Filename)
        );

        // Updated set of new corpus files
        Assert.That(newEngine2.ParallelCorpora[0].TargetCorpora[0].Id, Is.EqualTo(TARGET_CORPUS_ID));
        Assert.That(newEngine2.ParallelCorpora[0].TargetCorpora[0].Files[0].Id, Is.EqualTo(FILE4_TRG_ZIP_ID));
        Assert.That(newEngine2.ParallelCorpora[0].TargetCorpora[0].Files[0].Filename, Is.EqualTo(FILE4_FILENAME));
        Assert.That(newEngine2.ParallelCorpora[0].TargetCorpora[0].Files.Count, Is.EqualTo(1));
    }

    [TearDown]
    public void TearDown()
    {
        _env.Dispose();
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
            Pretranslations = _scope.ServiceProvider.GetRequiredService<
                IRepository<Translation.Models.Pretranslation>
            >();
            Builds = _scope.ServiceProvider.GetRequiredService<IRepository<Build>>();

            EchoClient = Substitute.For<TranslationEngineApi.TranslationEngineApiClient>();
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
            EchoClient
                .GetModelDownloadUrlAsync(
                    Arg.Any<GetModelDownloadUrlRequest>(),
                    null,
                    null,
                    Arg.Any<CancellationToken>()
                )
                .Returns(
                    CreateAsyncUnaryCall(
                        new GetModelDownloadUrlResponse
                        {
                            Url = "http://example.com",
                            ModelRevision = 1,
                            ExpiresAt = DateTime.UtcNow.AddHours(1).ToTimestamp()
                        }
                    )
                );
            var wg = new Translation.V1.WordGraph
            {
                SourceTokens = { "This is a test .".Split() },
                FinalStates = { 4 },
                Arcs =
                {
                    new Translation.V1.WordGraphArc
                    {
                        PrevState = 0,
                        NextState = 1,
                        Score = 1.0
                    },
                    new Translation.V1.WordGraphArc
                    {
                        PrevState = 1,
                        NextState = 2,
                        Score = 1.0
                    },
                    new Translation.V1.WordGraphArc
                    {
                        PrevState = 2,
                        NextState = 3,
                        Score = 1.0
                    },
                    new Translation.V1.WordGraphArc
                    {
                        PrevState = 3,
                        NextState = 4,
                        Score = 1.0
                    }
                }
            };
            var wgr = new GetWordGraphResponse { WordGraph = wg };
            EchoClient
                .TrainSegmentPairAsync(Arg.Any<TrainSegmentPairRequest>(), null, null, Arg.Any<CancellationToken>())
                .Returns(CreateAsyncUnaryCall(new Empty()));
            EchoClient
                .GetWordGraphAsync(Arg.Any<GetWordGraphRequest>(), null, null, Arg.Any<CancellationToken>())
                .Returns(CreateAsyncUnaryCall(wgr));

            var translationResult = new Translation.V1.TranslationResult
            {
                Translation = "This is a test .",
                SourceTokens = { "This is a test .".Split() },
                TargetTokens = { "This is a test .".Split() },
                Confidences = { 1.0, 1.0, 1.0, 1.0, 1.0 },
                Sources =
                {
                    new TranslationSources
                    {
                        Values =
                        {
                            Translation.V1.TranslationSource.Primary,
                            Translation.V1.TranslationSource.Secondary
                        }
                    },
                    new TranslationSources
                    {
                        Values =
                        {
                            Translation.V1.TranslationSource.Primary,
                            Translation.V1.TranslationSource.Secondary
                        }
                    },
                    new TranslationSources
                    {
                        Values =
                        {
                            Translation.V1.TranslationSource.Primary,
                            Translation.V1.TranslationSource.Secondary
                        }
                    },
                    new TranslationSources
                    {
                        Values =
                        {
                            Translation.V1.TranslationSource.Primary,
                            Translation.V1.TranslationSource.Secondary
                        }
                    },
                    new TranslationSources
                    {
                        Values =
                        {
                            Translation.V1.TranslationSource.Primary,
                            Translation.V1.TranslationSource.Secondary
                        }
                    }
                },
                Alignment =
                {
                    new Translation.V1.AlignedWordPair { SourceIndex = 0, TargetIndex = 0 },
                    new Translation.V1.AlignedWordPair { SourceIndex = 1, TargetIndex = 1 },
                    new Translation.V1.AlignedWordPair { SourceIndex = 2, TargetIndex = 2 },
                    new Translation.V1.AlignedWordPair { SourceIndex = 3, TargetIndex = 3 },
                    new Translation.V1.AlignedWordPair { SourceIndex = 4, TargetIndex = 4 }
                },
                Phrases =
                {
                    new Translation.V1.Phrase
                    {
                        SourceSegmentStart = 0,
                        SourceSegmentEnd = 5,
                        TargetSegmentCut = 5
                    }
                }
            };
            var translateResponse = new TranslateResponse { Results = { translationResult } };
            EchoClient
                .TranslateAsync(Arg.Any<TranslateRequest>(), null, null, Arg.Any<CancellationToken>())
                .Returns(CreateAsyncUnaryCall(translateResponse));
            EchoClient
                .GetQueueSizeAsync(Arg.Any<GetQueueSizeRequest>(), null, null, Arg.Any<CancellationToken>())
                .Returns(CreateAsyncUnaryCall(new GetQueueSizeResponse() { Size = 0 }));

            NmtClient = Substitute.For<TranslationEngineApi.TranslationEngineApiClient>();
            NmtClient
                .CreateAsync(Arg.Any<CreateRequest>(), null, null, Arg.Any<CancellationToken>())
                .Returns(CreateAsyncUnaryCall(new Empty()));
            NmtClient
                .DeleteAsync(Arg.Any<DeleteRequest>(), null, null, Arg.Any<CancellationToken>())
                .Returns(CreateAsyncUnaryCall(new Empty()));
            NmtClient
                .StartBuildAsync(Arg.Any<StartBuildRequest>(), null, null, Arg.Any<CancellationToken>())
                .Returns(CreateAsyncUnaryCall(new Empty()));
            NmtClient
                .CancelBuildAsync(Arg.Any<CancelBuildRequest>(), null, null, Arg.Any<CancellationToken>())
                .Returns(CreateAsyncUnaryCall(new CancelBuildResponse()));
            NmtClient
                .GetWordGraphAsync(Arg.Any<GetWordGraphRequest>(), null, null, Arg.Any<CancellationToken>())
                .Returns(CreateAsyncUnaryCall<GetWordGraphResponse>(StatusCode.Unimplemented));
            NmtClient
                .TranslateAsync(Arg.Any<TranslateRequest>(), null, null, Arg.Any<CancellationToken>())
                .Returns(CreateAsyncUnaryCall<TranslateResponse>(StatusCode.Unimplemented));

            SmtClient = Substitute.For<TranslationEngineApi.TranslationEngineApiClient>();
        }

        public ServalWebApplicationFactory Factory { get; }
        public IRepository<Engine> Engines { get; }
        public IRepository<DataFiles.Models.DataFile> DataFiles { get; }
        public IRepository<DataFiles.Models.Corpus> Corpora { get; }
        public IRepository<Translation.Models.Pretranslation> Pretranslations { get; }
        public IRepository<Build> Builds { get; }
        public TranslationEngineApi.TranslationEngineApiClient EchoClient { get; }
        public TranslationEngineApi.TranslationEngineApiClient NmtClient { get; }
        public TranslationEngineApi.TranslationEngineApiClient SmtClient { get; }

        public TranslationBuildsClient CreateTranslationBuildsClient(IEnumerable<string>? scope = null)
        {
            scope ??= [Scopes.ReadTranslationEngines,];
            HttpClient httpClient = Factory
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureTestServices(services =>
                    {
                        GrpcClientFactory grpcClientFactory = Substitute.For<GrpcClientFactory>();
                        grpcClientFactory
                            .CreateClient<TranslationEngineApi.TranslationEngineApiClient>("Echo")
                            .Returns(EchoClient);
                        grpcClientFactory
                            .CreateClient<TranslationEngineApi.TranslationEngineApiClient>("Nmt")
                            .Returns(NmtClient);
                        services.AddSingleton(grpcClientFactory);
                        services.AddTransient(CreateFileSystem);
                    });
                })
                .CreateClient();
            httpClient.DefaultRequestHeaders.Add("Scope", string.Join(" ", scope));
            return new TranslationBuildsClient(httpClient);
        }

        public TranslationEnginesClient CreateTranslationEnginesClient(IEnumerable<string>? scope = null)
        {
            scope ??=
            [
                Scopes.CreateTranslationEngines,
                Scopes.ReadTranslationEngines,
                Scopes.UpdateTranslationEngines,
                Scopes.DeleteTranslationEngines
            ];
            HttpClient httpClient = Factory
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureTestServices(services =>
                    {
                        GrpcClientFactory grpcClientFactory = Substitute.For<GrpcClientFactory>();
                        grpcClientFactory
                            .CreateClient<TranslationEngineApi.TranslationEngineApiClient>("Echo")
                            .Returns(EchoClient);
                        grpcClientFactory
                            .CreateClient<TranslationEngineApi.TranslationEngineApiClient>("Nmt")
                            .Returns(NmtClient);
                        services.AddSingleton(grpcClientFactory);
                        services.AddTransient(CreateFileSystem);
                    });
                })
                .CreateClient();
            httpClient.DefaultRequestHeaders.Add("Scope", string.Join(" ", scope));
            return new TranslationEnginesClient(httpClient);
        }

        public TranslationEngineTypesClient CreateTranslationEngineTypesClient(IEnumerable<string>? scope = null)
        {
            scope ??=
            [
                Scopes.CreateTranslationEngines,
                Scopes.ReadTranslationEngines,
                Scopes.UpdateTranslationEngines,
                Scopes.DeleteTranslationEngines
            ];
            HttpClient httpClient = Factory
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureTestServices(services =>
                    {
                        GrpcClientFactory grpcClientFactory = Substitute.For<GrpcClientFactory>();
                        grpcClientFactory
                            .CreateClient<TranslationEngineApi.TranslationEngineApiClient>("Echo")
                            .Returns(EchoClient);
                        grpcClientFactory
                            .CreateClient<TranslationEngineApi.TranslationEngineApiClient>("Nmt")
                            .Returns(NmtClient);
                        grpcClientFactory
                            .CreateClient<TranslationEngineApi.TranslationEngineApiClient>("SmtTransfer")
                            .Returns(SmtClient);
                        services.AddSingleton(grpcClientFactory);
                    });
                })
                .CreateClient();
            NmtClient
                .GetQueueSizeAsync(Arg.Any<GetQueueSizeRequest>(), null, null, Arg.Any<CancellationToken>())
                .Returns(CreateAsyncUnaryCall(new GetQueueSizeResponse() { Size = 0 }));
            NmtClient
                .GetLanguageInfoAsync(Arg.Any<GetLanguageInfoRequest>(), null, null, Arg.Any<CancellationToken>())
                .Returns(
                    CreateAsyncUnaryCall(new GetLanguageInfoResponse() { InternalCode = "abc_123", IsNative = true })
                );
            SmtClient
                .GetLanguageInfoAsync(Arg.Any<GetLanguageInfoRequest>(), null, null, Arg.Any<CancellationToken>())
                .Returns(
                    CreateAsyncUnaryCall(new GetLanguageInfoResponse() { InternalCode = "abc_123", IsNative = true })
                );
            EchoClient
                .GetLanguageInfoAsync(Arg.Any<GetLanguageInfoRequest>(), null, null, Arg.Any<CancellationToken>())
                .Returns(
                    CreateAsyncUnaryCall(new GetLanguageInfoResponse() { InternalCode = "abc_123", IsNative = true })
                );
            httpClient.DefaultRequestHeaders.Add("Scope", string.Join(" ", scope));
            return new TranslationEngineTypesClient(httpClient);
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

#pragma warning restore CS0612 // Type or member is obsolete
