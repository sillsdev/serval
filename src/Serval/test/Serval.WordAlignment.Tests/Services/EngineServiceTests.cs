namespace Serval.WordAlignment.Services;

[TestFixture]
public class EngineServiceTests
{
    const string BUILD1_ID = "b00000000000000000000001";

    [Test]
    public void GetWordAlignmentAsync_EngineDoesNotExist()
    {
        var env = new TestEnvironment();
        Assert.ThrowsAsync<EntityNotFoundException>(() =>
            env.Service.GetWordAlignmentAsync("engine1", "esto es una prueba.", "this is a test.")
        );
    }

    [Test]
    public async Task GetWordAlignmentAsync_EngineExists()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineWithTextFilesAsync()).Id;
        WordAlignmentResult? result = await env.Service.GetWordAlignmentAsync(
            engineId,
            "esto es una prueba.",
            "this is a test."
        );
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Alignment, Is.EqualTo(CreateNAlignedWordPair(5)));
    }

    [Test]
    public async Task CreateAsync()
    {
        var env = new TestEnvironment();
        Engine engine = new()
        {
            Id = "engine1",
            Owner = "owner1",
            SourceLanguage = "es",
            TargetLanguage = "en",
            Type = "Statistical",
            ParallelCorpora = [],
        };
        await env.Service.CreateAsync(engine);

        engine = (await env.Engines.GetAsync("engine1"))!;
        Assert.That(engine.SourceLanguage, Is.EqualTo("es"));
        Assert.That(engine.TargetLanguage, Is.EqualTo("en"));
    }

    [Test]
    public async Task DeleteAsync_EngineExists()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineWithTextFilesAsync()).Id;
        await env.Service.DeleteAsync("engine1");
        Engine? engine = await env.Engines.GetAsync(engineId);
        Assert.That(engine, Is.Null);
    }

    [Test]
    public async Task DeleteAsync_ProjectDoesNotExist()
    {
        var env = new TestEnvironment();
        await env.CreateEngineWithTextFilesAsync();
        Assert.ThrowsAsync<EntityNotFoundException>(() => env.Service.DeleteAsync("engine3"));
    }

    [Test]
    public async Task StartBuildAsync_TrainOnNotSpecified()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineWithTextFilesAsync()).Id;
        await env.Service.StartBuildAsync(new Build { Id = BUILD1_ID, EngineRef = engineId });
        await env
            .WordAlignmentEngineService.Received()
            .StartBuildAsync(
                engineId,
                BUILD1_ID,
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
    public async Task StartBuildAsync_TextIdsEmpty()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineWithTextFilesAsync()).Id;
        await env.Service.StartBuildAsync(
            new Build
            {
                Id = BUILD1_ID,
                EngineRef = engineId,
                TrainOn =
                [
                    new TrainingCorpus
                    {
                        ParallelCorpusRef = "corpus1",
                        SourceFilters = [new() { CorpusRef = "corpus1-source1", TextIds = [] }],
                        TargetFilters = [new() { CorpusRef = "corpus1-target1", TextIds = [] }],
                    },
                ],
            }
        );
        await env
            .WordAlignmentEngineService.Received()
            .StartBuildAsync(
                engineId,
                BUILD1_ID,
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
    public async Task StartBuildAsync_TextIdsPopulated()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineWithTextFilesAsync()).Id;
        await env.Service.StartBuildAsync(
            new Build
            {
                Id = BUILD1_ID,
                EngineRef = engineId,
                TrainOn =
                [
                    new TrainingCorpus
                    {
                        ParallelCorpusRef = "corpus1",
                        SourceFilters = [new() { CorpusRef = "corpus1-source1", TextIds = ["text1"] }],
                        TargetFilters = [new() { CorpusRef = "corpus1-target1", TextIds = ["text1"] }],
                    },
                ],
            }
        );
        await env
            .WordAlignmentEngineService.Received()
            .StartBuildAsync(
                engineId,
                BUILD1_ID,
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
    public async Task StartBuildAsync_TextIdsNotSpecified()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineWithTextFilesAsync()).Id;
        await env.Service.StartBuildAsync(
            new Build
            {
                Id = BUILD1_ID,
                EngineRef = engineId,
                TrainOn =
                [
                    new TrainingCorpus
                    {
                        ParallelCorpusRef = "corpus1",
                        SourceFilters = [new() { CorpusRef = "corpus1-source1" }],
                        TargetFilters = [new() { CorpusRef = "corpus1-target1" }],
                    },
                ],
            }
        );
        await env
            .WordAlignmentEngineService.Received()
            .StartBuildAsync(
                engineId,
                BUILD1_ID,
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
    public async Task StartBuildAsync_OneOfMultipleCorpora()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateMultipleCorporaEngineWithTextFilesAsync()).Id;
        await env.Service.StartBuildAsync(
            new Build
            {
                Id = BUILD1_ID,
                EngineRef = engineId,
                TrainOn = [new TrainingCorpus { ParallelCorpusRef = "corpus1" }],
                WordAlignOn = [new WordAlignmentCorpus { ParallelCorpusRef = "corpus1" }],
            }
        );
        await env
            .WordAlignmentEngineService.Received()
            .StartBuildAsync(
                engineId,
                BUILD1_ID,
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
    public async Task StartBuildAsync_TrainOnOneWordAlignTheOther()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateMultipleCorporaEngineWithTextFilesAsync()).Id;
        await env.Service.StartBuildAsync(
            new Build
            {
                Id = BUILD1_ID,
                EngineRef = engineId,
                TrainOn = [new TrainingCorpus { ParallelCorpusRef = "corpus1" }],
                WordAlignOn = [new WordAlignmentCorpus { ParallelCorpusRef = "corpus2" }],
            }
        );
        await env
            .WordAlignmentEngineService.Received()
            .StartBuildAsync(
                engineId,
                BUILD1_ID,
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
    public async Task StartBuildAsync_TextFilesScriptureRangeSpecified()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineWithTextFilesAsync()).Id;
        Assert.ThrowsAsync<InvalidOperationException>(() =>
            env.Service.StartBuildAsync(
                new Build
                {
                    Id = BUILD1_ID,
                    EngineRef = engineId,
                    TrainOn =
                    [
                        new TrainingCorpus
                        {
                            ParallelCorpusRef = "corpus1",
                            SourceFilters =
                            [
                                new()
                                {
                                    CorpusRef = "corpus1-source1",
                                    ScriptureRange = "MAT",
                                    TextIds = [],
                                },
                            ],
                            TargetFilters =
                            [
                                new()
                                {
                                    CorpusRef = "corpus1-target1",
                                    ScriptureRange = "MAT",
                                    TextIds = [],
                                },
                            ],
                        },
                    ],
                }
            )
        );
    }

    [Test]
    public async Task StartBuildAsync_ScriptureRangeSpecified()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineWithParatextProjectAsync()).Id;
        await env.Service.StartBuildAsync(
            new Build
            {
                Id = BUILD1_ID,
                EngineRef = engineId,
                TrainOn =
                [
                    new TrainingCorpus
                    {
                        ParallelCorpusRef = "corpus1",
                        SourceFilters = [new() { CorpusRef = "corpus1-source1", ScriptureRange = "MAT 1;MRK" }],
                        TargetFilters = [new() { CorpusRef = "corpus1-target1", ScriptureRange = "MAT;MRK 1" }],
                    },
                ],
            }
        );
        await env
            .WordAlignmentEngineService.Received()
            .StartBuildAsync(
                engineId,
                BUILD1_ID,
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
    public async Task StartBuildAsync_ScriptureRangeEmptyString()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineWithParatextProjectAsync()).Id;
        await env.Service.StartBuildAsync(
            new Build
            {
                Id = BUILD1_ID,
                EngineRef = engineId,
                TrainOn =
                [
                    new TrainingCorpus
                    {
                        ParallelCorpusRef = "corpus1",
                        SourceFilters = [new() { CorpusRef = "corpus1-source1", ScriptureRange = "" }],
                        TargetFilters = [new() { CorpusRef = "corpus1-target1", ScriptureRange = "" }],
                    },
                ],
            }
        );
        await env
            .WordAlignmentEngineService.Received()
            .StartBuildAsync(
                engineId,
                BUILD1_ID,
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
    public async Task StartBuildAsync_MixedSourceAndTarget()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineWithMultipleParatextProjectAsync()).Id;
        await env.Service.StartBuildAsync(
            new Build
            {
                Id = BUILD1_ID,
                EngineRef = engineId,
                TrainOn =
                [
                    new TrainingCorpus
                    {
                        ParallelCorpusRef = "corpus1",
                        SourceFilters =
                        [
                            new() { CorpusRef = "corpus1-source1", ScriptureRange = "MAT 1-2;MRK 1-2" },
                            new() { CorpusRef = "corpus1-source2", ScriptureRange = "MAT 3;MRK 1" },
                        ],
                        TargetFilters =
                        [
                            new() { CorpusRef = "corpus1-target1", ScriptureRange = "MAT 2-3;MRK 2" },
                            new() { CorpusRef = "corpus1-target2", ScriptureRange = "MAT 1;MRK 1-2" },
                        ],
                    },
                ],
            }
        );
        await env
            .WordAlignmentEngineService.Received()
            .StartBuildAsync(
                engineId,
                BUILD1_ID,
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
    public async Task StartBuildAsync_NoTargetFilter()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineWithMultipleParatextProjectAsync()).Id;
        await env.Service.StartBuildAsync(
            new Build
            {
                Id = BUILD1_ID,
                EngineRef = engineId,
                TrainOn =
                [
                    new TrainingCorpus
                    {
                        ParallelCorpusRef = "corpus1",
                        SourceFilters = [new() { CorpusRef = "corpus1-source1", ScriptureRange = "MAT 1;MRK" }],
                    },
                ],
            }
        );
        await env
            .WordAlignmentEngineService.Received()
            .StartBuildAsync(
                engineId,
                BUILD1_ID,
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
    public async Task CancelBuildAsync_EngineExistsNotBuilding()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineWithTextFilesAsync()).Id;
        await env.Service.CancelBuildAsync(engineId);
    }

    [Test]
    public async Task UpdateCorpusAsync()
    {
        var env = new TestEnvironment();
        Engine engine = await env.CreateEngineWithTextFilesAsync();
        string corpusId = engine.ParallelCorpora[0].Id;

        ParallelCorpus? corpus = await env.Service.UpdateParallelCorpusAsync(
            engine.Id,
            corpusId,
            sourceCorpora:
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
                            TextId = "text2",
                        },
                    ],
                },
            ],
            null
        );

        Assert.That(corpus, Is.Not.Null);
        Assert.That(corpus.SourceCorpora, Has.Count.EqualTo(2));
        Assert.That(corpus.SourceCorpora[0].Files[0].Id, Is.EqualTo("file1"));
        Assert.That(corpus.SourceCorpora[1].Files[0].Id, Is.EqualTo("file3"));
        Assert.That(corpus.TargetCorpora, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task DeleteWordAlignmentsWhenParallelCorpusIsUpdatedAsync()
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
        await env.Service.UpdateParallelCorpusAsync(engine.Id, "corpus1", sourceCorpora: [], targetCorpora: []);
        Assert.That(await env.WordAlignments.GetAsync(wordAlignment.Id), Is.Null);
    }

    [Test]
    public async Task DeleteWordAlignmentsWhenCorpusFilesAreDeletedAsync()
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
        await env.CreateEngineWithTextFilesAsync();
        await env.WordAlignments.InsertAsync(wordAlignment);
        Assert.That(await env.WordAlignments.GetAsync(wordAlignment.Id), Is.Not.Null);
        await env.Service.DeleteAllCorpusFilesAsync("file1");
        Assert.That(await env.WordAlignments.GetAsync(wordAlignment.Id), Is.Null);
    }

    [Test]
    public async Task DeleteWordAlignmentsWhenCorpusFilesAreUpdatedAsync()
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
        await env.CreateEngineWithTextFilesAsync();
        await env.WordAlignments.InsertAsync(wordAlignment);
        Assert.That(await env.WordAlignments.GetAsync(wordAlignment.Id), Is.Not.Null);
        await env.Service.UpdateCorpusFilesAsync(
            "corpus1",
            [
                new()
                {
                    Id = "file1",
                    Filename = "newfilename",
                    TextId = "text1",
                    Format = FileFormat.Text,
                },
            ]
        );
        Assert.That(await env.WordAlignments.GetAsync(wordAlignment.Id), Is.Null);
    }

    private class TestEnvironment
    {
        public TestEnvironment()
        {
            Engines = new MemoryRepository<Engine>();
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

            Service = new TestEngineService(
                Engines,
                new MemoryRepository<Build>(),
                WordAlignments,
                EngineServiceFactory,
                dataFileOptions,
                new MemoryDataAccessContext(),
                new LoggerFactory()
            );
        }

        public EngineService Service { get; }
        public IRepository<Engine> Engines { get; }
        public IRepository<Models.WordAlignment> WordAlignments { get; }
        public IWordAlignmentEngineService WordAlignmentEngineService { get; }
        public IEngineServiceFactory EngineServiceFactory { get; }

        public async Task<Engine> CreateEngineWithTextFilesAsync()
        {
            var engine = new Engine
            {
                Id = "engine1",
                Owner = "owner1",
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
                Owner = "owner1",
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
                Owner = "owner1",
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
                Owner = "owner1",
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
                Owner = "owner1",
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

    private class TestEngineService(
        IRepository<Engine> engines,
        IRepository<Build> builds,
        IRepository<Models.WordAlignment> wordAlignments,
        IEngineServiceFactory engineServiceFactory,
        IOptionsMonitor<DataFileOptions> dataFileOptions,
        IDataAccessContext dataAccessContext,
        ILoggerFactory loggerFactory
    )
        : EngineService(
            engines,
            builds,
            wordAlignments,
            engineServiceFactory,
            dataFileOptions,
            dataAccessContext,
            loggerFactory
        )
    {
        protected override Dictionary<string, List<int>> GetChapters(string fileLocation, string scriptureRange)
        {
            try
            {
                return ScriptureRangeParser.GetChapters(scriptureRange);
            }
            catch (ArgumentException ae)
            {
                throw new InvalidOperationException($"The scripture range {scriptureRange} is not valid: {ae.Message}");
            }
        }
    }
}
