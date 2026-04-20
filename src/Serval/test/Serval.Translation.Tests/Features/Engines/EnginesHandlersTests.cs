namespace Serval.Translation.Features.Engines;

#pragma warning disable CS0612 // Type or member is obsolete

[TestFixture]
public class EnginesHandlersTests
{
    const string OWNER = "owner1";

    [Test]
    public void Translate_EngineDoesNotExist()
    {
        var env = new TestEnvironment();
        TranslateHandler handler = new(env.Engines, env.EngineServiceFactory);
        Assert.ThrowsAsync<EntityNotFoundException>(() =>
            handler.HandleAsync(new Translate(OWNER, "engine1", "esto es una prueba."))
        );
    }

    [Test]
    public async Task Translate_EngineExists()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineWithTextFilesAsync()).Id;
        TranslateHandler handler = new(env.Engines, env.EngineServiceFactory);
        TranslateResponse response = await handler.HandleAsync(new Translate(OWNER, engineId, "esto es una prueba."));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.IsAvailable, Is.True);
            Assert.That(response.Results!.First().Translation, Is.EqualTo("this is a test."));
        }
    }

    [Test]
    public void GetWordGraph_EngineDoesNotExist()
    {
        var env = new TestEnvironment();
        GetWordGraphHandler handler = new(env.Engines, env.EngineServiceFactory);
        Assert.ThrowsAsync<EntityNotFoundException>(() =>
            handler.HandleAsync(new GetWordGraph(OWNER, "engine1", "esto es una prueba."))
        );
    }

    [Test]
    public async Task GetWordGraph_EngineExists()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineWithTextFilesAsync()).Id;
        GetWordGraphHandler handler = new(env.Engines, env.EngineServiceFactory);
        GetWordGraphResponse response = await handler.HandleAsync(
            new GetWordGraph(OWNER, engineId, "esto es una prueba.")
        );
        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.IsAvailable, Is.True);
            Assert.That(
                response.WordGraph!.Arcs.SelectMany(a => a.TargetTokens),
                Is.EqualTo("this is a test .".Split())
            );
        }
    }

    [Test]
    public void TrainSegment_EngineDoesNotExist()
    {
        var env = new TestEnvironment();
        TrainSegmentHandler handler = new(env.Engines, env.EngineServiceFactory);
        Assert.ThrowsAsync<EntityNotFoundException>(() =>
            handler.HandleAsync(
                new TrainSegment(
                    OWNER,
                    "engine1",
                    new SegmentPairDto
                    {
                        SourceSegment = "esto es una prueba.",
                        TargetSegment = "this is a test.",
                        SentenceStart = true,
                    }
                )
            )
        );
    }

    [Test]
    public async Task TrainSegment_EngineExists()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineWithTextFilesAsync()).Id;
        TrainSegmentHandler handler = new(env.Engines, env.EngineServiceFactory);
        Assert.DoesNotThrowAsync(() =>
            handler.HandleAsync(
                new TrainSegment(
                    OWNER,
                    engineId,
                    new SegmentPairDto
                    {
                        SourceSegment = "esto es una prueba.",
                        TargetSegment = "this is a test.",
                        SentenceStart = true,
                    }
                )
            )
        );
    }

    [Test]
    public async Task CreateEngine()
    {
        var env = new TestEnvironment();
        CreateEngineHandler handler = new(env.DataAccessContext, env.Engines, env.EngineServiceFactory, env.DtoMapper);
        CreateEngineResponse response = await handler.HandleAsync(
            new CreateEngine(
                OWNER,
                new TranslationEngineConfigDto
                {
                    SourceLanguage = "es",
                    TargetLanguage = "en",
                    Type = "Smt",
                }
            )
        );

        Engine? engine = await env.Engines.GetAsync(response.Engine.Id);
        Assert.That(engine, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(engine.SourceLanguage, Is.EqualTo("es"));
            Assert.That(engine.TargetLanguage, Is.EqualTo("en"));
        }
    }

    [Test]
    public async Task DeleteEngine_EngineExists()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineWithTextFilesAsync()).Id;
        DeleteEngineHandler handler = new(
            env.DataAccessContext,
            env.Engines,
            env.Builds,
            env.Pretranslations,
            env.EngineServiceFactory
        );
        await handler.HandleAsync(new DeleteEngine(OWNER, engineId));
        Engine? engine = await env.Engines.GetAsync(engineId);
        Assert.That(engine, Is.Null);
    }

    [Test]
    public async Task DeleteEngine_ProjectDoesNotExist()
    {
        var env = new TestEnvironment();
        await env.CreateEngineWithTextFilesAsync();
        DeleteEngineHandler handler = new(
            env.DataAccessContext,
            env.Engines,
            env.Builds,
            env.Pretranslations,
            env.EngineServiceFactory
        );
        Assert.ThrowsAsync<EntityNotFoundException>(() => handler.HandleAsync(new DeleteEngine(OWNER, "engine3")));
    }

    [Test]
    public async Task StartBuild_TrainOnNotSpecified()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineWithTextFilesAsync()).Id;
        StartBuildHandler handler = new(
            env.DataAccessContext,
            env.Engines,
            env.Builds,
            env.ContractMapper,
            env.EngineServiceFactory,
            Substitute.For<ILogger<StartBuildHandler>>(),
            env.DtoMapper,
            Substitute.For<IConfiguration>()
        );
        StartBuildResponse response = await handler.HandleAsync(
            new StartBuild(OWNER, engineId, new TranslationBuildConfigDto())
        );
        Assert.That(response.IsBuildRunning, Is.False);
        await env
            .TranslationEngineService.Received()
            .StartBuildAsync(
                engineId,
                response.Build!.Id,
                ArgEx.IsEquivalentTo<IReadOnlyList<ParallelCorpusContract>>([
                    new()
                    {
                        Id = "corpus1",
                        SourceCorpora =
                        [
                            new()
                            {
                                Id = "corpus1",
                                Language = "es",
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file1.txt",
                                        Format = FileFormat.Text,
                                        TextId = "text1",
                                    },
                                ],
                            },
                        ],
                        TargetCorpora =
                        [
                            new()
                            {
                                Id = "corpus1",
                                Language = "en",
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file2.txt",
                                        Format = FileFormat.Text,
                                        TextId = "text1",
                                    },
                                ],
                            },
                        ],
                    },
                ]),
                null,
                Arg.Any<CancellationToken>()
            );
    }

    [Test]
    public async Task StartBuild_TextIdsEmpty()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineWithTextFilesAsync()).Id;
        StartBuildHandler handler = new(
            env.DataAccessContext,
            env.Engines,
            env.Builds,
            env.ContractMapper,
            env.EngineServiceFactory,
            Substitute.For<ILogger<StartBuildHandler>>(),
            env.DtoMapper,
            Substitute.For<IConfiguration>()
        );
        StartBuildResponse response = await handler.HandleAsync(
            new StartBuild(
                OWNER,
                engineId,
                new TranslationBuildConfigDto
                {
                    TrainOn = [new TrainingCorpusConfigDto { CorpusId = "corpus1", TextIds = [] }],
                }
            )
        );
        Assert.That(response.IsBuildRunning, Is.False);
        await env
            .TranslationEngineService.Received()
            .StartBuildAsync(
                engineId,
                response.Build!.Id,
                ArgEx.IsEquivalentTo<IReadOnlyList<ParallelCorpusContract>>([
                    new()
                    {
                        Id = "corpus1",
                        SourceCorpora =
                        [
                            new()
                            {
                                Id = "corpus1",
                                Language = "es",
                                TrainOnTextIds = [],
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file1.txt",
                                        Format = FileFormat.Text,
                                        TextId = "text1",
                                    },
                                ],
                            },
                        ],
                        TargetCorpora =
                        [
                            new()
                            {
                                Id = "corpus1",
                                Language = "en",
                                TrainOnTextIds = [],
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file2.txt",
                                        Format = FileFormat.Text,
                                        TextId = "text1",
                                    },
                                ],
                            },
                        ],
                    },
                ]),
                null,
                Arg.Any<CancellationToken>()
            );
    }

    [Test]
    public async Task StartBuild_TextIdsPopulated()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineWithTextFilesAsync()).Id;
        StartBuildHandler handler = new(
            env.DataAccessContext,
            env.Engines,
            env.Builds,
            env.ContractMapper,
            env.EngineServiceFactory,
            Substitute.For<ILogger<StartBuildHandler>>(),
            env.DtoMapper,
            Substitute.For<IConfiguration>()
        );
        StartBuildResponse response = await handler.HandleAsync(
            new StartBuild(
                OWNER,
                engineId,
                new TranslationBuildConfigDto
                {
                    TrainOn = [new TrainingCorpusConfigDto { CorpusId = "corpus1", TextIds = ["text1"] }],
                }
            )
        );
        Assert.That(response.IsBuildRunning, Is.False);
        await env
            .TranslationEngineService.Received()
            .StartBuildAsync(
                engineId,
                response.Build!.Id,
                ArgEx.IsEquivalentTo<IReadOnlyList<ParallelCorpusContract>>([
                    new()
                    {
                        Id = "corpus1",
                        SourceCorpora =
                        [
                            new()
                            {
                                Id = "corpus1",
                                Language = "es",
                                TrainOnTextIds = ["text1"],
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file1.txt",
                                        Format = FileFormat.Text,
                                        TextId = "text1",
                                    },
                                ],
                            },
                        ],
                        TargetCorpora =
                        [
                            new()
                            {
                                Id = "corpus1",
                                Language = "en",
                                TrainOnTextIds = ["text1"],
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file2.txt",
                                        Format = FileFormat.Text,
                                        TextId = "text1",
                                    },
                                ],
                            },
                        ],
                    },
                ]),
                null,
                Arg.Any<CancellationToken>()
            );
    }

    [Test]
    public async Task StartBuild_TextIdsNotSpecified()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineWithTextFilesAsync()).Id;
        StartBuildHandler handler = new(
            env.DataAccessContext,
            env.Engines,
            env.Builds,
            env.ContractMapper,
            env.EngineServiceFactory,
            Substitute.For<ILogger<StartBuildHandler>>(),
            env.DtoMapper,
            Substitute.For<IConfiguration>()
        );
        StartBuildResponse response = await handler.HandleAsync(
            new StartBuild(
                OWNER,
                engineId,
                new TranslationBuildConfigDto { TrainOn = [new TrainingCorpusConfigDto { CorpusId = "corpus1" }] }
            )
        );
        Assert.That(response.IsBuildRunning, Is.False);
        await env
            .TranslationEngineService.Received()
            .StartBuildAsync(
                engineId,
                response.Build!.Id,
                ArgEx.IsEquivalentTo<IReadOnlyList<ParallelCorpusContract>>([
                    new()
                    {
                        Id = "corpus1",
                        SourceCorpora =
                        [
                            new()
                            {
                                Id = "corpus1",
                                Language = "es",
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file1.txt",
                                        Format = FileFormat.Text,
                                        TextId = "text1",
                                    },
                                ],
                            },
                        ],
                        TargetCorpora =
                        [
                            new()
                            {
                                Id = "corpus1",
                                Language = "en",
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file2.txt",
                                        Format = FileFormat.Text,
                                        TextId = "text1",
                                    },
                                ],
                            },
                        ],
                    },
                ]),
                null,
                Arg.Any<CancellationToken>()
            );
    }

    [Test]
    public async Task StartBuild_OneOfMultipleCorpora()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateMultipleCorporaEngineWithTextFilesAsync()).Id;
        StartBuildHandler handler = new(
            env.DataAccessContext,
            env.Engines,
            env.Builds,
            env.ContractMapper,
            env.EngineServiceFactory,
            Substitute.For<ILogger<StartBuildHandler>>(),
            env.DtoMapper,
            Substitute.For<IConfiguration>()
        );
        StartBuildResponse response = await handler.HandleAsync(
            new StartBuild(
                OWNER,
                engineId,
                new TranslationBuildConfigDto
                {
                    TrainOn = [new TrainingCorpusConfigDto { CorpusId = "corpus1" }],
                    Pretranslate = [new PretranslateCorpusConfigDto { CorpusId = "corpus1" }],
                }
            )
        );
        Assert.That(response.IsBuildRunning, Is.False);
        await env
            .TranslationEngineService.Received()
            .StartBuildAsync(
                engineId,
                response.Build!.Id,
                ArgEx.IsEquivalentTo<IReadOnlyList<ParallelCorpusContract>>([
                    new()
                    {
                        Id = "corpus1",
                        SourceCorpora =
                        [
                            new()
                            {
                                Id = "corpus1",
                                Language = "es",
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file1.txt",
                                        Format = FileFormat.Text,
                                        TextId = "text1",
                                    },
                                ],
                            },
                        ],
                        TargetCorpora =
                        [
                            new()
                            {
                                Id = "corpus1",
                                Language = "en",
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file2.txt",
                                        Format = FileFormat.Text,
                                        TextId = "text1",
                                    },
                                ],
                            },
                        ],
                    },
                ]),
                null,
                Arg.Any<CancellationToken>()
            );
    }

    [Test]
    public async Task StartBuild_TrainOnOnePretranslateTheOther()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateMultipleCorporaEngineWithTextFilesAsync()).Id;
        StartBuildHandler handler = new(
            env.DataAccessContext,
            env.Engines,
            env.Builds,
            env.ContractMapper,
            env.EngineServiceFactory,
            Substitute.For<ILogger<StartBuildHandler>>(),
            env.DtoMapper,
            Substitute.For<IConfiguration>()
        );
        StartBuildResponse response = await handler.HandleAsync(
            new StartBuild(
                OWNER,
                engineId,
                new TranslationBuildConfigDto
                {
                    TrainOn = [new TrainingCorpusConfigDto { CorpusId = "corpus1" }],
                    Pretranslate = [new PretranslateCorpusConfigDto { CorpusId = "corpus2" }],
                }
            )
        );
        Assert.That(response.IsBuildRunning, Is.False);
        await env
            .TranslationEngineService.Received()
            .StartBuildAsync(
                engineId,
                response.Build!.Id,
                ArgEx.IsEquivalentTo<IReadOnlyList<ParallelCorpusContract>>([
                    new()
                    {
                        Id = "corpus1",
                        SourceCorpora =
                        [
                            new()
                            {
                                Id = "corpus1",
                                Language = "es",
                                InferenceTextIds = [],
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file1.txt",
                                        Format = FileFormat.Text,
                                        TextId = "text1",
                                    },
                                ],
                            },
                        ],
                        TargetCorpora =
                        [
                            new()
                            {
                                Id = "corpus1",
                                Language = "en",
                                InferenceTextIds = [],
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file2.txt",
                                        Format = FileFormat.Text,
                                        TextId = "text1",
                                    },
                                ],
                            },
                        ],
                    },
                    new()
                    {
                        Id = "corpus2",
                        SourceCorpora =
                        [
                            new()
                            {
                                Id = "corpus2",
                                Language = "es",
                                TrainOnTextIds = [],
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file3.txt",
                                        Format = FileFormat.Text,
                                        TextId = "text1",
                                    },
                                ],
                            },
                        ],
                        TargetCorpora =
                        [
                            new()
                            {
                                Id = "corpus2",
                                Language = "en",
                                TrainOnTextIds = [],
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file4.txt",
                                        Format = FileFormat.Text,
                                        TextId = "text1",
                                    },
                                ],
                            },
                        ],
                    },
                ]),
                null,
                Arg.Any<CancellationToken>()
            );
    }

    [Test]
    public async Task StartBuild_TextFilesScriptureRangeSpecified()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineWithTextFilesAsync()).Id;
        StartBuildHandler handler = new(
            env.DataAccessContext,
            env.Engines,
            env.Builds,
            env.ContractMapper,
            env.EngineServiceFactory,
            Substitute.For<ILogger<StartBuildHandler>>(),
            env.DtoMapper,
            Substitute.For<IConfiguration>()
        );
        Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(
                new StartBuild(
                    OWNER,
                    engineId,
                    new TranslationBuildConfigDto
                    {
                        TrainOn = [new TrainingCorpusConfigDto { CorpusId = "corpus1", ScriptureRange = "MAT" }],
                    }
                )
            )
        );
    }

    [Test]
    public async Task StartBuild_ScriptureRangeSpecified()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineWithParatextProjectAsync()).Id;
        StartBuildHandler handler = new(
            env.DataAccessContext,
            env.Engines,
            env.Builds,
            env.ContractMapper,
            env.EngineServiceFactory,
            Substitute.For<ILogger<StartBuildHandler>>(),
            env.DtoMapper,
            Substitute.For<IConfiguration>()
        );
        StartBuildResponse response = await handler.HandleAsync(
            new StartBuild(
                OWNER,
                engineId,
                new TranslationBuildConfigDto
                {
                    TrainOn = [new TrainingCorpusConfigDto { CorpusId = "corpus1", ScriptureRange = "MAT 1;MRK" }],
                }
            )
        );
        Assert.That(response.IsBuildRunning, Is.False);
        await env
            .TranslationEngineService.Received()
            .StartBuildAsync(
                engineId,
                response.Build!.Id,
                ArgEx.IsEquivalentTo<IReadOnlyList<ParallelCorpusContract>>([
                    new()
                    {
                        Id = "corpus1",
                        SourceCorpora =
                        [
                            new()
                            {
                                Id = "corpus1",
                                Language = "es",
                                TrainOnChapters = new() { ["MAT"] = [1], ["MRK"] = [] },
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file1.zip",
                                        Format = FileFormat.Paratext,
                                        TextId = "file1.zip",
                                    },
                                ],
                            },
                        ],
                        TargetCorpora =
                        [
                            new()
                            {
                                Id = "corpus1",
                                Language = "en",
                                TrainOnChapters = new() { ["MAT"] = [1], ["MRK"] = [] },
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file2.zip",
                                        Format = FileFormat.Paratext,
                                        TextId = "file2.zip",
                                    },
                                ],
                            },
                        ],
                    },
                ]),
                null,
                Arg.Any<CancellationToken>()
            );
    }

    [Test]
    public async Task StartBuild_ScriptureRangeEmptyString()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineWithParatextProjectAsync()).Id;
        StartBuildHandler handler = new(
            env.DataAccessContext,
            env.Engines,
            env.Builds,
            env.ContractMapper,
            env.EngineServiceFactory,
            Substitute.For<ILogger<StartBuildHandler>>(),
            env.DtoMapper,
            Substitute.For<IConfiguration>()
        );
        StartBuildResponse response = await handler.HandleAsync(
            new StartBuild(
                OWNER,
                engineId,
                new TranslationBuildConfigDto
                {
                    TrainOn = [new TrainingCorpusConfigDto { CorpusId = "corpus1", ScriptureRange = "" }],
                }
            )
        );
        Assert.That(response.IsBuildRunning, Is.False);
        await env
            .TranslationEngineService.Received()
            .StartBuildAsync(
                engineId,
                response.Build!.Id,
                ArgEx.IsEquivalentTo<IReadOnlyList<ParallelCorpusContract>>([
                    new()
                    {
                        Id = "corpus1",
                        SourceCorpora =
                        [
                            new()
                            {
                                Id = "corpus1",
                                Language = "es",
                                TrainOnChapters = [],
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file1.zip",
                                        Format = FileFormat.Paratext,
                                        TextId = "file1.zip",
                                    },
                                ],
                            },
                        ],
                        TargetCorpora =
                        [
                            new()
                            {
                                Id = "corpus1",
                                Language = "en",
                                TrainOnChapters = [],
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file2.zip",
                                        Format = FileFormat.Paratext,
                                        TextId = "file2.zip",
                                    },
                                ],
                            },
                        ],
                    },
                ]),
                null,
                Arg.Any<CancellationToken>()
            );
    }

    [Test]
    public async Task StartBuild_ParallelCorpus_TextFiles()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateParallelCorpusEngineWithTextFilesAsync()).Id;
        StartBuildHandler handler = new(
            env.DataAccessContext,
            env.Engines,
            env.Builds,
            env.ContractMapper,
            env.EngineServiceFactory,
            Substitute.For<ILogger<StartBuildHandler>>(),
            env.DtoMapper,
            Substitute.For<IConfiguration>()
        );
        StartBuildResponse response = await handler.HandleAsync(
            new StartBuild(
                OWNER,
                engineId,
                new TranslationBuildConfigDto
                {
                    TrainOn =
                    [
                        new TrainingCorpusConfigDto
                        {
                            ParallelCorpusId = "parallel-corpus1",
                            SourceFilters = [new() { CorpusId = "parallel-corpus1-source1", TextIds = ["MAT"] }],
                            TargetFilters = [new() { CorpusId = "parallel-corpus1-target1", TextIds = ["MAT"] }],
                        },
                    ],
                }
            )
        );
        Assert.That(response.IsBuildRunning, Is.False);
        await env
            .TranslationEngineService.Received()
            .StartBuildAsync(
                engineId,
                response.Build!.Id,
                ArgEx.IsEquivalentTo<IReadOnlyList<ParallelCorpusContract>>([
                    new()
                    {
                        Id = "parallel-corpus1",
                        SourceCorpora =
                        [
                            new()
                            {
                                Id = "parallel-corpus1-source1",
                                Language = "es",
                                TrainOnTextIds = ["MAT"],
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file1.txt",
                                        Format = FileFormat.Text,
                                        TextId = "MAT",
                                    },
                                ],
                            },
                            new()
                            {
                                Id = "parallel-corpus1-source2",
                                Language = "es",
                                TrainOnTextIds = [],
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file3.txt",
                                        Format = FileFormat.Text,
                                        TextId = "MRK",
                                    },
                                ],
                            },
                        ],
                        TargetCorpora =
                        [
                            new()
                            {
                                Id = "parallel-corpus1-target1",
                                Language = "en",
                                TrainOnTextIds = ["MAT"],
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file2.txt",
                                        Format = FileFormat.Text,
                                        TextId = "MAT",
                                    },
                                ],
                            },
                            new()
                            {
                                Id = "parallel-corpus1-target2",
                                Language = "en",
                                TrainOnTextIds = [],
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file4.txt",
                                        Format = FileFormat.Text,
                                        TextId = "MRK",
                                    },
                                ],
                            },
                        ],
                    },
                ]),
                null,
                Arg.Any<CancellationToken>()
            );
    }

    [Test]
    public async Task StartBuild_ParallelCorpus_OneOfMultipleCorpora()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateMultipleParallelCorpusEngineWithTextFilesAsync()).Id;
        StartBuildHandler handler = new(
            env.DataAccessContext,
            env.Engines,
            env.Builds,
            env.ContractMapper,
            env.EngineServiceFactory,
            Substitute.For<ILogger<StartBuildHandler>>(),
            env.DtoMapper,
            Substitute.For<IConfiguration>()
        );
        StartBuildResponse response = await handler.HandleAsync(
            new StartBuild(
                OWNER,
                engineId,
                new TranslationBuildConfigDto
                {
                    TrainOn =
                    [
                        new TrainingCorpusConfigDto
                        {
                            ParallelCorpusId = "parallel-corpus1",
                            SourceFilters = [new() { CorpusId = "parallel-corpus1-source1", TextIds = ["MAT"] }],
                            TargetFilters = [new() { CorpusId = "parallel-corpus1-target1", TextIds = ["MAT"] }],
                        },
                    ],
                    Pretranslate = [new PretranslateCorpusConfigDto { ParallelCorpusId = "parallel-corpus1" }],
                }
            )
        );
        Assert.That(response.IsBuildRunning, Is.False);
        await env
            .TranslationEngineService.Received()
            .StartBuildAsync(
                engineId,
                response.Build!.Id,
                ArgEx.IsEquivalentTo<IReadOnlyList<ParallelCorpusContract>>([
                    new()
                    {
                        Id = "parallel-corpus1",
                        SourceCorpora =
                        [
                            new()
                            {
                                Id = "parallel-corpus1-source1",
                                Language = "es",
                                TrainOnTextIds = ["MAT"],
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file1.txt",
                                        Format = FileFormat.Text,
                                        TextId = "MAT",
                                    },
                                ],
                            },
                        ],
                        TargetCorpora =
                        [
                            new()
                            {
                                Id = "parallel-corpus1-target1",
                                Language = "en",
                                TrainOnTextIds = ["MAT"],
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file2.txt",
                                        Format = FileFormat.Text,
                                        TextId = "MAT",
                                    },
                                ],
                            },
                        ],
                    },
                ]),
                null,
                Arg.Any<CancellationToken>()
            );
    }

    [Test]
    public async Task StartBuild_ParallelCorpus_TrainOnOnePretranslateTheOther()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateMultipleParallelCorpusEngineWithTextFilesAsync()).Id;
        StartBuildHandler handler = new(
            env.DataAccessContext,
            env.Engines,
            env.Builds,
            env.ContractMapper,
            env.EngineServiceFactory,
            Substitute.For<ILogger<StartBuildHandler>>(),
            env.DtoMapper,
            Substitute.For<IConfiguration>()
        );
        StartBuildResponse response = await handler.HandleAsync(
            new StartBuild(
                OWNER,
                engineId,
                new TranslationBuildConfigDto
                {
                    TrainOn =
                    [
                        new TrainingCorpusConfigDto
                        {
                            ParallelCorpusId = "parallel-corpus1",
                            SourceFilters = [new() { CorpusId = "parallel-corpus1-source1", TextIds = ["MAT"] }],
                            TargetFilters = [new() { CorpusId = "parallel-corpus1-target1", TextIds = ["MAT"] }],
                        },
                    ],
                    Pretranslate = [new PretranslateCorpusConfigDto { ParallelCorpusId = "parallel-corpus2" }],
                }
            )
        );
        Assert.That(response.IsBuildRunning, Is.False);
        await env
            .TranslationEngineService.Received()
            .StartBuildAsync(
                engineId,
                response.Build!.Id,
                ArgEx.IsEquivalentTo<IReadOnlyList<ParallelCorpusContract>>([
                    new()
                    {
                        Id = "parallel-corpus1",
                        SourceCorpora =
                        [
                            new()
                            {
                                Id = "parallel-corpus1-source1",
                                Language = "es",
                                TrainOnTextIds = ["MAT"],
                                InferenceTextIds = [],
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file1.txt",
                                        Format = FileFormat.Text,
                                        TextId = "MAT",
                                    },
                                ],
                            },
                        ],
                        TargetCorpora =
                        [
                            new()
                            {
                                Id = "parallel-corpus1-target1",
                                Language = "en",
                                TrainOnTextIds = ["MAT"],
                                InferenceTextIds = [],
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file2.txt",
                                        Format = FileFormat.Text,
                                        TextId = "MAT",
                                    },
                                ],
                            },
                        ],
                    },
                    new()
                    {
                        Id = "parallel-corpus2",
                        SourceCorpora =
                        [
                            new()
                            {
                                Id = "parallel-corpus2-source1",
                                Language = "es",
                                TrainOnTextIds = [],
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file3.txt",
                                        Format = FileFormat.Text,
                                        TextId = "MRK",
                                    },
                                ],
                            },
                        ],
                        TargetCorpora =
                        [
                            new()
                            {
                                Id = "parallel-corpus2-target1",
                                Language = "en",
                                TrainOnTextIds = [],
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file4.txt",
                                        Format = FileFormat.Text,
                                        TextId = "MRK",
                                    },
                                ],
                            },
                        ],
                    },
                ]),
                null,
                Arg.Any<CancellationToken>()
            );
    }

    [Test]
    public async Task StartBuild_TextIds_ParallelCorpus()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateParallelCorpusEngineWithParatextProjectAsync()).Id;
        StartBuildHandler handler = new(
            env.DataAccessContext,
            env.Engines,
            env.Builds,
            env.ContractMapper,
            env.EngineServiceFactory,
            Substitute.For<ILogger<StartBuildHandler>>(),
            env.DtoMapper,
            Substitute.For<IConfiguration>()
        );
        StartBuildResponse response = await handler.HandleAsync(
            new StartBuild(
                OWNER,
                engineId,
                new TranslationBuildConfigDto
                {
                    TrainOn =
                    [
                        new TrainingCorpusConfigDto
                        {
                            ParallelCorpusId = "parallel-corpus1",
                            SourceFilters = [new() { CorpusId = "parallel-corpus1-source1", TextIds = ["MAT", "MRK"] }],
                            TargetFilters = [new() { CorpusId = "parallel-corpus1-target1", TextIds = ["MAT", "MRK"] }],
                        },
                    ],
                }
            )
        );
        Assert.That(response.IsBuildRunning, Is.False);
        await env
            .TranslationEngineService.Received()
            .StartBuildAsync(
                engineId,
                response.Build!.Id,
                ArgEx.IsEquivalentTo<IReadOnlyList<ParallelCorpusContract>>([
                    new()
                    {
                        Id = "parallel-corpus1",
                        SourceCorpora =
                        [
                            new()
                            {
                                Id = "parallel-corpus1-source1",
                                Language = "es",
                                TrainOnTextIds = ["MAT", "MRK"],
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file1.zip",
                                        Format = FileFormat.Paratext,
                                        TextId = "file1.zip",
                                    },
                                ],
                            },
                            new()
                            {
                                Id = "parallel-corpus1-source2",
                                Language = "es",
                                TrainOnTextIds = [],
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file3.zip",
                                        Format = FileFormat.Paratext,
                                        TextId = "file3.zip",
                                    },
                                ],
                            },
                        ],
                        TargetCorpora =
                        [
                            new()
                            {
                                Id = "parallel-corpus1-target1",
                                Language = "en",
                                TrainOnTextIds = ["MAT", "MRK"],
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file2.zip",
                                        Format = FileFormat.Paratext,
                                        TextId = "file2.zip",
                                    },
                                ],
                            },
                            new()
                            {
                                Id = "parallel-corpus1-target2",
                                Language = "en",
                                TrainOnTextIds = [],
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file4.zip",
                                        Format = FileFormat.Paratext,
                                        TextId = "file4.zip",
                                    },
                                ],
                            },
                        ],
                    },
                ]),
                null,
                Arg.Any<CancellationToken>()
            );
    }

    [Test]
    public async Task StartBuild_ScriptureRange_ParallelCorpus()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateParallelCorpusEngineWithParatextProjectAsync()).Id;
        StartBuildHandler handler = new(
            env.DataAccessContext,
            env.Engines,
            env.Builds,
            env.ContractMapper,
            env.EngineServiceFactory,
            Substitute.For<ILogger<StartBuildHandler>>(),
            env.DtoMapper,
            Substitute.For<IConfiguration>()
        );
        StartBuildResponse response = await handler.HandleAsync(
            new StartBuild(
                OWNER,
                engineId,
                new TranslationBuildConfigDto
                {
                    TrainOn =
                    [
                        new TrainingCorpusConfigDto
                        {
                            ParallelCorpusId = "parallel-corpus1",
                            SourceFilters =
                            [
                                new() { CorpusId = "parallel-corpus1-source1", ScriptureRange = "MAT 1;MRK" },
                            ],
                            TargetFilters =
                            [
                                new() { CorpusId = "parallel-corpus1-target1", ScriptureRange = "MAT 1;MRK" },
                            ],
                        },
                    ],
                    Pretranslate =
                    [
                        new PretranslateCorpusConfigDto
                        {
                            ParallelCorpusId = "parallel-corpus1",
                            SourceFilters = [new() { CorpusId = "parallel-corpus1-source1", ScriptureRange = "MAT 2" }],
                        },
                    ],
                }
            )
        );
        Assert.That(response.IsBuildRunning, Is.False);
        await env
            .TranslationEngineService.Received()
            .StartBuildAsync(
                engineId,
                response.Build!.Id,
                ArgEx.IsEquivalentTo<IReadOnlyList<ParallelCorpusContract>>([
                    new()
                    {
                        Id = "parallel-corpus1",
                        SourceCorpora =
                        [
                            new()
                            {
                                Id = "parallel-corpus1-source1",
                                Language = "es",
                                TrainOnChapters = new() { ["MAT"] = [1], ["MRK"] = [] },
                                InferenceChapters = new() { ["MAT"] = [2] },
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file1.zip",
                                        Format = FileFormat.Paratext,
                                        TextId = "file1.zip",
                                    },
                                ],
                            },
                            new()
                            {
                                Id = "parallel-corpus1-source2",
                                Language = "es",
                                TrainOnTextIds = [],
                                InferenceTextIds = [],
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file3.zip",
                                        Format = FileFormat.Paratext,
                                        TextId = "file3.zip",
                                    },
                                ],
                            },
                        ],
                        TargetCorpora =
                        [
                            new()
                            {
                                Id = "parallel-corpus1-target1",
                                Language = "en",
                                TrainOnChapters = new() { ["MAT"] = [1], ["MRK"] = [] },
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file2.zip",
                                        Format = FileFormat.Paratext,
                                        TextId = "file2.zip",
                                    },
                                ],
                            },
                            new()
                            {
                                Id = "parallel-corpus1-target2",
                                Language = "en",
                                TrainOnTextIds = [],
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file4.zip",
                                        Format = FileFormat.Paratext,
                                        TextId = "file4.zip",
                                    },
                                ],
                            },
                        ],
                    },
                ]),
                null,
                Arg.Any<CancellationToken>()
            );
    }

    [Test]
    public async Task StartBuild_MixedSourceAndTarget_ParallelCorpus()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateParallelCorpusEngineWithParatextProjectAsync()).Id;
        StartBuildHandler handler = new(
            env.DataAccessContext,
            env.Engines,
            env.Builds,
            env.ContractMapper,
            env.EngineServiceFactory,
            Substitute.For<ILogger<StartBuildHandler>>(),
            env.DtoMapper,
            Substitute.For<IConfiguration>()
        );
        StartBuildResponse response = await handler.HandleAsync(
            new StartBuild(
                OWNER,
                engineId,
                new TranslationBuildConfigDto
                {
                    TrainOn =
                    [
                        new TrainingCorpusConfigDto
                        {
                            ParallelCorpusId = "parallel-corpus1",
                            SourceFilters =
                            [
                                new() { CorpusId = "parallel-corpus1-source1", ScriptureRange = "MAT 1-2;MRK 1-2" },
                                new() { CorpusId = "parallel-corpus1-source2", ScriptureRange = "MAT 3;MRK 1" },
                            ],
                            TargetFilters =
                            [
                                new() { CorpusId = "parallel-corpus1-target1", ScriptureRange = "MAT 2-3;MRK 2" },
                                new() { CorpusId = "parallel-corpus1-target2", ScriptureRange = "MAT 1;MRK 1-2" },
                            ],
                        },
                    ],
                }
            )
        );
        Assert.That(response.IsBuildRunning, Is.False);
        await env
            .TranslationEngineService.Received()
            .StartBuildAsync(
                engineId,
                response.Build!.Id,
                ArgEx.IsEquivalentTo<IReadOnlyList<ParallelCorpusContract>>([
                    new()
                    {
                        Id = "parallel-corpus1",
                        SourceCorpora =
                        [
                            new()
                            {
                                Id = "parallel-corpus1-source1",
                                Language = "es",
                                TrainOnChapters = new() { ["MAT"] = [1, 2], ["MRK"] = [1, 2] },
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file1.zip",
                                        Format = FileFormat.Paratext,
                                        TextId = "file1.zip",
                                    },
                                ],
                            },
                            new()
                            {
                                Id = "parallel-corpus1-source2",
                                Language = "es",
                                TrainOnChapters = new() { ["MAT"] = [3], ["MRK"] = [1] },
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file3.zip",
                                        Format = FileFormat.Paratext,
                                        TextId = "file3.zip",
                                    },
                                ],
                            },
                        ],
                        TargetCorpora =
                        [
                            new()
                            {
                                Id = "parallel-corpus1-target1",
                                Language = "en",
                                TrainOnChapters = new() { ["MAT"] = [2, 3], ["MRK"] = [2] },
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file2.zip",
                                        Format = FileFormat.Paratext,
                                        TextId = "file2.zip",
                                    },
                                ],
                            },
                            new()
                            {
                                Id = "parallel-corpus1-target2",
                                Language = "en",
                                TrainOnChapters = new() { ["MAT"] = [1], ["MRK"] = [1, 2] },
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file4.zip",
                                        Format = FileFormat.Paratext,
                                        TextId = "file4.zip",
                                    },
                                ],
                            },
                        ],
                    },
                ]),
                null,
                Arg.Any<CancellationToken>()
            );
    }

    [Test]
    public async Task StartBuild_TextFilesScriptureRangeSpecified_ParallelCorpus()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateParallelCorpusEngineWithParatextProjectAsync()).Id;
        StartBuildHandler handler = new(
            env.DataAccessContext,
            env.Engines,
            env.Builds,
            env.ContractMapper,
            env.EngineServiceFactory,
            Substitute.For<ILogger<StartBuildHandler>>(),
            env.DtoMapper,
            Substitute.For<IConfiguration>()
        );
        Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(
                new StartBuild(
                    OWNER,
                    engineId,
                    new TranslationBuildConfigDto
                    {
                        TrainOn =
                        [
                            new TrainingCorpusConfigDto
                            {
                                ParallelCorpusId = "parallel-corpus1",
                                SourceFilters =
                                [
                                    new()
                                    {
                                        CorpusId = "parallel-corpus1-source1",
                                        ScriptureRange = "MAT",
                                        TextIds = [],
                                    },
                                ],
                                TargetFilters =
                                [
                                    new()
                                    {
                                        CorpusId = "parallel-corpus1-target1",
                                        ScriptureRange = "MAT",
                                        TextIds = [],
                                    },
                                ],
                            },
                        ],
                    }
                )
            )
        );
    }

    [Test]
    public async Task StartBuild_NoFilters_ParallelCorpus()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateParallelCorpusEngineWithParatextProjectAsync()).Id;
        StartBuildHandler handler = new(
            env.DataAccessContext,
            env.Engines,
            env.Builds,
            env.ContractMapper,
            env.EngineServiceFactory,
            Substitute.For<ILogger<StartBuildHandler>>(),
            env.DtoMapper,
            Substitute.For<IConfiguration>()
        );
        StartBuildResponse response = await handler.HandleAsync(
            new StartBuild(
                OWNER,
                engineId,
                new TranslationBuildConfigDto
                {
                    TrainOn = [new TrainingCorpusConfigDto { ParallelCorpusId = "parallel-corpus1" }],
                }
            )
        );
        Assert.That(response.IsBuildRunning, Is.False);
        await env
            .TranslationEngineService.Received()
            .StartBuildAsync(
                engineId,
                response.Build!.Id,
                ArgEx.IsEquivalentTo<IReadOnlyList<ParallelCorpusContract>>([
                    new()
                    {
                        Id = "parallel-corpus1",
                        SourceCorpora =
                        [
                            new()
                            {
                                Id = "parallel-corpus1-source1",
                                Language = "es",
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file1.zip",
                                        Format = FileFormat.Paratext,
                                        TextId = "file1.zip",
                                    },
                                ],
                            },
                            new()
                            {
                                Id = "parallel-corpus1-source2",
                                Language = "es",
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file3.zip",
                                        Format = FileFormat.Paratext,
                                        TextId = "file3.zip",
                                    },
                                ],
                            },
                        ],
                        TargetCorpora =
                        [
                            new()
                            {
                                Id = "parallel-corpus1-target1",
                                Language = "en",
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file2.zip",
                                        Format = FileFormat.Paratext,
                                        TextId = "file2.zip",
                                    },
                                ],
                            },
                            new()
                            {
                                Id = "parallel-corpus1-target2",
                                Language = "en",
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file4.zip",
                                        Format = FileFormat.Paratext,
                                        TextId = "file4.zip",
                                    },
                                ],
                            },
                        ],
                    },
                ]),
                null,
                Arg.Any<CancellationToken>()
            );
    }

    [Test]
    public async Task StartBuild_TrainOnNotSpecified_ParallelCorpus()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateParallelCorpusEngineWithParatextProjectAsync()).Id;
        StartBuildHandler handler = new(
            env.DataAccessContext,
            env.Engines,
            env.Builds,
            env.ContractMapper,
            env.EngineServiceFactory,
            Substitute.For<ILogger<StartBuildHandler>>(),
            env.DtoMapper,
            Substitute.For<IConfiguration>()
        );
        StartBuildResponse response = await handler.HandleAsync(
            new StartBuild(OWNER, engineId, new TranslationBuildConfigDto())
        );
        Assert.That(response.IsBuildRunning, Is.False);
        await env
            .TranslationEngineService.Received()
            .StartBuildAsync(
                engineId,
                response.Build!.Id,
                ArgEx.IsEquivalentTo<IReadOnlyList<ParallelCorpusContract>>([
                    new()
                    {
                        Id = "parallel-corpus1",
                        SourceCorpora =
                        [
                            new()
                            {
                                Id = "parallel-corpus1-source1",
                                Language = "es",
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file1.zip",
                                        Format = FileFormat.Paratext,
                                        TextId = "file1.zip",
                                    },
                                ],
                            },
                            new()
                            {
                                Id = "parallel-corpus1-source2",
                                Language = "es",
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file3.zip",
                                        Format = FileFormat.Paratext,
                                        TextId = "file3.zip",
                                    },
                                ],
                            },
                        ],
                        TargetCorpora =
                        [
                            new()
                            {
                                Id = "parallel-corpus1-target1",
                                Language = "en",
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file2.zip",
                                        Format = FileFormat.Paratext,
                                        TextId = "file2.zip",
                                    },
                                ],
                            },
                            new()
                            {
                                Id = "parallel-corpus1-target2",
                                Language = "en",
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file4.zip",
                                        Format = FileFormat.Paratext,
                                        TextId = "file4.zip",
                                    },
                                ],
                            },
                        ],
                    },
                ]),
                null,
                Arg.Any<CancellationToken>()
            );
    }

    [Test]
    public async Task StartBuild_NoTargetFilter_ParallelCorpus()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateParallelCorpusEngineWithParatextProjectAsync()).Id;
        StartBuildHandler handler = new(
            env.DataAccessContext,
            env.Engines,
            env.Builds,
            env.ContractMapper,
            env.EngineServiceFactory,
            Substitute.For<ILogger<StartBuildHandler>>(),
            env.DtoMapper,
            Substitute.For<IConfiguration>()
        );
        StartBuildResponse response = await handler.HandleAsync(
            new StartBuild(
                OWNER,
                engineId,
                new TranslationBuildConfigDto
                {
                    TrainOn =
                    [
                        new TrainingCorpusConfigDto
                        {
                            ParallelCorpusId = "parallel-corpus1",
                            SourceFilters =
                            [
                                new() { CorpusId = "parallel-corpus1-source1", ScriptureRange = "MAT 1;MRK" },
                            ],
                        },
                    ],
                }
            )
        );
        Assert.That(response.IsBuildRunning, Is.False);
        await env
            .TranslationEngineService.Received()
            .StartBuildAsync(
                engineId,
                response.Build!.Id,
                ArgEx.IsEquivalentTo<IReadOnlyList<ParallelCorpusContract>>([
                    new()
                    {
                        Id = "parallel-corpus1",
                        SourceCorpora =
                        [
                            new()
                            {
                                Id = "parallel-corpus1-source1",
                                Language = "es",
                                TrainOnChapters = new() { ["MAT"] = [1], ["MRK"] = [] },
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file1.zip",
                                        Format = FileFormat.Paratext,
                                        TextId = "file1.zip",
                                    },
                                ],
                            },
                            new()
                            {
                                Id = "parallel-corpus1-source2",
                                Language = "es",
                                TrainOnTextIds = [],
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file3.zip",
                                        Format = FileFormat.Paratext,
                                        TextId = "file3.zip",
                                    },
                                ],
                            },
                        ],
                        TargetCorpora =
                        [
                            new()
                            {
                                Id = "parallel-corpus1-target1",
                                Language = "en",
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file2.zip",
                                        Format = FileFormat.Paratext,
                                        TextId = "file2.zip",
                                    },
                                ],
                            },
                            new()
                            {
                                Id = "parallel-corpus1-target2",
                                Language = "en",
                                Files =
                                [
                                    new()
                                    {
                                        Location = "file4.zip",
                                        Format = FileFormat.Paratext,
                                        TextId = "file4.zip",
                                    },
                                ],
                            },
                        ],
                    },
                ]),
                null,
                Arg.Any<CancellationToken>()
            );
    }

    [Test]
    public async Task CancelBuildAsync_EngineExistsNotBuilding()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineWithTextFilesAsync()).Id;
        CancelBuildHandler handler = new(
            env.DataAccessContext,
            env.Engines,
            env.Builds,
            env.EngineServiceFactory,
            env.DtoMapper
        );
        await handler.HandleAsync(new CancelBuild(OWNER, engineId), CancellationToken.None);
    }

    [Test]
    public async Task GetBuild_WithNoMinRevision_ReturnsFoundBuild()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineWithTextFilesAsync()).Id;
        var build = new Build
        {
            Id = "build1",
            EngineRef = engineId,
            Owner = OWNER,
            Progress = 0.1,
        };
        await env.Builds.InsertAsync(build);

        GetBuildHandler handler = new(env.Engines, env.Builds, env.DtoMapper, env.ApiOptions);
        GetBuildResponse response = await handler.HandleAsync(new GetBuild(OWNER, engineId, build.Id, null));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.Status, Is.EqualTo(GetBuildStatus.Found));
            Assert.That(response.Build, Is.Not.Null);
            Assert.That(response.Build!.Revision, Is.EqualTo(1));
            Assert.That(response.Build.Progress, Is.EqualTo(0.1));
        }
    }

    [Test]
    public async Task GetBuild_WithMinRevision_Update_ReturnsFound()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineWithTextFilesAsync()).Id;
        var build = new Build
        {
            Id = "build1",
            EngineRef = engineId,
            Owner = OWNER,
        };
        await env.Builds.InsertAsync(build);

        GetBuildHandler handler = new(env.Engines, env.Builds, env.DtoMapper, env.ApiOptions);
        Task<GetBuildResponse> task = handler.HandleAsync(new GetBuild(OWNER, engineId, build.Id, 2));
        await env.Builds.UpdateAsync(build, u => u.Set(b => b.Progress, 0.1));

        GetBuildResponse response = await task;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.Status, Is.EqualTo(GetBuildStatus.Found));
            Assert.That(response.Build, Is.Not.Null);
            Assert.That(response.Build!.Revision, Is.EqualTo(2));
            Assert.That(response.Build.Progress, Is.EqualTo(0.1));
        }
    }

    [Test]
    public async Task GetBuild_WithMinRevision_Delete_ReturnsDeleted()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineWithTextFilesAsync()).Id;
        var build = new Build
        {
            Id = "build1",
            EngineRef = engineId,
            Owner = OWNER,
        };
        await env.Builds.InsertAsync(build);

        GetBuildHandler handler = new(env.Engines, env.Builds, env.DtoMapper, env.ApiOptions);
        Task<GetBuildResponse> task = handler.HandleAsync(new GetBuild(OWNER, engineId, build.Id, 2));
        await env.Builds.DeleteAsync(build);

        GetBuildResponse response = await task;
        Assert.That(response.Status, Is.EqualTo(GetBuildStatus.Deleted));
        Assert.That(response.Build, Is.Null);
    }

    [Test]
    public void GetBuild_BuildDoesNotExist_Throws()
    {
        var env = new TestEnvironment();
        GetBuildHandler handler = new(env.Engines, env.Builds, env.DtoMapper, env.ApiOptions);
        Assert.ThrowsAsync<EntityNotFoundException>(() =>
            handler.HandleAsync(new GetBuild(OWNER, "engine1", "build1", 2))
        );
    }

    [Test]
    public async Task UpdateCorpusAsync()
    {
        var env = new TestEnvironment();
        Engine engine = await env.CreateEngineWithTextFilesAsync();
        string corpusId = engine.Corpora[0].Id;

        var getDataFileHandler = Substitute.For<IRequestHandler<GetDataFile, GetDataFileResponse>>();
        getDataFileHandler
            .HandleAsync(Arg.Is<GetDataFile>(r => r.DataFileId == "file1"), Arg.Any<CancellationToken>())
            .Returns(
                new GetDataFileResponse(true, new DataFileContract("file1", "file1", "file1.txt", FileFormat.Text))
            );
        getDataFileHandler
            .HandleAsync(Arg.Is<GetDataFile>(r => r.DataFileId == "file3"), Arg.Any<CancellationToken>())
            .Returns(
                new GetDataFileResponse(true, new DataFileContract("file3", "file3", "file3.txt", FileFormat.Text))
            );

        var handler = new UpdateCorpusHandler(
            env.DataAccessContext,
            env.Engines,
            env.Pretranslations,
            getDataFileHandler,
            env.DtoMapper
        );
        UpdateCorpusResponse response = await handler.HandleAsync(
            new UpdateCorpus(
                OWNER,
                engine.Id,
                corpusId,
                new TranslationCorpusUpdateConfigDto
                {
                    SourceFiles =
                    [
                        new() { FileId = "file1", TextId = "text1" },
                        new() { FileId = "file3", TextId = "text2" },
                    ],
                }
            ),
            CancellationToken.None
        );

        TranslationCorpusDto corpus = response.Corpus;
        Assert.That(corpus, Is.Not.Null);
        Assert.That(corpus.SourceFiles, Has.Count.EqualTo(2));
        Assert.That(corpus.SourceFiles[0].File.Id, Is.EqualTo("file1"));
        Assert.That(corpus.SourceFiles[1].File.Id, Is.EqualTo("file3"));
        Assert.That(corpus.TargetFiles, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task UpdateEngine_ShouldUpdateLanguages_WhenRequestIsValid()
    {
        var env = new TestEnvironment();
        var engine = await env.CreateEngineWithTextFilesAsync();

        var request = new TranslationEngineUpdateConfigDto { SourceLanguage = "en", TargetLanguage = "fr" };

        UpdateEngineHandler handler = new(
            env.DataAccessContext,
            env.Engines,
            env.Pretranslations,
            env.EngineServiceFactory
        );
        await handler.HandleAsync(new UpdateEngine(OWNER, engine.Id, request), CancellationToken.None);

        engine = await env.Engines.GetAsync(engine.Id);

        Assert.That(engine, Is.Not.Null);
        Assert.That(engine.SourceLanguage, Is.Not.Null);
        Assert.That(engine.SourceLanguage, Is.EqualTo("en"));
        Assert.That(engine.TargetLanguage, Is.Not.Null);
        Assert.That(engine.TargetLanguage, Is.EqualTo("fr"));
    }

    [Test]
    public async Task UpdateEngine_ShouldNotUpdateSourceLanguage_WhenSourceLanguageNotProvided()
    {
        var env = new TestEnvironment();
        Engine engine = await env.CreateEngineWithTextFilesAsync();

        UpdateEngineHandler handler = new(
            env.DataAccessContext,
            env.Engines,
            env.Pretranslations,
            env.EngineServiceFactory
        );
        await handler.HandleAsync(
            new UpdateEngine(OWNER, engine.Id, new TranslationEngineUpdateConfigDto { TargetLanguage = "fr" }),
            CancellationToken.None
        );

        Engine? updatedEngine = await env.Engines.GetAsync(engine.Id);

        Assert.That(updatedEngine, Is.Not.Null);
        Assert.That(updatedEngine.SourceLanguage, Is.Not.Null);
        Assert.That(updatedEngine.SourceLanguage, Is.EqualTo(engine.SourceLanguage));
        Assert.That(updatedEngine.TargetLanguage, Is.Not.Null);
        Assert.That(updatedEngine.TargetLanguage, Is.EqualTo("fr"));
    }

    [Test]
    public async Task UpdateEngine_ShouldNotUpdateTargetLanguage_WhenTargetLanguageNotProvided()
    {
        var env = new TestEnvironment();
        Engine engine = await env.CreateEngineWithTextFilesAsync();

        UpdateEngineHandler handler = new(
            env.DataAccessContext,
            env.Engines,
            env.Pretranslations,
            env.EngineServiceFactory
        );
        await handler.HandleAsync(
            new UpdateEngine(OWNER, engine.Id, new TranslationEngineUpdateConfigDto { SourceLanguage = "en" }),
            CancellationToken.None
        );

        Engine? updatedEngine = await env.Engines.GetAsync(engine.Id);

        Assert.That(updatedEngine, Is.Not.Null);
        Assert.That(updatedEngine.SourceLanguage, Is.Not.Null);
        Assert.That(updatedEngine.SourceLanguage, Is.EqualTo("en"));
        Assert.That(updatedEngine.TargetLanguage, Is.Not.Null);
        Assert.That(updatedEngine.TargetLanguage, Is.EqualTo(engine.TargetLanguage));
    }

    [Test]
    public async Task UpdateEngine_ShouldNotUpdate_WhenSourceAndTargetLanguagesNotProvided()
    {
        var env = new TestEnvironment();
        Engine engine = await env.CreateEngineWithTextFilesAsync();

        UpdateEngineHandler handler = new(
            env.DataAccessContext,
            env.Engines,
            env.Pretranslations,
            env.EngineServiceFactory
        );
        await handler.HandleAsync(
            new UpdateEngine(OWNER, engine.Id, new TranslationEngineUpdateConfigDto()),
            CancellationToken.None
        );

        Engine? updatedEngine = await env.Engines.GetAsync(engine.Id);

        Assert.That(updatedEngine, Is.Not.Null);
        Assert.That(updatedEngine.SourceLanguage, Is.Not.Null);
        Assert.That(updatedEngine.SourceLanguage, Is.EqualTo(engine.SourceLanguage));
        Assert.That(updatedEngine.TargetLanguage, Is.Not.Null);
        Assert.That(updatedEngine.TargetLanguage, Is.EqualTo(engine.TargetLanguage));
    }

    [Test]
    public async Task DeletePretranslationsWhenCorpusIsUpdatedAsync()
    {
        var env = new TestEnvironment();
        Pretranslation pretranslation = new()
        {
            Id = "pretranslation1",
            EngineRef = "engine1",
            CorpusRef = "corpus1",
            Refs = ["ref1"],
            SourceRefs = ["ref1"],
            TargetRefs = ["ref1"],
            TextId = "textId1",
            Translation = "translation",
        };
        var engine = await env.CreateEngineWithTextFilesAsync();
        await env.Pretranslations.InsertAsync(pretranslation);
        Assert.That(await env.Pretranslations.GetAsync(pretranslation.Id), Is.Not.Null);

        var getDataFileHandler = Substitute.For<IRequestHandler<GetDataFile, GetDataFileResponse>>();
        var handler = new UpdateCorpusHandler(
            env.DataAccessContext,
            env.Engines,
            env.Pretranslations,
            getDataFileHandler,
            env.DtoMapper
        );
        await handler.HandleAsync(
            new UpdateCorpus(
                OWNER,
                engine.Id,
                "corpus1",
                new TranslationCorpusUpdateConfigDto { SourceFiles = [], TargetFiles = [] }
            ),
            CancellationToken.None
        );
        Assert.That(await env.Pretranslations.GetAsync(pretranslation.Id), Is.Null);
    }

    [Test]
    public async Task DeletePretranslationsWhenParallelCorpusIsUpdatedAsync()
    {
        var env = new TestEnvironment();
        Pretranslation pretranslation = new()
        {
            Id = "pretranslation1",
            EngineRef = "engine1",
            CorpusRef = "parallel-corpus1",
            Refs = ["ref1"],
            SourceRefs = ["ref1"],
            TargetRefs = ["ref1"],
            TextId = "textId1",
            Translation = "translation",
        };
        var engine = await env.CreateParallelCorpusEngineWithTextFilesAsync();
        await env.Pretranslations.InsertAsync(pretranslation);
        Assert.That(await env.Pretranslations.GetAsync(pretranslation.Id), Is.Not.Null);

        var getCorpusHandler = Substitute.For<IRequestHandler<GetCorpus, GetCorpusResponse>>();
        var handler = new UpdateParallelCorpusHandler(
            env.DataAccessContext,
            env.Engines,
            env.Pretranslations,
            getCorpusHandler,
            env.DtoMapper
        );
        await handler.HandleAsync(
            new UpdateParallelCorpus(
                OWNER,
                engine.Id,
                "parallel-corpus1",
                new TranslationParallelCorpusUpdateConfigDto()
            ),
            CancellationToken.None
        );
        Assert.That(await env.Pretranslations.GetAsync(pretranslation.Id), Is.Null);
    }

    [Test]
    public async Task DeletePretranslationsWhenCorpusFilesAreDeletedAsync()
    {
        var env = new TestEnvironment();
        Pretranslation pretranslation = new()
        {
            Id = "pretranslation1",
            EngineRef = "engine1",
            CorpusRef = "corpus1",
            Refs = ["ref1"],
            SourceRefs = ["ref1"],
            TargetRefs = ["ref1"],
            TextId = "textId1",
            Translation = "translation",
        };
        await env.CreateEngineWithTextFilesAsync();
        await env.Pretranslations.InsertAsync(pretranslation);
        Assert.That(await env.Pretranslations.GetAsync(pretranslation.Id), Is.Not.Null);
        var handler = new DataFileDeletedHandler(env.DataAccessContext, env.Engines, env.Pretranslations);
        await handler.HandleAsync(new DataFileDeleted("file1"), CancellationToken.None);
        Assert.That(await env.Pretranslations.GetAsync(pretranslation.Id), Is.Null);
    }

    [Test]
    public async Task DeletePretranslationsWhenCorpusFilesAreUpdatedAsync()
    {
        var env = new TestEnvironment();
        Pretranslation pretranslation = new()
        {
            Id = "pretranslation1",
            EngineRef = "engine1",
            CorpusRef = "corpus1",
            Refs = ["ref1"],
            SourceRefs = ["ref1"],
            TargetRefs = ["ref1"],
            TextId = "textId1",
            Translation = "translation",
        };
        await env.CreateEngineWithTextFilesAsync();
        await env.Pretranslations.InsertAsync(pretranslation);
        Assert.That(await env.Pretranslations.GetAsync(pretranslation.Id), Is.Not.Null);
        var handler = new CorpusUpdatedHandler(env.DataAccessContext, env.Engines, env.Pretranslations);
        await handler.HandleAsync(
            new CorpusUpdated(
                "corpus1",
                [new(new DataFileContract("file1", "file1", "newfilename", FileFormat.Text), "text1")]
            ),
            CancellationToken.None
        );
        Assert.That(await env.Pretranslations.GetAsync(pretranslation.Id), Is.Null);
    }

    private class TestEnvironment
    {
        public TestEnvironment()
        {
            Engines = new MemoryRepository<Engine>();
            TranslationEngineService = Substitute.For<ITranslationEngineService>();
            ApiOptions = Substitute.For<IOptionsMonitor<ApiOptions>>();
            ApiOptions.CurrentValue.Returns(new ApiOptions());

            var translationResult = new TranslationResultContract
            {
                Translation = "this is a test.",
                SourceTokens = ["esto", "es", "una", "prueba", "."],
                TargetTokens = ["this", "is", "a", "test", "."],
                Confidences = [1.0, 1.0, 1.0, 1.0, 1.0],
                Sources =
                [
                    new HashSet<TranslationSource> { TranslationSource.Primary },
                    new HashSet<TranslationSource> { TranslationSource.Primary },
                    new HashSet<TranslationSource> { TranslationSource.Primary },
                    new HashSet<TranslationSource> { TranslationSource.Primary },
                    new HashSet<TranslationSource> { TranslationSource.Primary },
                    new HashSet<TranslationSource> { TranslationSource.Primary },
                ],
                Alignment =
                [
                    new AlignedWordPairContract { SourceIndex = 0, TargetIndex = 0 },
                    new AlignedWordPairContract { SourceIndex = 1, TargetIndex = 1 },
                    new AlignedWordPairContract { SourceIndex = 2, TargetIndex = 2 },
                    new AlignedWordPairContract { SourceIndex = 3, TargetIndex = 3 },
                    new AlignedWordPairContract { SourceIndex = 4, TargetIndex = 4 },
                ],
                Phrases =
                [
                    new PhraseContract
                    {
                        SourceSegmentStart = 0,
                        SourceSegmentEnd = 5,
                        TargetSegmentCut = 5,
                    },
                ],
            };
            TranslationEngineService
                .TranslateAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyList<TranslationResultContract>>([translationResult]));

            var wordGraph = new WordGraphContract
            {
                SourceTokens = ["esto", "es", "una", "prueba", "."],
                InitialStateScore = 0.0,
                FinalStates = new HashSet<int> { 3 },
                Arcs =
                [
                    new WordGraphArcContract
                    {
                        PrevState = 0,
                        NextState = 1,
                        Score = 1.0,
                        TargetTokens = ["this", "is"],
                        Alignment =
                        [
                            new AlignedWordPairContract { SourceIndex = 0, TargetIndex = 0 },
                            new AlignedWordPairContract { SourceIndex = 1, TargetIndex = 1 },
                        ],
                        SourceSegmentStart = 0,
                        SourceSegmentEnd = 2,
                        Sources =
                        [
                            new HashSet<TranslationSource> { TranslationSource.Primary },
                            new HashSet<TranslationSource> { TranslationSource.Primary },
                        ],
                        Confidences = [1.0, 1.0],
                    },
                    new WordGraphArcContract
                    {
                        PrevState = 1,
                        NextState = 2,
                        Score = 1.0,
                        TargetTokens = ["a", "test"],
                        Alignment =
                        [
                            new AlignedWordPairContract { SourceIndex = 0, TargetIndex = 0 },
                            new AlignedWordPairContract { SourceIndex = 1, TargetIndex = 1 },
                        ],
                        SourceSegmentStart = 2,
                        SourceSegmentEnd = 4,
                        Sources =
                        [
                            new HashSet<TranslationSource> { TranslationSource.Primary },
                            new HashSet<TranslationSource> { TranslationSource.Primary },
                        ],
                        Confidences = [1.0, 1.0],
                    },
                    new WordGraphArcContract
                    {
                        PrevState = 2,
                        NextState = 3,
                        Score = 1.0,
                        TargetTokens = ["."],
                        Alignment = [new AlignedWordPairContract { SourceIndex = 0, TargetIndex = 0 }],
                        SourceSegmentStart = 4,
                        SourceSegmentEnd = 5,
                        Sources = [new HashSet<TranslationSource> { TranslationSource.Primary }],
                        Confidences = [1.0],
                    },
                ],
            };
            TranslationEngineService
                .GetWordGraphAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(wordGraph));
            TranslationEngineService
                .CancelBuildAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<string?>(null));
            TranslationEngineService
                .CreateAsync(
                    Arg.Any<string>(),
                    Arg.Any<string>(),
                    Arg.Any<string>(),
                    Arg.Any<string?>(),
                    Arg.Any<bool>(),
                    Arg.Any<CancellationToken>()
                )
                .Returns(Task.CompletedTask);
            TranslationEngineService
                .DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            TranslationEngineService
                .StartBuildAsync(
                    Arg.Any<string>(),
                    Arg.Any<string>(),
                    Arg.Any<IReadOnlyList<ParallelCorpusContract>>(),
                    Arg.Any<string?>(),
                    Arg.Any<CancellationToken>()
                )
                .Returns(Task.CompletedTask);
            TranslationEngineService
                .UpdateAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            TranslationEngineService
                .TrainSegmentPairAsync(
                    Arg.Any<string>(),
                    Arg.Any<string>(),
                    Arg.Any<string>(),
                    Arg.Any<bool>(),
                    Arg.Any<CancellationToken>()
                )
                .Returns(Task.CompletedTask);

            IOptionsMonitor<DataFileOptions> dataFileOptions = Substitute.For<IOptionsMonitor<DataFileOptions>>();
            dataFileOptions.CurrentValue.Returns(new DataFileOptions());

            Pretranslations = new MemoryRepository<Pretranslation>();
            Builds = new MemoryRepository<Build>();
            var parallelCorpusService = Substitute.For<IParallelCorpusService>();
            parallelCorpusService
                .GetChapters(Arg.Any<IReadOnlyList<ParallelCorpusContract>>(), Arg.Any<string>(), Arg.Any<string>())
                .Returns(callInfo =>
                {
                    return ScriptureRangeParser.GetChapters(callInfo.ArgAt<string>(2));
                });
            ContractMapper = new ContractMapper(dataFileOptions, parallelCorpusService);
            DataAccessContext = new MemoryDataAccessContext();
            EngineServiceFactory = Substitute.For<IEngineServiceFactory>();
            EngineServiceFactory
                .TryGetEngineService("Smt", out Arg.Any<ITranslationEngineService?>())
                .Returns(callInfo =>
                {
                    callInfo[1] = TranslationEngineService;
                    return true;
                });
            DtoMapper = new DtoMapper(Substitute.For<IUrlService>());
        }

        public IRepository<Engine> Engines { get; }
        public IRepository<Build> Builds { get; }
        public IRepository<Pretranslation> Pretranslations { get; }
        public ITranslationEngineService TranslationEngineService { get; }
        public IOptionsMonitor<ApiOptions> ApiOptions { get; }
        public ContractMapper ContractMapper { get; }
        public IEngineServiceFactory EngineServiceFactory { get; }
        public IDataAccessContext DataAccessContext { get; }
        public DtoMapper DtoMapper { get; }

        public async Task<Engine> CreateEngineWithTextFilesAsync()
        {
            var engine = new Engine
            {
                Id = "engine1",
                Owner = OWNER,
                SourceLanguage = "es",
                TargetLanguage = "en",
                Type = "Smt",
                Corpora =
                [
                    new()
                    {
                        Id = "corpus1",
                        SourceLanguage = "es",
                        TargetLanguage = "en",
                        SourceFiles =
                        [
                            new()
                            {
                                Id = "file1",
                                Filename = "file1.txt",
                                Format = FileFormat.Text,
                                TextId = "text1",
                            },
                        ],
                        TargetFiles =
                        [
                            new()
                            {
                                Id = "file2",
                                Filename = "file2.txt",
                                Format = FileFormat.Text,
                                TextId = "text1",
                            },
                        ],
                    },
                ],
                ModelRevision = 1,
            };
            await Engines.InsertAsync(engine);
            return engine;
        }

        public async Task<Engine> CreateMultipleCorporaEngineWithTextFilesAsync()
        {
            var engine = new Engine
            {
                Id = "engine1",
                Owner = OWNER,
                SourceLanguage = "es",
                TargetLanguage = "en",
                Type = "Smt",
                Corpora =
                [
                    new()
                    {
                        Id = "corpus1",
                        SourceLanguage = "es",
                        TargetLanguage = "en",
                        SourceFiles =
                        [
                            new()
                            {
                                Id = "file1",
                                Filename = "file1.txt",
                                Format = FileFormat.Text,
                                TextId = "text1",
                            },
                        ],
                        TargetFiles =
                        [
                            new()
                            {
                                Id = "file2",
                                Filename = "file2.txt",
                                Format = FileFormat.Text,
                                TextId = "text1",
                            },
                        ],
                    },
                    new()
                    {
                        Id = "corpus2",
                        SourceLanguage = "es",
                        TargetLanguage = "en",
                        SourceFiles =
                        [
                            new()
                            {
                                Id = "file3",
                                Filename = "file3.txt",
                                Format = FileFormat.Text,
                                TextId = "text1",
                            },
                        ],
                        TargetFiles =
                        [
                            new()
                            {
                                Id = "file4",
                                Filename = "file4.txt",
                                Format = FileFormat.Text,
                                TextId = "text1",
                            },
                        ],
                    },
                ],
            };
            await Engines.InsertAsync(engine);
            return engine;
        }

        public async Task<Engine> CreateEngineWithParatextProjectAsync()
        {
            var engine = new Engine
            {
                Id = "engine1",
                Owner = OWNER,
                SourceLanguage = "es",
                TargetLanguage = "en",
                Type = "Smt",
                Corpora =
                [
                    new()
                    {
                        Id = "corpus1",
                        SourceLanguage = "es",
                        TargetLanguage = "en",
                        SourceFiles =
                        [
                            new()
                            {
                                Id = "file1",
                                Filename = "file1.zip",
                                Format = FileFormat.Paratext,
                                TextId = "file1.zip",
                            },
                        ],
                        TargetFiles =
                        [
                            new()
                            {
                                Id = "file2",
                                Filename = "file2.zip",
                                Format = FileFormat.Paratext,
                                TextId = "file2.zip",
                            },
                        ],
                    },
                ],
            };
            await Engines.InsertAsync(engine);
            return engine;
        }

        public async Task<Engine> CreateParallelCorpusEngineWithTextFilesAsync()
        {
            var engine = new Engine
            {
                Id = "engine1",
                Owner = OWNER,
                SourceLanguage = "es",
                TargetLanguage = "en",
                Type = "Smt",
                ParallelCorpora =
                [
                    new()
                    {
                        Id = "parallel-corpus1",
                        SourceCorpora =
                        [
                            new()
                            {
                                Id = "parallel-corpus1-source1",
                                Name = "",
                                Language = "es",
                                Files =
                                [
                                    new()
                                    {
                                        Id = "file1",
                                        Filename = "file1.txt",
                                        Format = FileFormat.Text,
                                        TextId = "MAT",
                                    },
                                ],
                            },
                            new()
                            {
                                Id = "parallel-corpus1-source2",
                                Name = "",
                                Language = "es",
                                Files =
                                [
                                    new()
                                    {
                                        Id = "file3",
                                        Filename = "file3.txt",
                                        Format = FileFormat.Text,
                                        TextId = "MRK",
                                    },
                                ],
                            },
                        ],
                        TargetCorpora = new List<Models.MonolingualCorpus>()
                        {
                            new()
                            {
                                Id = "parallel-corpus1-target1",
                                Name = "",
                                Language = "en",
                                Files =
                                [
                                    new()
                                    {
                                        Id = "file2",
                                        Filename = "file2.txt",
                                        Format = FileFormat.Text,
                                        TextId = "MAT",
                                    },
                                ],
                            },
                            new()
                            {
                                Id = "parallel-corpus1-target2",
                                Name = "",
                                Language = "en",
                                Files =
                                [
                                    new()
                                    {
                                        Id = "file4",
                                        Filename = "file4.txt",
                                        Format = FileFormat.Text,
                                        TextId = "MRK",
                                    },
                                ],
                            },
                        },
                    },
                ],
            };
            await Engines.InsertAsync(engine);
            return engine;
        }

        public async Task<Engine> CreateMultipleParallelCorpusEngineWithTextFilesAsync()
        {
            var engine = new Engine
            {
                Id = "engine1",
                Owner = OWNER,
                SourceLanguage = "es",
                TargetLanguage = "en",
                Type = "Smt",
                ParallelCorpora =
                [
                    new()
                    {
                        Id = "parallel-corpus1",
                        SourceCorpora = new List<Models.MonolingualCorpus>()
                        {
                            new()
                            {
                                Id = "parallel-corpus1-source1",
                                Name = "",
                                Language = "es",
                                Files =
                                [
                                    new()
                                    {
                                        Id = "file1",
                                        Filename = "file1.txt",
                                        Format = FileFormat.Text,
                                        TextId = "MAT",
                                    },
                                ],
                            },
                        },
                        TargetCorpora = new List<Models.MonolingualCorpus>()
                        {
                            new()
                            {
                                Id = "parallel-corpus1-target1",
                                Name = "",
                                Language = "en",
                                Files =
                                [
                                    new()
                                    {
                                        Id = "file2",
                                        Filename = "file2.txt",
                                        Format = FileFormat.Text,
                                        TextId = "MAT",
                                    },
                                ],
                            },
                        },
                    },
                    new()
                    {
                        Id = "parallel-corpus2",
                        SourceCorpora = new List<Models.MonolingualCorpus>()
                        {
                            new()
                            {
                                Id = "parallel-corpus2-source1",
                                Name = "",
                                Language = "es",
                                Files =
                                [
                                    new()
                                    {
                                        Id = "file3",
                                        Filename = "file3.txt",
                                        Format = FileFormat.Text,
                                        TextId = "MRK",
                                    },
                                ],
                            },
                        },
                        TargetCorpora = new List<Models.MonolingualCorpus>()
                        {
                            new()
                            {
                                Id = "parallel-corpus2-target1",
                                Name = "",
                                Language = "en",
                                Files =
                                [
                                    new()
                                    {
                                        Id = "file4",
                                        Filename = "file4.txt",
                                        Format = FileFormat.Text,
                                        TextId = "MRK",
                                    },
                                ],
                            },
                        },
                    },
                ],
            };
            await Engines.InsertAsync(engine);
            return engine;
        }

        public async Task<Engine> CreateParallelCorpusEngineWithParatextProjectAsync()
        {
            var engine = new Engine
            {
                Id = "engine1",
                Owner = OWNER,
                SourceLanguage = "es",
                TargetLanguage = "en",
                Type = "Smt",
                ParallelCorpora =
                [
                    new()
                    {
                        Id = "parallel-corpus1",
                        SourceCorpora = new List<Models.MonolingualCorpus>()
                        {
                            new()
                            {
                                Id = "parallel-corpus1-source1",
                                Name = "",
                                Language = "es",
                                Files =
                                [
                                    new()
                                    {
                                        Id = "file1",
                                        Filename = "file1.zip",
                                        Format = FileFormat.Paratext,
                                        TextId = "file1.zip",
                                    },
                                ],
                            },
                            new()
                            {
                                Id = "parallel-corpus1-source2",
                                Name = "",
                                Language = "es",
                                Files =
                                [
                                    new()
                                    {
                                        Id = "file3",
                                        Filename = "file3.zip",
                                        Format = FileFormat.Paratext,
                                        TextId = "file3.zip",
                                    },
                                ],
                            },
                        },
                        TargetCorpora = new List<Models.MonolingualCorpus>()
                        {
                            new()
                            {
                                Id = "parallel-corpus1-target1",
                                Name = "",
                                Language = "en",
                                Files =
                                [
                                    new()
                                    {
                                        Id = "file2",
                                        Filename = "file2.zip",
                                        Format = FileFormat.Paratext,
                                        TextId = "file2.zip",
                                    },
                                ],
                            },
                            new()
                            {
                                Id = "parallel-corpus1-target2",
                                Name = "",
                                Language = "en",
                                Files =
                                [
                                    new()
                                    {
                                        Id = "file4",
                                        Filename = "file4.zip",
                                        Format = FileFormat.Paratext,
                                        TextId = "file4.zip",
                                    },
                                ],
                            },
                        },
                    },
                ],
            };
            await Engines.InsertAsync(engine);
            return engine;
        }
    }
}

#pragma warning restore CS0612 // Type or member is obsolete
