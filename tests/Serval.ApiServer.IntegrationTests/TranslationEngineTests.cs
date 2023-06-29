namespace Serval.ApiServer;
using Serval.Translation.V1;
using Google.Protobuf.WellKnownTypes;
using Serval.DataFiles.Models;

[TestFixture]
[Category("Integration")]
public class TranslationEngineTests
{
    TestEnvironment? _env;
    TranslationCorpusConfig? _testCorpusConfig;

    const string ECHO_ENGINE1_ID = "e00000000000000000000000";
    const string ECHO_ENGINE2_ID = "e00000000000000000000001";
    const string ECHO_ENGINE3_ID = "e00000000000000000000002";
    const string SMT_ENGINE1_ID = "be0000000000000000000000";
    const string NMT_ENGINE1_ID = "ce0000000000000000000000";
    const string FILE1_ID = "f00000000000000000000000";
    const string FILE1_FILENAME = "abcd";
    const string FILE2_ID = "f00000000000000000000001";
    const string FILE2_FILENAME = "efgh";
    const string DOES_NOT_EXIST_ENGINE_ID = "e00000000000000000000003";

    [SetUp]
    public async Task SetUp()
    {
        _env = new TestEnvironment();
        Engine e0,
            e1,
            e2,
            be0,
            ce0;
        e0 = new Engine
        {
            Id = ECHO_ENGINE1_ID,
            Name = "e0",
            SourceLanguage = "en",
            TargetLanguage = "en",
            Type = "Echo",
            Owner = "client1"
        };
        e1 = new Engine
        {
            Id = ECHO_ENGINE2_ID,
            Name = "e1",
            SourceLanguage = "en",
            TargetLanguage = "es",
            Type = "Echo",
            Owner = "client1"
        };
        e2 = new Engine
        {
            Id = ECHO_ENGINE3_ID,
            Name = "e2",
            SourceLanguage = "en",
            TargetLanguage = "en",
            Type = "Echo",
            Owner = "client2"
        };
        be0 = new Engine
        {
            Id = SMT_ENGINE1_ID,
            Name = "be0",
            SourceLanguage = "en",
            TargetLanguage = "en",
            Type = "SMTTransfer",
            Owner = "client1"
        };
        ce0 = new Engine
        {
            Id = NMT_ENGINE1_ID,
            Name = "ce0",
            SourceLanguage = "en",
            TargetLanguage = "en",
            Type = "Nmt",
            Owner = "client1"
        };

        await _env.Engines.InsertAllAsync(new[] { e0, e1, e2, be0, ce0 });

        DataFile srcFile,
            trgFile;
        srcFile = new DataFile
        {
            Id = FILE1_ID,
            Owner = "client1",
            Name = "src.txt",
            Filename = FILE1_FILENAME,
            Format = Shared.Contracts.FileFormat.Text
        };
        trgFile = new DataFile
        {
            Id = FILE2_ID,
            Owner = "client1",
            Name = "trg.txt",
            Filename = FILE2_FILENAME,
            Format = Shared.Contracts.FileFormat.Text
        };
        await _env.DataFiles.InsertAllAsync(new[] { srcFile, trgFile });

        _testCorpusConfig = new TranslationCorpusConfig
        {
            Name = "TestCorpus",
            SourceLanguage = "en",
            TargetLanguage = "en",
            SourceFiles =
            {
                new TranslationCorpusFileConfig { FileId = FILE1_ID, TextId = "all" }
            },
            TargetFiles =
            {
                new TranslationCorpusFileConfig { FileId = FILE2_ID, TextId = "all" }
            }
        };
    }

    [Test]
    [TestCase(new[] { Scopes.ReadTranslationEngines }, 200)]
    [TestCase(new[] { Scopes.ReadFiles }, 403)] //Arbitrary unrelated privilege
    public async Task GetAllAsync(IEnumerable<string> scope, int expectedStatusCode)
    {
        ITranslationEnginesClient client = _env!.CreateClient(scope);
        ServalApiException? ex;
        switch (expectedStatusCode)
        {
            case 200:
                ICollection<TranslationEngine> results = await client.GetAllAsync();
                Assert.That(results, Has.Count.EqualTo(4));
                Assert.That(results.All(eng => eng.SourceLanguage.Equals("en")));
                break;
            case 403:
                ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.GetAllAsync();
                });
                Assert.That(ex!.StatusCode, Is.EqualTo(expectedStatusCode));
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
    public async Task GetByIdAsync(IEnumerable<string> scope, int expectedStatusCode, string engineId)
    {
        ITranslationEnginesClient client = _env!.CreateClient(scope);
        ServalApiException? ex;
        switch (expectedStatusCode)
        {
            case 200:
                TranslationEngine result = await client.GetAsync(engineId);
                Assert.That(result.Name, Is.EqualTo("e0"));
                break;
            case 403:
            case 404:
                ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.GetAsync(engineId);
                });
                Assert.That(ex!.StatusCode, Is.EqualTo(expectedStatusCode));
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
        ITranslationEnginesClient client = _env!.CreateClient(scope);
        ServalApiException? ex;
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
                Assert.NotNull(engine);
                Assert.That(engine.Name, Is.EqualTo("test"));
                break;
            case 400:
            case 403:
                ex = Assert.ThrowsAsync<ServalApiException>(async () =>
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
                Assert.That(ex!.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
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
        ITranslationEnginesClient client = _env!.CreateClient(scope);
        ServalApiException? ex;
        switch (expectedStatusCode)
        {
            case 200:
                Assert.DoesNotThrowAsync(async () =>
                {
                    await client.DeleteAsync(engineId);
                });
                ICollection<TranslationEngine> results = await client.GetAllAsync();
                Assert.That(results, Has.Count.EqualTo(3));
                Assert.That(results.All(eng => eng.SourceLanguage.Equals("en")));
                break;
            case 403:
            case 404:
                ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.DeleteAsync(engineId);
                });
                Assert.That(ex!.StatusCode, Is.EqualTo(expectedStatusCode));
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
    public async Task TranslateSegmentWithEngineByIdAsync(
        IEnumerable<string> scope,
        int expectedStatusCode,
        string engineId
    )
    {
        ITranslationEnginesClient client = _env!.CreateClient(scope);
        ServalApiException? ex;
        Serval.Client.TranslationResult? result = null;
        switch (expectedStatusCode)
        {
            case 200:
                await _env.Builds.InsertAsync(
                    new Build { EngineRef = engineId, State = Shared.Contracts.JobState.Completed }
                );
                Assert.DoesNotThrowAsync(async () =>
                {
                    result = await client.TranslateAsync(engineId, "This is a test .");
                });
                Assert.NotNull(result);
                Assert.That(result!.Translation, Is.EqualTo("This is a test ."));
                Assert.That(result.Sources, Has.Count.EqualTo(5));
                Assert.That(
                    result.Sources,
                    Has.All.EquivalentTo(new[] { Client.TranslationSource.Primary, Client.TranslationSource.Secondary })
                );
                break;
            // case 405: //NOTE: Cannot test 405s because they are handled in middleware that does not run in test environment
            //     TranslationCorpus added_corpus405 = await client.AddCorpusAsync(engineId, _testCorpusConfig!);
            //     var ptcc405 = new PretranslateCorpusConfig
            //     {
            //         CorpusId = added_corpus405.Id,
            //         TextIds = new List<string> { "all" }
            //     };
            //     var tbc405 = new TranslationBuildConfig
            //     {
            //         Pretranslate = new List<PretranslateCorpusConfig> { ptcc405 }
            //     };
            //     Assert.DoesNotThrowAsync(async () =>
            //     {
            //         await client.StartBuildAsync(engineId, tbc405);
            //     });
            //     ex = Assert.ThrowsAsync<ServalApiException>(async () =>
            //     {
            //         await client.TranslateAsync(engineId, "This is a test .");
            //     });
            //     Assert.That(ex!.StatusCode, Is.EqualTo(expectedStatusCode));

            //     break;
            case 404:
            case 409:
                ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.TranslateAsync(engineId, "This is a test .");
                });
                Assert.That(ex!.StatusCode, Is.EqualTo(expectedStatusCode));

                break;
            default:
                Assert.Fail("Unanticipated expectedStatusCode. Check test case for typo.");
                break;
        }
    }

    [Test]
    [TestCase(new[] { Scopes.ReadTranslationEngines, Scopes.UpdateTranslationEngines }, 200, ECHO_ENGINE1_ID)]
    [TestCase(new[] { Scopes.ReadTranslationEngines }, 404, DOES_NOT_EXIST_ENGINE_ID)]
    [TestCase(new[] { Scopes.ReadTranslationEngines, Scopes.UpdateTranslationEngines }, 409, ECHO_ENGINE1_ID)]
    public async Task TranslateNSegmentWithEngineByIdAsync(
        IEnumerable<string> scope,
        int expectedStatusCode,
        string engineId
    )
    {
        ITranslationEnginesClient client = _env!.CreateClient(scope);
        ServalApiException? ex;
        ICollection<Serval.Client.TranslationResult>? results = null;
        switch (expectedStatusCode)
        {
            case 200:
                await _env.Builds.InsertAsync(
                    new Build { EngineRef = engineId, State = Shared.Contracts.JobState.Completed }
                );
                Assert.DoesNotThrowAsync(async () =>
                {
                    results = await client.TranslateNAsync(engineId, 1, "This is a test .");
                });
                Assert.NotNull(results);
                Assert.That(results, Has.Count.EqualTo(1));
                Assert.That(results!.First().Translation, Is.EqualTo("This is a test ."));
                Assert.That(results!.First().Sources, Has.Count.EqualTo(5));
                Assert.That(
                    results!.First().Sources,
                    Has.All.EquivalentTo(new[] { Client.TranslationSource.Primary, Client.TranslationSource.Secondary })
                );
                break;
            case 404:
            case 409:
                ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.TranslateNAsync(engineId, 1, "This is a test .");
                });
                Assert.That(ex!.StatusCode, Is.EqualTo(expectedStatusCode));

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
    public async Task GetWordGraphForSegmentByIdAsync(
        IEnumerable<string> scope,
        int expectedStatusCode,
        string engineId
    )
    {
        ITranslationEnginesClient client = _env!.CreateClient(scope);
        ServalApiException? ex;
        Serval.Client.WordGraph? wg = null;
        switch (expectedStatusCode)
        {
            case 200:
                TranslationCorpus added_corpus = await client.AddCorpusAsync(engineId, _testCorpusConfig!);
                await _env.Builds.InsertAsync(
                    new Build { EngineRef = engineId, State = Shared.Contracts.JobState.Completed }
                );
                Assert.DoesNotThrowAsync(async () =>
                {
                    wg = await client.GetWordGraphAsync(engineId, "This is a test .");
                });
                Assert.NotNull(wg);
                Assert.Multiple(() =>
                {
                    Assert.That(wg!.FinalStates.First(), Is.EqualTo(4));
                    Assert.That(wg!.Arcs.Count(), Is.EqualTo(4));
                });
                break;
            case 404:
            case 409:
                ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.GetWordGraphAsync(engineId, "This is a test .");
                });
                Assert.That(ex!.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
            default:
                Assert.Fail("Unanticipated expectedStatusCode. Check test case for typo.");
                break;
        }
    }

    [Test]
    [TestCase(new[] { Scopes.UpdateTranslationEngines }, 200, ECHO_ENGINE1_ID)]
    [TestCase(new[] { Scopes.UpdateTranslationEngines }, 404, DOES_NOT_EXIST_ENGINE_ID)]
    [TestCase(new[] { Scopes.UpdateTranslationEngines }, 409, ECHO_ENGINE1_ID)]
    public async Task TrainEngineByIdOnSegmentPairAsync(
        IEnumerable<string> scope,
        int expectedStatusCode,
        string engineId
    )
    {
        ITranslationEnginesClient client = _env!.CreateClient(scope);
        ServalApiException? ex;
        SegmentPair sp = new SegmentPair();
        sp.SourceSegment = "This is a test .";
        sp.TargetSegment = "This is a test .";
        sp.SentenceStart = true;
        switch (expectedStatusCode)
        {
            case 200:
                TranslationCorpus added_corpus = await client.AddCorpusAsync(engineId, _testCorpusConfig!);
                await _env.Builds.InsertAsync(
                    new Build { EngineRef = engineId, State = Shared.Contracts.JobState.Completed }
                );
                Assert.DoesNotThrowAsync(async () =>
                {
                    await client.TrainSegmentAsync(engineId, sp);
                });
                break;
            case 404:
            case 409:
                ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.TrainSegmentAsync(engineId, sp);
                });
                Assert.That(ex!.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
            default:
                Assert.Fail("Unanticipated expectedStatusCode. Check test case for typo.");
                break;
        }
    }

    [Test]
    [TestCase(new[] { Scopes.UpdateTranslationEngines }, 201, ECHO_ENGINE1_ID)]
    [TestCase(new[] { Scopes.UpdateTranslationEngines }, 404, DOES_NOT_EXIST_ENGINE_ID)]
    public async Task AddCorpusToEngineByIdAsync(IEnumerable<string> scope, int expectedStatusCode, string engineId)
    {
        ITranslationEnginesClient client = _env!.CreateClient(scope);
        ServalApiException? ex;
        switch (expectedStatusCode)
        {
            case 201:
                TranslationCorpus result = new TranslationCorpus();

                Assert.DoesNotThrowAsync(async () =>
                {
                    result = await client.AddCorpusAsync(engineId, _testCorpusConfig!);
                });
                Assert.Multiple(() =>
                {
                    Assert.That(result.Name, Is.EqualTo("TestCorpus"));
                    Assert.That(result.SourceFiles.First().File.Id, Is.EqualTo(FILE1_ID));
                    Assert.That(result.TargetFiles.First().File.Id, Is.EqualTo(FILE2_ID));
                });
                Engine? engine = await _env.Engines.GetAsync(engineId);
                Assert.That(engine, Is.Not.Null);
                Assert.Multiple(() =>
                {
                    Assert.That(engine!.Corpora[0].SourceFiles[0].Filename, Is.EqualTo(FILE1_FILENAME));
                    Assert.That(engine.Corpora[0].TargetFiles[0].Filename, Is.EqualTo(FILE2_FILENAME));
                });
                break;
            case 404:
                ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    result = await client.AddCorpusAsync(engineId, _testCorpusConfig!);
                });
                Assert.That(ex!.StatusCode, Is.EqualTo(expectedStatusCode));
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
    public async Task UpdateCorpusByIdForEngineByIdAsync(
        IEnumerable<string> scope,
        int expectedStatusCode,
        string engineId
    )
    {
        TranslationEnginesClient client = _env!.CreateClient(scope);
        ServalApiException? ex;
        switch (expectedStatusCode)
        {
            case 200:
                TranslationCorpus result = await client.AddCorpusAsync(engineId, _testCorpusConfig!);

                Assert.DoesNotThrowAsync(async () =>
                {
                    var src = new[]
                    {
                        new TranslationCorpusFileConfig { FileId = FILE2_ID, TextId = "all" }
                    };
                    var trg = new[]
                    {
                        new TranslationCorpusFileConfig { FileId = FILE1_ID, TextId = "all" }
                    };
                    var updateConfig = new TranslationCorpusUpdateConfig { SourceFiles = src, TargetFiles = trg };
                    await client.UpdateCorpusAsync(engineId, result.Id, updateConfig);
                });
                Engine? engine = await _env.Engines.GetAsync(engineId);
                Assert.That(engine, Is.Not.Null);
                Assert.Multiple(() =>
                {
                    Assert.That(engine!.Corpora[0].SourceFiles[0].Filename, Is.EqualTo(FILE2_FILENAME));
                    Assert.That(engine.Corpora[0].TargetFiles[0].Filename, Is.EqualTo(FILE1_FILENAME));
                });
                break;
            case 400:
            case 404:
                ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    var src = new[]
                    {
                        new TranslationCorpusFileConfig { FileId = FILE2_ID, TextId = "all" }
                    };
                    var trg = new[]
                    {
                        new TranslationCorpusFileConfig { FileId = FILE1_ID, TextId = "all" }
                    };
                    var updateConfig = new TranslationCorpusUpdateConfig { SourceFiles = src, TargetFiles = trg };
                    await client.UpdateCorpusAsync(engineId, "c00000000000000000000000", updateConfig);
                });
                Assert.That(ex!.StatusCode, Is.EqualTo(expectedStatusCode));
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
    public async Task GetAllCorporaForEngineByIdAsync(
        IEnumerable<string> scope,
        int expectedStatusCode,
        string engineId
    )
    {
        ITranslationEnginesClient client = _env!.CreateClient(scope);
        ServalApiException? ex;
        switch (expectedStatusCode)
        {
            case 200:
                TranslationCorpus result = await client.AddCorpusAsync(engineId, _testCorpusConfig!);
                TranslationCorpus result_afterAdd = (await client.GetAllCorporaAsync(engineId)).First();
                Assert.Multiple(() =>
                {
                    Assert.That(result_afterAdd.Name, Is.EqualTo(result.Name));
                    Assert.That(result_afterAdd.SourceLanguage, Is.EqualTo(result.SourceLanguage));
                    Assert.That(result_afterAdd.TargetLanguage, Is.EqualTo(result.TargetLanguage));
                });
                break;
            case 404:
                ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    TranslationCorpus result = (await client.GetAllCorporaAsync(engineId)).First();
                });
                Assert.That(ex!.StatusCode, Is.EqualTo(expectedStatusCode));
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
    public async Task GetCorpusByIdForEngineByIdAsync(
        IEnumerable<string> scope,
        int expectedStatusCode,
        string engineId,
        bool addCorpus = false
    )
    {
        ITranslationEnginesClient client = _env!.CreateClient(scope);
        ServalApiException? ex;
        TranslationCorpus? result = null;
        if (addCorpus)
        {
            result = await client.AddCorpusAsync(engineId, _testCorpusConfig!);
        }
        switch (expectedStatusCode)
        {
            case 200:
                if (result is null)
                    Assert.Fail();
                TranslationCorpus result_afterAdd = await client.GetCorpusAsync(engineId, result!.Id);
                Assert.Multiple(() =>
                {
                    Assert.That(result_afterAdd.Name, Is.EqualTo(result.Name));
                    Assert.That(result_afterAdd.SourceLanguage, Is.EqualTo(result.SourceLanguage));
                    Assert.That(result_afterAdd.TargetLanguage, Is.EqualTo(result.TargetLanguage));
                });
                break;
            case 404:
                ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    TranslationCorpus result_afterAdd = await client.GetCorpusAsync(
                        engineId,
                        "c00000000000000000000000"
                    );
                });
                Assert.That(ex!.StatusCode, Is.EqualTo(expectedStatusCode));
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
    public async Task DeleteCorpusByIdForEngineByIdAsync(
        IEnumerable<string> scope,
        int expectedStatusCode,
        string engineId
    )
    {
        ITranslationEnginesClient client = _env!.CreateClient(scope);
        ServalApiException? ex;
        switch (expectedStatusCode)
        {
            case 200:
                TranslationCorpus result = await client.AddCorpusAsync(engineId, _testCorpusConfig!);
                Assert.DoesNotThrowAsync(async () =>
                {
                    await client.DeleteCorpusAsync(engineId, result.Id);
                });
                ICollection<TranslationCorpus> results_afterDelete = await client.GetAllCorporaAsync(engineId);
                Assert.That(results_afterDelete, Has.Count.EqualTo(0));
                break;
            case 404:
                ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.DeleteCorpusAsync(engineId, "c00000000000000000000000");
                });
                Assert.That(ex!.StatusCode, Is.EqualTo(expectedStatusCode));
                break;

            default:
                Assert.Fail("Unanticipated expectedStatusCode. Check test case for typo.");
                break;
        }
    }

    [Test]
    [TestCase(
        new[] { Scopes.ReadTranslationEngines, Scopes.CreateTranslationEngines, Scopes.UpdateTranslationEngines },
        200,
        ECHO_ENGINE1_ID
    )]
    [TestCase(
        new[] { Scopes.ReadTranslationEngines, Scopes.CreateTranslationEngines, Scopes.UpdateTranslationEngines },
        409,
        ECHO_ENGINE1_ID
    )]
    [TestCase(
        new[] { Scopes.ReadTranslationEngines, Scopes.CreateTranslationEngines, Scopes.UpdateTranslationEngines },
        404,
        DOES_NOT_EXIST_ENGINE_ID,
        false
    )]
    [TestCase(
        new[] { Scopes.ReadTranslationEngines, Scopes.CreateTranslationEngines, Scopes.UpdateTranslationEngines },
        404,
        ECHO_ENGINE1_ID,
        false
    )]
    public async Task GetAllPretranslationsForCorpusByIdForEngineByIdAsync(
        IEnumerable<string> scope,
        int expectedStatusCode,
        string engineId,
        bool addCorpus = true
    )
    {
        ITranslationEnginesClient client = _env!.CreateClient(scope);
        ServalApiException? ex = null;
        TranslationCorpus? added_corpus = null;
        if (addCorpus)
        {
            added_corpus = await client.AddCorpusAsync(engineId, _testCorpusConfig!);
            Serval.Translation.Models.Pretranslation pret = new Serval.Translation.Models.Pretranslation
            {
                CorpusRef = added_corpus.Id,
                TextId = "all",
                EngineRef = engineId,
                Refs = new List<string> { "ref1", "ref2" },
                Translation = "translation"
            };
            await _env.Pretranslations.InsertAsync(pret);
        }

        switch (expectedStatusCode)
        {
            case 200:
                Assert.True(addCorpus, "Check that addCorpus is true - cannot build without added corpus");
                await _env.Builds.InsertAsync(
                    new Build { EngineRef = engineId, State = Shared.Contracts.JobState.Completed }
                );
                ICollection<Serval.Client.Pretranslation>? results = null;
                Assert.DoesNotThrowAsync(async () =>
                {
                    results = await client.GetAllPretranslationsAsync(engineId, added_corpus!.Id);
                });
                Assert.NotNull(results);
                Assert.That(results!.First().TextId, Is.EqualTo("all"));
                break;
            case 404:
            case 409:
                ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    results = await client.GetAllPretranslationsAsync(
                        engineId,
                        addCorpus ? added_corpus!.Id : "cccccccccccccccccccccccc"
                    );
                });
                Assert.That(ex!.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
            default:
                Assert.Fail("Unanticipated expectedStatusCode. Check test case for typo.");
                break;
        }
    }

    [Test]
    [TestCase(
        new[] { Scopes.ReadTranslationEngines, Scopes.CreateTranslationEngines, Scopes.UpdateTranslationEngines },
        200,
        ECHO_ENGINE1_ID,
        true,
        "all"
    )]
    [TestCase(
        new[] { Scopes.ReadTranslationEngines, Scopes.CreateTranslationEngines, Scopes.UpdateTranslationEngines },
        409,
        ECHO_ENGINE1_ID,
        true,
        "all"
    )]
    [TestCase(
        new[] { Scopes.ReadTranslationEngines, Scopes.CreateTranslationEngines, Scopes.UpdateTranslationEngines },
        404,
        DOES_NOT_EXIST_ENGINE_ID,
        false,
        "all"
    )]
    [TestCase(
        new[] { Scopes.ReadTranslationEngines, Scopes.CreateTranslationEngines, Scopes.UpdateTranslationEngines },
        404,
        ECHO_ENGINE1_ID,
        false,
        "all"
    )]
    [TestCase(
        new[] { Scopes.ReadTranslationEngines, Scopes.CreateTranslationEngines, Scopes.UpdateTranslationEngines },
        404,
        ECHO_ENGINE1_ID,
        true,
        "not_the_right_id"
    )]
    public async Task GetPretranslationForTextByIdForCorpusByIdForEngineByIdAsync(
        IEnumerable<string> scope,
        int expectedStatusCode,
        string engineId,
        bool addCorpus,
        string textId
    )
    {
        ITranslationEnginesClient client = _env!.CreateClient(scope);
        ServalApiException? ex = null;
        TranslationCorpus? added_corpus = null;
        if (addCorpus)
        {
            added_corpus = await client.AddCorpusAsync(engineId, _testCorpusConfig!);
            Serval.Translation.Models.Pretranslation pret = new Serval.Translation.Models.Pretranslation
            {
                CorpusRef = added_corpus.Id,
                TextId = "all", //Note that this is not equal to textId necessarily
                EngineRef = engineId,
                Refs = new List<string> { "ref1", "ref2" },
                Translation = "translation"
            };
            await _env.Pretranslations.InsertAsync(pret);
        }

        switch (expectedStatusCode)
        {
            case 200:
                Assert.True(addCorpus, "Check that addCorpus is true - cannot build without added corpus");
                await _env.Builds.InsertAsync(
                    new Build { EngineRef = engineId, State = Shared.Contracts.JobState.Completed }
                );
                ICollection<Serval.Client.Pretranslation>? results = null;
                Assert.DoesNotThrowAsync(async () =>
                {
                    results = await client.GetAllPretranslations2Async(engineId, added_corpus!.Id, textId);
                });
                Assert.NotNull(results);
                Assert.That(results!.First().TextId, Is.EqualTo("all"));
                break;
            case 404:
            case 409:
                ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    results = await client.GetAllPretranslations2Async(
                        engineId,
                        addCorpus ? added_corpus!.Id : "cccccccccccccccccccccccc",
                        textId
                    );
                });
                Assert.That(ex!.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
            default:
                Assert.Fail("Unanticipated expectedStatusCode. Check test case for typo.");
                break;
        }
    }

    [Test]
    [TestCase(new[] { Scopes.ReadTranslationEngines }, 200, SMT_ENGINE1_ID)]
    [TestCase(new[] { Scopes.ReadTranslationEngines }, 404, DOES_NOT_EXIST_ENGINE_ID, false)]
    public async Task GetAllBuildsForEngineByIdAsync(
        IEnumerable<string> scope,
        int expectedStatusCode,
        string engineId,
        bool addBuild = true
    )
    {
        ITranslationEnginesClient client = _env!.CreateClient(scope);
        ServalApiException? ex = null;
        Build? build = null;
        if (addBuild)
        {
            build = new Build { EngineRef = engineId };
            await _env.Builds.InsertAsync(build);
        }
        switch (expectedStatusCode)
        {
            case 200:
                ICollection<Serval.Client.TranslationBuild>? results = null;
                Assert.DoesNotThrowAsync(async () =>
                {
                    results = await client.GetAllBuildsAsync(engineId);
                });
                Assert.NotNull(results);
                Assert.That(results!.Count(), Is.GreaterThan(0));
                Assert.Multiple(() =>
                {
                    Assert.That(results!.First().Revision, Is.EqualTo(1));
                    Assert.That(results!.First().Id, Is.EqualTo(build!.Id));
                    Assert.That(results!.First().State, Is.EqualTo(JobState.Pending));
                });
                break;
            case 404:
                ex = Assert.ThrowsAsync<ServalApiException>(async () =>
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
    public async Task GetBuildByIdForEngineByIdAsync(
        IEnumerable<string> scope,
        int expectedStatusCode,
        string engineId,
        bool addBuild = true
    )
    {
        ITranslationEnginesClient client = _env!.CreateClient(scope);
        ServalApiException? ex = null;
        Build? build = null;
        if (addBuild)
        {
            build = new Build { EngineRef = engineId };
            await _env.Builds.InsertAsync(build);
        }
        switch (expectedStatusCode)
        {
            case 200:
                Serval.Client.TranslationBuild? result = null;
                Assert.NotNull(build);
                Assert.DoesNotThrowAsync(async () =>
                {
                    result = await client.GetBuildAsync(engineId, build!.Id);
                });
                Assert.Multiple(() =>
                {
                    Assert.That(result!.Revision, Is.EqualTo(1));
                    Assert.That(result.Id, Is.EqualTo(build!.Id));
                    Assert.That(result.State, Is.EqualTo(JobState.Pending));
                });
                break;
            case 404:
                ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.GetBuildAsync(engineId, "bbbbbbbbbbbbbbbbbbbbbbbb");
                });
                Assert.That(ex!.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
            case 408:
                Assert.NotNull(build);
                ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.GetBuildAsync(engineId, build!.Id, 3);
                });
                Assert.That(ex!.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
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
    public async Task StartBuildsForEngineByIdAsync(IEnumerable<string> scope, int expectedStatusCode, string engineId)
    {
        ITranslationEnginesClient client = _env!.CreateClient(scope);
        ServalApiException? ex = null;
        PretranslateCorpusConfig? ptcc = null;
        TranslationBuildConfig? tbc = null;
        switch (expectedStatusCode)
        {
            case 201:
                TranslationCorpus added_corpus = await client.AddCorpusAsync(engineId, _testCorpusConfig!);
                Serval.Client.TranslationBuild? result_afterStart = null;
                ptcc = new PretranslateCorpusConfig
                {
                    CorpusId = added_corpus.Id,
                    TextIds = new List<string> { "all" }
                };
                tbc = new TranslationBuildConfig { Pretranslate = new List<PretranslateCorpusConfig> { ptcc } };
                Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    result_afterStart = await client.GetCurrentBuildAsync(engineId);
                });
                Assert.DoesNotThrowAsync(async () =>
                {
                    await client.StartBuildAsync(engineId, tbc);
                });
                Assert.DoesNotThrowAsync(async () =>
                {
                    await client.GetCurrentBuildAsync(engineId);
                });
                break;
            case 404:
                ptcc = new PretranslateCorpusConfig
                {
                    CorpusId = "cccccccccccccccccccccccc",
                    TextIds = new List<string> { "all" }
                };
                tbc = new TranslationBuildConfig { Pretranslate = new List<PretranslateCorpusConfig> { ptcc } };
                ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.StartBuildAsync(engineId, tbc);
                });
                Assert.That(ex!.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
            default:
                Assert.Fail("Unanticipated expectedStatusCode. Check test case for typo.");
                break;
        }
    }

    [Test]
    [TestCase(new[] { Scopes.ReadTranslationEngines }, 200, ECHO_ENGINE1_ID)]
    [TestCase(new[] { Scopes.ReadTranslationEngines }, 408, ECHO_ENGINE1_ID)]
    [TestCase(new[] { Scopes.ReadTranslationEngines }, 204, ECHO_ENGINE1_ID, false)]
    [TestCase(new[] { Scopes.ReadTranslationEngines }, 404, DOES_NOT_EXIST_ENGINE_ID, false)]
    public async Task GetCurrentBuildForEngineByIdAsync(
        IEnumerable<string> scope,
        int expectedStatusCode,
        string engineId,
        bool addBuild = true
    )
    {
        ITranslationEnginesClient client = _env!.CreateClient(scope);
        ServalApiException? ex = null;
        Build? build = null;
        TranslationBuild? result = null;
        if (addBuild)
        {
            build = new Build { EngineRef = engineId };
            await _env.Builds.InsertAsync(build);
        }

        switch (expectedStatusCode)
        {
            case 200:
                Assert.DoesNotThrowAsync(async () =>
                {
                    result = await client.GetCurrentBuildAsync(engineId);
                });
                Assert.NotNull(result);
                Assert.NotNull(build);
                Assert.That(result!.Id, Is.EqualTo(build!.Id));
                break;
            case 204:
            case 404:
                ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.GetCurrentBuildAsync(engineId);
                });
                Assert.That(ex!.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
            case 408:
                ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.GetCurrentBuildAsync(engineId, minRevision: 3);
                });
                Assert.That(ex!.StatusCode, Is.EqualTo(expectedStatusCode));

                break;
            default:
                Assert.Fail("Unanticipated expectedStatusCode. Check test case for typo.");
                break;
        }
    }

    [Test]
    [TestCase(new[] { Scopes.UpdateTranslationEngines }, 200, ECHO_ENGINE1_ID)]
    [TestCase(new[] { Scopes.UpdateTranslationEngines }, 404, DOES_NOT_EXIST_ENGINE_ID, false)]
    // [TestCase(new[] { Scopes.UpdateTranslationEngines }, 404, ECHO_ENGINE1_ID, false)] currently no-op when no build
    public async Task CancelCurrentBuildForEngineByIdAsync(
        IEnumerable<string> scope,
        int expectedStatusCode,
        string engineId,
        bool addBuild = false
    )
    {
        ITranslationEnginesClient client = _env!.CreateClient(scope);
        ServalApiException? ex = null;
        Build? build = null;
        if (addBuild)
        {
            build = new Build { EngineRef = engineId };
            await _env.Builds.InsertAsync(build);
        }

        switch (expectedStatusCode)
        {
            case 200:
                Assert.DoesNotThrowAsync(async () =>
                {
                    await client.CancelBuildAsync(engineId);
                });
                break;
            case 404:
                ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.CancelBuildAsync(engineId);
                });
                Assert.That(ex!.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
            default:
                Assert.Fail("Unanticipated expectedStatusCode. Check test case for typo.");
                break;
        }
    }

    [TearDown]
    public void TearDown()
    {
        _env!.Dispose();
    }

    private class TestEnvironment : DisposableBase
    {
        private readonly IServiceScope _scope;
        private readonly IMongoClient _mongoClient;

        public TestEnvironment()
        {
            _mongoClient = new MongoClient();
            ResetDatabases();

            Factory = new ServalWebApplicationFactory();
            _scope = Factory.Services.CreateScope();
            Engines = _scope.ServiceProvider.GetRequiredService<IRepository<Engine>>();
            DataFiles = _scope.ServiceProvider.GetRequiredService<IRepository<DataFiles.Models.DataFile>>();
            Pretranslations = _scope.ServiceProvider.GetRequiredService<
                IRepository<Serval.Translation.Models.Pretranslation>
            >();
            Builds = _scope.ServiceProvider.GetRequiredService<IRepository<Serval.Translation.Models.Build>>();
        }

        ServalWebApplicationFactory Factory { get; }
        public IRepository<Engine> Engines { get; }
        public IRepository<DataFiles.Models.DataFile> DataFiles { get; }
        public IRepository<Serval.Translation.Models.Pretranslation> Pretranslations { get; }
        public IRepository<Serval.Translation.Models.Build> Builds { get; }

        public TranslationEnginesClient CreateClient(
            IEnumerable<string>? scope = null,
            bool delayedBuildService = false
        )
        {
            if (scope is null)
            {
                scope = new[]
                {
                    Scopes.CreateTranslationEngines,
                    Scopes.ReadTranslationEngines,
                    Scopes.UpdateTranslationEngines,
                    Scopes.DeleteTranslationEngines
                };
            }
            var httpClient = Factory
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureTestServices(services =>
                    {
                        var echo_client = Substitute.For<TranslationEngineApi.TranslationEngineApiClient>();
                        echo_client
                            .CreateAsync(Arg.Any<CreateRequest>(), null, null, Arg.Any<CancellationToken>())
                            .Returns(CreateAsyncUnaryCall(new Empty()));
                        echo_client
                            .DeleteAsync(Arg.Any<DeleteRequest>(), null, null, Arg.Any<CancellationToken>())
                            .Returns(CreateAsyncUnaryCall(new Empty()));
                        echo_client
                            .StartBuildAsync(Arg.Any<StartBuildRequest>(), null, null, Arg.Any<CancellationToken>())
                            .Returns(CreateAsyncUnaryCall(new Empty()));
                        echo_client
                            .CancelBuildAsync(Arg.Any<CancelBuildRequest>(), null, null, Arg.Any<CancellationToken>())
                            .Returns(CreateAsyncUnaryCall(new Empty()));
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
                        echo_client
                            .TrainSegmentPairAsync(
                                Arg.Any<TrainSegmentPairRequest>(),
                                null,
                                null,
                                Arg.Any<CancellationToken>()
                            )
                            .Returns(CreateAsyncUnaryCall(new Empty()));
                        echo_client
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
                        echo_client
                            .TranslateAsync(Arg.Any<TranslateRequest>(), null, null, Arg.Any<CancellationToken>())
                            .Returns(CreateAsyncUnaryCall(translateResponse));

                        var nmt_client = Substitute.For<TranslationEngineApi.TranslationEngineApiClient>();
                        nmt_client
                            .CreateAsync(Arg.Any<CreateRequest>(), null, null, Arg.Any<CancellationToken>())
                            .Returns(CreateAsyncUnaryCall(new Empty()));
                        nmt_client
                            .DeleteAsync(Arg.Any<DeleteRequest>(), null, null, Arg.Any<CancellationToken>())
                            .Returns(CreateAsyncUnaryCall(new Empty()));
                        nmt_client
                            .StartBuildAsync(Arg.Any<StartBuildRequest>(), null, null, Arg.Any<CancellationToken>())
                            .Returns(CreateAsyncUnaryCall(new Empty()));
                        nmt_client
                            .CancelBuildAsync(Arg.Any<CancelBuildRequest>(), null, null, Arg.Any<CancellationToken>())
                            .Returns(CreateAsyncUnaryCall(new Empty()));
                        nmt_client
                            .GetWordGraphAsync(Arg.Any<GetWordGraphRequest>(), null, null, Arg.Any<CancellationToken>())
                            .Returns(CreateAsyncUnaryCall(new GetWordGraphResponse(), true));
                        nmt_client
                            .TranslateAsync(Arg.Any<TranslateRequest>(), null, null, Arg.Any<CancellationToken>())
                            .Returns(CreateAsyncUnaryCall(new TranslateResponse(), true));

                        var grpcClientFactory = Substitute.For<GrpcClientFactory>();
                        grpcClientFactory
                            .CreateClient<TranslationEngineApi.TranslationEngineApiClient>("Echo")
                            .Returns(echo_client);
                        grpcClientFactory
                            .CreateClient<TranslationEngineApi.TranslationEngineApiClient>("Nmt")
                            .Returns(nmt_client);
                        services.AddSingleton(grpcClientFactory);
                    });
                })
                .CreateClient();
            httpClient.DefaultRequestHeaders.Add("Scope", string.Join(" ", scope));
            return new TranslationEnginesClient(httpClient);
        }

        public void ResetDatabases()
        {
            _mongoClient.DropDatabase("serval_test");
            _mongoClient.DropDatabase("serval_test_jobs");
        }

        private static AsyncUnaryCall<TResponse> CreateAsyncUnaryCall<TResponse>(
            TResponse response,
            bool raisesError = false //Not functional since doesn't seem to pass through middleware
        )
        {
            return new AsyncUnaryCall<TResponse>(
                Task.FromResult(response),
                Task.FromResult(new Metadata()),
                () => raisesError ? new Status(StatusCode.Unimplemented, "") : Status.DefaultSuccess,
                () => new Metadata(),
                () => { }
            );
        }

        protected override void DisposeManagedResources()
        {
            _scope.Dispose();
            Factory.Dispose();
            ResetDatabases();
        }
    }
}
