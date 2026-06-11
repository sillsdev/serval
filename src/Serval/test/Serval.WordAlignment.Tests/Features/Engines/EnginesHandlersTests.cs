using Serval.DataFiles.Contracts;
using Serval.Shared.Dtos;
using Serval.Shared.Services;
using Serval.WordAlignment.Services;

namespace Serval.WordAlignment.Features.Engines;

[TestFixture]
public class EnginesHandlersTests
{
    const string OWNER = "owner1";

    [Test]
    public void GetWordAlignment_EngineDoesNotExist()
    {
        var env = new TestEnvironment();
        var handler = new AlignHandler(env.Engines, env.EngineServiceFactory);
        Assert.ThrowsAsync<EntityNotFoundException>(async () =>
            await handler.HandleAsync(new Align(OWNER, "engine1", "esto es una prueba.", "this is a test."))
        );
    }

    [Test]
    public async Task GetWordAlignment_EngineExists()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineWithTextFilesAsync()).Id;
        var handler = new AlignHandler(env.Engines, env.EngineServiceFactory);
        WordAlignmentResponse response = await handler.HandleAsync(
            new Align(OWNER, engineId, "esto es una prueba.", "this is a test.")
        );
        Assert.That(response.IsAvailable, Is.True);
        Assert.That(
            response.Result!.Alignment,
            Is.EqualTo(
                CreateNAlignedWordPair(5)
                    .Select(awp => new AlignedWordPairDto
                    {
                        SourceIndex = awp.SourceIndex,
                        TargetIndex = awp.TargetIndex,
                        Score = awp.Score,
                    })
            )
        );
    }

    [Test]
    public async Task Create()
    {
        var env = new TestEnvironment();
        CreateEngine engineRequest = new(
            OWNER,
            new()
            {
                SourceLanguage = "es",
                TargetLanguage = "en",
                Type = "Statistical",
            }
        );
        CreateEngineResponse response = await new CreateEngineHandler(
            env.DataAccessContext,
            env.Engines,
            env.EngineServiceFactory,
            env.DtoMapper
        ).HandleAsync(engineRequest);

        Engine engine = (await env.Engines.GetAsync(response.Engine.Id))!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(engine.SourceLanguage, Is.EqualTo("es"));
            Assert.That(engine.TargetLanguage, Is.EqualTo("en"));
        }
    }

    [Test]
    public async Task Delete_EngineExists()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineWithTextFilesAsync()).Id;
        await new DeleteEngineHandler(
            env.DataAccessContext,
            env.Engines,
            env.Builds,
            env.WordAlignments,
            env.EngineServiceFactory
        ).HandleAsync(new(OWNER, "engine1"));
        Engine? engine = await env.Engines.GetAsync(engineId);
        Assert.That(engine, Is.Null);
    }

    [Test]
    public async Task Delete_EngineDoesNotExist()
    {
        var env = new TestEnvironment();
        await env.CreateEngineWithTextFilesAsync();
        Assert.ThrowsAsync<EntityNotFoundException>(() =>
            new DeleteEngineHandler(
                env.DataAccessContext,
                env.Engines,
                env.Builds,
                env.WordAlignments,
                env.EngineServiceFactory
            ).HandleAsync(new(OWNER, "engine3"))
        );
    }

    [Test]
    public async Task StartBuild_TrainOnNotSpecified()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineWithTextFilesAsync()).Id;
        StartBuildResponse response = await new StartBuildHandler(
            env.DataAccessContext,
            env.Engines,
            env.Builds,
            env.ContractMapper,
            env.EngineServiceFactory,
            Substitute.For<ILogger<StartBuildHandler>>(),
            env.DtoMapper,
            Substitute.For<Microsoft.Extensions.Configuration.IConfiguration>()
        ).HandleAsync(new(OWNER, engineId, new()));
        Assert.That(response.Build, Is.Not.Null);
        await env
            .WordAlignmentEngineService.Received()
            .StartBuildAsync(
                engineId,
                response.Build.Id,
                ArgEx.IsEquivalentTo<IReadOnlyList<ParallelCorpusContract>>([
                    new()
                    {
                        Id = "corpus1",
                        SourceCorpora =
                        [
                            new()
                            {
                                Id = "corpus1-source1",
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
                                Id = "corpus1-target1",
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
        StartBuildResponse response = await new StartBuildHandler(
            env.DataAccessContext,
            env.Engines,
            env.Builds,
            env.ContractMapper,
            env.EngineServiceFactory,
            Substitute.For<ILogger<StartBuildHandler>>(),
            env.DtoMapper,
            Substitute.For<Microsoft.Extensions.Configuration.IConfiguration>()
        ).HandleAsync(
            new(
                OWNER,
                engineId,
                new WordAlignmentBuildConfigDto
                {
                    TrainOn =
                    [
                        new TrainingCorpusConfigDto
                        {
                            ParallelCorpusId = "corpus1",
                            SourceFilters = [new() { CorpusId = "corpus1-source1", TextIds = [] }],
                            TargetFilters = [new() { CorpusId = "corpus1-target1", TextIds = [] }],
                        },
                    ],
                }
            )
        );
        Assert.That(response.Build, Is.Not.Null);
        await env
            .WordAlignmentEngineService.Received()
            .StartBuildAsync(
                engineId,
                response.Build.Id,
                ArgEx.IsEquivalentTo<IReadOnlyList<ParallelCorpusContract>>([
                    new()
                    {
                        Id = "corpus1",
                        SourceCorpora =
                        [
                            new()
                            {
                                Id = "corpus1-source1",
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
                                Id = "corpus1-target1",
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
        StartBuildResponse response = await new StartBuildHandler(
            env.DataAccessContext,
            env.Engines,
            env.Builds,
            env.ContractMapper,
            env.EngineServiceFactory,
            Substitute.For<ILogger<StartBuildHandler>>(),
            env.DtoMapper,
            Substitute.For<Microsoft.Extensions.Configuration.IConfiguration>()
        ).HandleAsync(
            new(
                OWNER,
                engineId,
                new WordAlignmentBuildConfigDto
                {
                    TrainOn =
                    [
                        new TrainingCorpusConfigDto
                        {
                            ParallelCorpusId = "corpus1",
                            SourceFilters = [new() { CorpusId = "corpus1-source1", TextIds = ["text1"] }],
                            TargetFilters = [new() { CorpusId = "corpus1-target1", TextIds = ["text1"] }],
                        },
                    ],
                }
            )
        );
        Assert.That(response.Build, Is.Not.Null);
        await env
            .WordAlignmentEngineService.Received()
            .StartBuildAsync(
                engineId,
                response.Build.Id,
                ArgEx.IsEquivalentTo<IReadOnlyList<ParallelCorpusContract>>([
                    new()
                    {
                        Id = "corpus1",
                        SourceCorpora =
                        [
                            new()
                            {
                                Id = "corpus1-source1",
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
                                Id = "corpus1-target1",
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
        StartBuildResponse response = await new StartBuildHandler(
            env.DataAccessContext,
            env.Engines,
            env.Builds,
            env.ContractMapper,
            env.EngineServiceFactory,
            Substitute.For<ILogger<StartBuildHandler>>(),
            env.DtoMapper,
            Substitute.For<Microsoft.Extensions.Configuration.IConfiguration>()
        ).HandleAsync(
            new(
                OWNER,
                engineId,
                new WordAlignmentBuildConfigDto
                {
                    TrainOn =
                    [
                        new TrainingCorpusConfigDto
                        {
                            ParallelCorpusId = "corpus1",
                            SourceFilters = [new() { CorpusId = "corpus1-source1" }],
                            TargetFilters = [new() { CorpusId = "corpus1-target1" }],
                        },
                    ],
                }
            )
        );
        Assert.That(response.Build, Is.Not.Null);
        await env
            .WordAlignmentEngineService.Received()
            .StartBuildAsync(
                engineId,
                response.Build.Id,
                ArgEx.IsEquivalentTo<IReadOnlyList<ParallelCorpusContract>>([
                    new()
                    {
                        Id = "corpus1",
                        SourceCorpora =
                        [
                            new()
                            {
                                Id = "corpus1-source1",
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
                                Id = "corpus1-target1",
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
        StartBuildResponse response = await new StartBuildHandler(
            env.DataAccessContext,
            env.Engines,
            env.Builds,
            env.ContractMapper,
            env.EngineServiceFactory,
            Substitute.For<ILogger<StartBuildHandler>>(),
            env.DtoMapper,
            Substitute.For<Microsoft.Extensions.Configuration.IConfiguration>()
        ).HandleAsync(
            new(
                OWNER,
                engineId,
                new WordAlignmentBuildConfigDto
                {
                    TrainOn = [new TrainingCorpusConfigDto { ParallelCorpusId = "corpus1" }],
                    WordAlignOn = [new WordAlignmentCorpusConfigDto { ParallelCorpusId = "corpus1" }],
                }
            )
        );
        Assert.That(response.Build, Is.Not.Null);
        await env
            .WordAlignmentEngineService.Received()
            .StartBuildAsync(
                engineId,
                response.Build.Id,
                ArgEx.IsEquivalentTo<IReadOnlyList<ParallelCorpusContract>>([
                    new()
                    {
                        Id = "corpus1",
                        SourceCorpora =
                        [
                            new()
                            {
                                Id = "corpus1-source1",
                                Language = "es",
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
                                Id = "corpus1-target1",
                                Language = "en",
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
    public async Task StartBuild_TrainOnOneWordAlignTheOther()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateMultipleCorporaEngineWithTextFilesAsync()).Id;
        StartBuildResponse response = await new StartBuildHandler(
            env.DataAccessContext,
            env.Engines,
            env.Builds,
            env.ContractMapper,
            env.EngineServiceFactory,
            Substitute.For<ILogger<StartBuildHandler>>(),
            env.DtoMapper,
            Substitute.For<Microsoft.Extensions.Configuration.IConfiguration>()
        ).HandleAsync(
            new(
                OWNER,
                engineId,
                new WordAlignmentBuildConfigDto
                {
                    TrainOn = [new TrainingCorpusConfigDto { ParallelCorpusId = "corpus1" }],
                    WordAlignOn = [new WordAlignmentCorpusConfigDto { ParallelCorpusId = "corpus2" }],
                }
            )
        );
        Assert.That(response.Build, Is.Not.Null);
        await env
            .WordAlignmentEngineService.Received()
            .StartBuildAsync(
                engineId,
                response.Build.Id,
                ArgEx.IsEquivalentTo<IReadOnlyList<ParallelCorpusContract>>([
                    new()
                    {
                        Id = "corpus1",
                        SourceCorpora =
                        [
                            new()
                            {
                                Id = "corpus1-source1",
                                Language = "es",
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
                                Id = "corpus1-target1",
                                Language = "en",
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
                        Id = "corpus2",
                        SourceCorpora =
                        [
                            new()
                            {
                                Id = "corpus2-source1",
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
                                Id = "corpus2-target1",
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
    public async Task StartBuild_TextFilesScriptureRangeSpecified()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineWithTextFilesAsync()).Id;
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await new StartBuildHandler(
                env.DataAccessContext,
                env.Engines,
                env.Builds,
                env.ContractMapper,
                env.EngineServiceFactory,
                Substitute.For<ILogger<StartBuildHandler>>(),
                env.DtoMapper,
                Substitute.For<Microsoft.Extensions.Configuration.IConfiguration>()
            ).HandleAsync(
                new(
                    OWNER,
                    engineId,
                    new WordAlignmentBuildConfigDto
                    {
                        TrainOn =
                        [
                            new TrainingCorpusConfigDto
                            {
                                ParallelCorpusId = "corpus1",
                                SourceFilters =
                                [
                                    new()
                                    {
                                        CorpusId = "corpus1-source1",
                                        ScriptureRange = "MAT",
                                        TextIds = [],
                                    },
                                ],
                                TargetFilters =
                                [
                                    new()
                                    {
                                        CorpusId = "corpus1-target1",
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
    public async Task StartBuild_ScriptureRangeSpecified()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineWithParatextProjectAsync()).Id;
        StartBuildResponse response = await new StartBuildHandler(
            env.DataAccessContext,
            env.Engines,
            env.Builds,
            env.ContractMapper,
            env.EngineServiceFactory,
            Substitute.For<ILogger<StartBuildHandler>>(),
            env.DtoMapper,
            Substitute.For<Microsoft.Extensions.Configuration.IConfiguration>()
        ).HandleAsync(
            new(
                OWNER,
                engineId,
                new WordAlignmentBuildConfigDto
                {
                    TrainOn =
                    [
                        new TrainingCorpusConfigDto
                        {
                            ParallelCorpusId = "corpus1",
                            SourceFilters = [new() { CorpusId = "corpus1-source1", ScriptureRange = "MAT 1;MRK" }],
                            TargetFilters = [new() { CorpusId = "corpus1-target1", ScriptureRange = "MAT;MRK 1" }],
                        },
                    ],
                }
            )
        );
        Assert.That(response.Build, Is.Not.Null);
        await env
            .WordAlignmentEngineService.Received()
            .StartBuildAsync(
                engineId,
                response.Build.Id,
                ArgEx.IsEquivalentTo<IReadOnlyList<ParallelCorpusContract>>([
                    new()
                    {
                        Id = "corpus1",
                        SourceCorpora =
                        [
                            new()
                            {
                                Id = "corpus1-source1",
                                Language = "es",
                                TrainOnChapters = new Dictionary<string, HashSet<int>> { ["MAT"] = [1], ["MRK"] = [] },
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
                                Id = "corpus1-target1",
                                Language = "en",
                                TrainOnChapters = new Dictionary<string, HashSet<int>> { ["MAT"] = [], ["MRK"] = [1] },
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
        StartBuildResponse response = await new StartBuildHandler(
            env.DataAccessContext,
            env.Engines,
            env.Builds,
            env.ContractMapper,
            env.EngineServiceFactory,
            Substitute.For<ILogger<StartBuildHandler>>(),
            env.DtoMapper,
            Substitute.For<Microsoft.Extensions.Configuration.IConfiguration>()
        ).HandleAsync(
            new(
                OWNER,
                engineId,
                new WordAlignmentBuildConfigDto
                {
                    TrainOn =
                    [
                        new TrainingCorpusConfigDto
                        {
                            ParallelCorpusId = "corpus1",
                            SourceFilters = [new() { CorpusId = "corpus1-source1", ScriptureRange = "" }],
                            TargetFilters = [new() { CorpusId = "corpus1-target1", ScriptureRange = "" }],
                        },
                    ],
                }
            )
        );
        Assert.That(response.Build, Is.Not.Null);
        await env
            .WordAlignmentEngineService.Received()
            .StartBuildAsync(
                engineId,
                response.Build.Id,
                ArgEx.IsEquivalentTo<IReadOnlyList<ParallelCorpusContract>>([
                    new()
                    {
                        Id = "corpus1",
                        SourceCorpora =
                        [
                            new()
                            {
                                Id = "corpus1-source1",
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
                                Id = "corpus1-target1",
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
    public async Task StartBuild_MixedSourceAndTarget()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineWithMultipleParatextProjectAsync()).Id;
        StartBuildResponse response = await new StartBuildHandler(
            env.DataAccessContext,
            env.Engines,
            env.Builds,
            env.ContractMapper,
            env.EngineServiceFactory,
            Substitute.For<ILogger<StartBuildHandler>>(),
            env.DtoMapper,
            Substitute.For<Microsoft.Extensions.Configuration.IConfiguration>()
        ).HandleAsync(
            new(
                OWNER,
                engineId,
                new WordAlignmentBuildConfigDto
                {
                    TrainOn =
                    [
                        new TrainingCorpusConfigDto
                        {
                            ParallelCorpusId = "corpus1",
                            SourceFilters =
                            [
                                new() { CorpusId = "corpus1-source1", ScriptureRange = "MAT 1-2;MRK 1-2" },
                                new() { CorpusId = "corpus1-source2", ScriptureRange = "MAT 3;MRK 1" },
                            ],
                            TargetFilters =
                            [
                                new() { CorpusId = "corpus1-target1", ScriptureRange = "MAT 2-3;MRK 2" },
                                new() { CorpusId = "corpus1-target2", ScriptureRange = "MAT 1;MRK 1-2" },
                            ],
                        },
                    ],
                }
            )
        );
        Assert.That(response.Build, Is.Not.Null);
        await env
            .WordAlignmentEngineService.Received()
            .StartBuildAsync(
                engineId,
                response.Build.Id,
                ArgEx.IsEquivalentTo<IReadOnlyList<ParallelCorpusContract>>([
                    new()
                    {
                        Id = "corpus1",
                        SourceCorpora =
                        [
                            new()
                            {
                                Id = "corpus1-source1",
                                Language = "es",
                                TrainOnChapters = new Dictionary<string, HashSet<int>>
                                {
                                    ["MAT"] = [1, 2],
                                    ["MRK"] = [1, 2],
                                },
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
                                Id = "corpus1-source2",
                                Language = "es",
                                TrainOnChapters = new Dictionary<string, HashSet<int>> { ["MAT"] = [3], ["MRK"] = [1] },
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
                                Id = "corpus1-target1",
                                Language = "en",
                                TrainOnChapters = new Dictionary<string, HashSet<int>>
                                {
                                    ["MAT"] = [2, 3],
                                    ["MRK"] = [2],
                                },
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
                                Id = "corpus1-target2",
                                Language = "en",
                                TrainOnChapters = new Dictionary<string, HashSet<int>>
                                {
                                    ["MAT"] = [1],
                                    ["MRK"] = [1, 2],
                                },
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
    public async Task StartBuild_NoTargetFilter()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineWithMultipleParatextProjectAsync()).Id;
        StartBuildResponse response = await new StartBuildHandler(
            env.DataAccessContext,
            env.Engines,
            env.Builds,
            env.ContractMapper,
            env.EngineServiceFactory,
            Substitute.For<ILogger<StartBuildHandler>>(),
            env.DtoMapper,
            Substitute.For<Microsoft.Extensions.Configuration.IConfiguration>()
        ).HandleAsync(
            new(
                OWNER,
                engineId,
                new WordAlignmentBuildConfigDto
                {
                    TrainOn =
                    [
                        new TrainingCorpusConfigDto
                        {
                            ParallelCorpusId = "corpus1",
                            SourceFilters = [new() { CorpusId = "corpus1-source1", ScriptureRange = "MAT 1;MRK" }],
                        },
                    ],
                }
            )
        );
        Assert.That(response.Build, Is.Not.Null);
        await env
            .WordAlignmentEngineService.Received()
            .StartBuildAsync(
                engineId,
                response.Build.Id,
                ArgEx.IsEquivalentTo<IReadOnlyList<ParallelCorpusContract>>([
                    new()
                    {
                        Id = "corpus1",
                        SourceCorpora =
                        [
                            new()
                            {
                                Id = "corpus1-source1",
                                Language = "es",
                                TrainOnChapters = new Dictionary<string, HashSet<int>> { ["MAT"] = [1], ["MRK"] = [] },
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
                                Id = "corpus1-source2",
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
                                Id = "corpus1-target1",
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
                                Id = "corpus1-target2",
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
    public async Task CancelBuild_EngineExistsNotBuilding()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineWithTextFilesAsync()).Id;
        await new CancelBuildHandler(
            env.DataAccessContext,
            env.Engines,
            env.Builds,
            env.EngineServiceFactory,
            env.DtoMapper
        ).HandleAsync(new(OWNER, engineId));
    }

    [Test]
    public async Task UpdateCorpus()
    {
        var env = new TestEnvironment();
        Engine engine = await env.CreateEngineWithTextFilesAsync();
        string corpusId = engine.ParallelCorpora[0].Id;

        var getCorpusHandler = Substitute.For<IRequestHandler<GetCorpus, GetCorpusResponse>>();
        getCorpusHandler
            .HandleAsync(Arg.Is<GetCorpus>(r => r.CorpusId == "corpus1-source1"), Arg.Any<CancellationToken>())
            .Returns(
                new GetCorpusResponse(
                    true,
                    new CorpusContract(
                        "corpus1-source1",
                        "es",
                        null,
                        [new(new DataFileContract("file1", "file1", "file1.txt", FileFormat.Text), "text1")]
                    )
                )
            );
        getCorpusHandler
            .HandleAsync(Arg.Is<GetCorpus>(r => r.CorpusId == "corpus1-source2"), Arg.Any<CancellationToken>())
            .Returns(
                new GetCorpusResponse(
                    true,
                    new CorpusContract(
                        "corpus1-source2",
                        "es",
                        null,
                        [new(new DataFileContract("file1", "file1", "file1.txt", FileFormat.Text), "text1")]
                    )
                )
            );

        UpdateParallelCorpusResponse response = await new UpdateParallelCorpusHandler(
            env.DataAccessContext,
            env.Engines,
            env.WordAlignments,
            getCorpusHandler,
            env.DtoMapper
        ).HandleAsync(
            new(
                OWNER,
                engine.Id,
                corpusId,
                new WordAlignmentParallelCorpusUpdateConfigDto
                {
                    SourceCorpusIds = ["corpus1-source1", "corpus1-source2"],
                }
            )
        );

        Assert.That(response.Corpus, Is.Not.Null);
        Assert.That(response.Corpus.SourceCorpora, Has.Count.EqualTo(2));
        Assert.That(response.Corpus.SourceCorpora[0].Id, Is.EqualTo("corpus1-source1"));
        Assert.That(response.Corpus.SourceCorpora[1].Id, Is.EqualTo("corpus1-source2"));
        Assert.That(response.Corpus.TargetCorpora, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task DeleteWordAlignmentsWhenParallelCorpusIsUpdated()
    {
        var env = new TestEnvironment();
        Models.WordAlignment wordAlignment = new()
        {
            Id = "wordAlignment",
            EngineRef = "engine1",
            CorpusRef = "corpus1",
            SourceRefs = ["ref1"],
            TargetRefs = ["ref1"],
            Refs = ["ref1"],
            TextId = "textId1",
            SourceTokens = [],
            TargetTokens = [],
            Alignment = CreateNAlignedWordPair(0),
        };
        var engine = await env.CreateEngineWithTextFilesAsync();
        await env.WordAlignments.InsertAsync(wordAlignment);
        Assert.That(await env.WordAlignments.GetAsync(wordAlignment.Id), Is.Not.Null);

        var getCorpusHandler = Substitute.For<IRequestHandler<GetCorpus, GetCorpusResponse>>();
        await new UpdateParallelCorpusHandler(
            env.DataAccessContext,
            env.Engines,
            env.WordAlignments,
            getCorpusHandler,
            env.DtoMapper
        ).HandleAsync(
            new(
                OWNER,
                engine.Id,
                "corpus1",
                new WordAlignmentParallelCorpusUpdateConfigDto { SourceCorpusIds = [], TargetCorpusIds = [] }
            )
        );

        Assert.That(await env.WordAlignments.GetAsync(wordAlignment.Id), Is.Null);
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
            Progress = 0.1,
        };
        await env.Builds.InsertAsync(build);
        GetBuildResponse response = await new GetBuildHandler(
            env.Engines,
            env.Builds,
            env.DtoMapper,
            env.ApiOptions
        ).HandleAsync(new(OWNER, engineId, build.Id, null));
        Assert.That(response.Build, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.Build.Id, Is.EqualTo(build.Id));
            Assert.That(response.Build.Revision, Is.EqualTo(1));
            Assert.That(response.Build.Progress, Is.EqualTo(0.1).Within(0.01));
            Assert.That(response.Status, Is.EqualTo(GetBuildStatus.Found));
        }
    }

    [Test]
    public async Task GetBuild_WithMinRevision_ReturnsFoundBuild()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineWithTextFilesAsync()).Id;
        var build = new Build { Id = "build1", EngineRef = engineId };
        await env.Builds.InsertAsync(build);
        Task<GetBuildResponse> task = new GetBuildHandler(
            env.Engines,
            env.Builds,
            env.DtoMapper,
            env.ApiOptions
        ).HandleAsync(new(OWNER, engineId, build.Id, 2));
        await env.Builds.UpdateAsync(build, u => u.Set(b => b.Progress, 0.1));
        GetBuildResponse response = await task;
        Assert.That(response.Build, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.Build.Id, Is.EqualTo(build.Id));
            Assert.That(response.Build.Revision, Is.EqualTo(2));
            Assert.That(response.Build.Progress, Is.EqualTo(0.1).Within(0.01));
            Assert.That(response.Status, Is.EqualTo(GetBuildStatus.Found));
        }
    }

    [Test]
    public async Task GetBuild_WithMinRevision_ReturnsNotActive_Deleted()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineWithTextFilesAsync()).Id;
        var build = new Build { Id = "build1", EngineRef = engineId };
        await env.Builds.InsertAsync(build);
        Task<GetBuildResponse> task = new GetBuildHandler(
            env.Engines,
            env.Builds,
            env.DtoMapper,
            env.ApiOptions
        ).HandleAsync(new(OWNER, engineId, build.Id, 2));
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
    public async Task GetCurrentBuild_WithNoMinRevision_ReturnsFoundBuild()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineWithTextFilesAsync()).Id;
        var build = new Build
        {
            Id = "build1",
            EngineRef = engineId,
            Progress = 0.1,
        };
        await env.Builds.InsertAsync(build);
        GetCurrentBuildResponse response = await new GetCurrentBuildHandler(
            env.Engines,
            env.Builds,
            env.DtoMapper,
            env.ApiOptions
        ).HandleAsync(new(OWNER, engineId, null));
        Assert.That(response.Build, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.Build.Id, Is.EqualTo(build.Id));
            Assert.That(response.Build.Revision, Is.EqualTo(1));
            Assert.That(response.Build.Progress, Is.EqualTo(0.1).Within(0.01));
            Assert.That(response.Status, Is.EqualTo(GetCurrentBuildStatus.Found));
        }
    }

    [Test]
    public async Task GetCurrentBuild_WithMinRevision_ReturnsFoundBuild()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineWithTextFilesAsync()).Id;
        var build = new Build { Id = "build1", EngineRef = engineId };
        await env.Builds.InsertAsync(build);
        Task<GetCurrentBuildResponse> task = new GetCurrentBuildHandler(
            env.Engines,
            env.Builds,
            env.DtoMapper,
            env.ApiOptions
        ).HandleAsync(new(OWNER, engineId, 2));
        await env.Builds.UpdateAsync(build, u => u.Set(b => b.Progress, 0.1));
        GetCurrentBuildResponse response = await task;
        Assert.That(response.Build, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.Build.Id, Is.EqualTo(build.Id));
            Assert.That(response.Build.Revision, Is.EqualTo(2));
            Assert.That(response.Build.Progress, Is.EqualTo(0.1).Within(0.01));
            Assert.That(response.Status, Is.EqualTo(GetCurrentBuildStatus.Found));
        }
    }

    [Test]
    public async Task GetCurrentBuild_WithMinRevision_ReturnsNotActive_Deleted()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineWithTextFilesAsync()).Id;
        var build = new Build { Id = "build1", EngineRef = engineId };
        await env.Builds.InsertAsync(build);
        Task<GetCurrentBuildResponse> task = new GetCurrentBuildHandler(
            env.Engines,
            env.Builds,
            env.DtoMapper,
            env.ApiOptions
        ).HandleAsync(new(OWNER, engineId, 2));
        await env.Builds.DeleteAsync(build);
        GetCurrentBuildResponse response = await task;
        Assert.That(response.Status, Is.EqualTo(GetCurrentBuildStatus.NotActive));
        Assert.That(response.Build, Is.Null);
    }

    [Test]
    public void GetCurrentBuild_BuildDoesNotExist_Throws()
    {
        var env = new TestEnvironment();
        GetCurrentBuildHandler handler = new(env.Engines, env.Builds, env.DtoMapper, env.ApiOptions);
        Assert.ThrowsAsync<EntityNotFoundException>(() =>
            handler.HandleAsync(new GetCurrentBuild(OWNER, "engine1", 2))
        );
    }

    private class TestEnvironment
    {
        public TestEnvironment()
        {
            Engines = new MemoryRepository<Engine>();
            Builds = new MemoryRepository<Build>();
            WordAlignments = new MemoryRepository<Models.WordAlignment>();

            WordAlignmentEngineService = Substitute.For<IWordAlignmentEngineService>();
            WordAlignmentEngineService
                .AlignAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(
                    new WordAlignmentResultContract
                    {
                        SourceTokens = "esto es una prueba .".Split(),
                        TargetTokens = "this is a test .".Split(),
                        Alignment =
                        [
                            new AlignedWordPairContract { SourceIndex = 0, TargetIndex = 0 },
                            new AlignedWordPairContract { SourceIndex = 1, TargetIndex = 1 },
                            new AlignedWordPairContract { SourceIndex = 2, TargetIndex = 2 },
                            new AlignedWordPairContract { SourceIndex = 3, TargetIndex = 3 },
                            new AlignedWordPairContract { SourceIndex = 4, TargetIndex = 4 },
                        ],
                    }
                );

            EngineServiceFactory = Substitute.For<IEngineServiceFactory>();
            EngineServiceFactory
                .TryGetEngineService("Statistical", out Arg.Any<IWordAlignmentEngineService?>())
                .Returns(x =>
                {
                    x[1] = WordAlignmentEngineService;
                    return true;
                });

            IOptionsMonitor<DataFileOptions> dataFileOptions = Substitute.For<IOptionsMonitor<DataFileOptions>>();
            dataFileOptions.CurrentValue.Returns(new DataFileOptions());
            var parallelCorpusService = Substitute.For<IParallelCorpusService>();
            parallelCorpusService
                .GetChapters(Arg.Any<IReadOnlyList<ParallelCorpusContract>>(), Arg.Any<string>(), Arg.Any<string>())
                .Returns(callInfo =>
                {
                    return ScriptureRangeParser.GetChapters(callInfo.ArgAt<string>(2));
                });

            ContractMapper = new ContractMapper(dataFileOptions, parallelCorpusService);
            DtoMapper = new DtoMapper(Substitute.For<IUrlService>());
            DataAccessContext = new MemoryDataAccessContext();

            ApiOptions = Substitute.For<IOptionsMonitor<ApiOptions>>();
            ApiOptions.CurrentValue.Returns(new ApiOptions());
        }

        public IRepository<Engine> Engines { get; }
        public IRepository<Build> Builds { get; }
        public IDataAccessContext DataAccessContext { get; }
        public IRepository<Models.WordAlignment> WordAlignments { get; }
        public IWordAlignmentEngineService WordAlignmentEngineService { get; }
        public IEngineServiceFactory EngineServiceFactory { get; }
        public ContractMapper ContractMapper { get; }
        public DtoMapper DtoMapper { get; }
        public IOptionsMonitor<ApiOptions> ApiOptions { get; }

        public async Task<Engine> CreateEngineWithTextFilesAsync()
        {
            var engine = new Engine
            {
                Id = "engine1",
                Owner = OWNER,
                SourceLanguage = "es",
                TargetLanguage = "en",
                Type = "Statistical",
                ParallelCorpora =
                [
                    new()
                    {
                        Id = "corpus1",
                        SourceCorpora =
                        [
                            new()
                            {
                                Id = "corpus1-source1",
                                Name = "",
                                Language = "es",
                                Files =
                                [
                                    new()
                                    {
                                        Id = "file1",
                                        Filename = "file1.txt",
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
                                Id = "corpus1-target1",
                                Name = "",
                                Language = "en",
                                Files =
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
                    },
                ],
                ModelRevision = 1,
            };
            await Engines.InsertAsync(engine);
            return engine;
        }

        public async Task<Engine> CreateEngineWithMultipleTextFilesAsync()
        {
            var engine = new Engine
            {
                Id = "engine1",
                Owner = OWNER,
                SourceLanguage = "es",
                TargetLanguage = "en",
                Type = "Statistical",
                ParallelCorpora =
                [
                    new()
                    {
                        Id = "corpus1",
                        SourceCorpora =
                        [
                            new()
                            {
                                Id = "corpus1-source1",
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
                                Id = "corpus1-source2",
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
                        TargetCorpora =
                        [
                            new()
                            {
                                Id = "corpus1-target1",
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
                                Id = "corpus1-target2",
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
                        ],
                    },
                ],
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
                Type = "Statistical",
                ParallelCorpora =
                [
                    new()
                    {
                        Id = "corpus1",
                        SourceCorpora =
                        [
                            new()
                            {
                                Id = "corpus1-source1",
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
                        ],
                        TargetCorpora =
                        [
                            new()
                            {
                                Id = "corpus1-target1",
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
                        ],
                    },
                    new()
                    {
                        Id = "corpus2",
                        SourceCorpora =
                        [
                            new()
                            {
                                Id = "corpus2-source1",
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
                        TargetCorpora =
                        [
                            new()
                            {
                                Id = "corpus2-target1",
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
                Type = "Statistical",
                ParallelCorpora =
                [
                    new()
                    {
                        Id = "corpus1",
                        SourceCorpora =
                        [
                            new()
                            {
                                Id = "corpus1-source1",
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
                        ],
                        TargetCorpora =
                        [
                            new()
                            {
                                Id = "corpus1-target1",
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
                        ],
                    },
                ],
            };
            await Engines.InsertAsync(engine);
            return engine;
        }

        public async Task<Engine> CreateEngineWithMultipleParatextProjectAsync()
        {
            var engine = new Engine
            {
                Id = "engine1",
                Owner = OWNER,
                SourceLanguage = "es",
                TargetLanguage = "en",
                Type = "Statistical",
                ParallelCorpora =
                [
                    new()
                    {
                        Id = "corpus1",
                        SourceCorpora =
                        [
                            new()
                            {
                                Id = "corpus1-source1",
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
                                Id = "corpus1-source2",
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
                        ],
                        TargetCorpora =
                        [
                            new()
                            {
                                Id = "corpus1-target1",
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
                                Id = "corpus1-target2",
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
                        ],
                    },
                ],
            };
            await Engines.InsertAsync(engine);
            return engine;
        }
    }

    private static IReadOnlyList<AlignedWordPair> CreateNAlignedWordPair(int numberOfAlignedWords)
    {
        var alignedWordPairs = new List<AlignedWordPair>();
        for (int i = 0; i < numberOfAlignedWords; i++)
        {
            alignedWordPairs.Add(new AlignedWordPair { SourceIndex = i, TargetIndex = i });
        }
        return alignedWordPairs;
    }
}
