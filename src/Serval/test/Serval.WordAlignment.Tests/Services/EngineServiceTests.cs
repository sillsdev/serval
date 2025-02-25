using Google.Protobuf.WellKnownTypes;
using Serval.WordAlignment.V1;

namespace Serval.WordAlignment.Services;

[TestFixture]
public class EngineServiceTests
{
    const string BUILD1_ID = "b00000000000000000000001";

    [Test]
    public void GetWordAlignmentAsync_EngineDoesNotExist()
    {
        var env = new TestEnvironment();
        Assert.ThrowsAsync<EntityNotFoundException>(
            () => env.Service.GetWordAlignmentAsync("engine1", "esto es una prueba.", "this is a test.")
        );
    }

    [Test]
    public async Task GetWordAlignmentAsync_EngineExists()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineWithTextFilesAsync()).Id;
        Models.WordAlignmentResult? result = await env.Service.GetWordAlignmentAsync(
            engineId,
            "esto es una prueba.",
            "this is a test."
        );
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Alignment, Is.EqualTo(CreateNAlignedWordPair(5)));
    }

    [Test]
    public async Task CreateAsync()
    {
        var env = new TestEnvironment();
        Engine engine =
            new()
            {
                Id = "engine1",
                Owner = "owner1",
                SourceLanguage = "es",
                TargetLanguage = "en",
                Type = "Statistical",
                ParallelCorpora = []
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
        _ = env.WordAlignmentServiceClient.Received()
            .StartBuildAsync(
                new StartBuildRequest
                {
                    BuildId = BUILD1_ID,
                    EngineId = engineId,
                    EngineType = "Statistical",
                    Corpora =
                    {
                        new V1.ParallelCorpus
                        {
                            Id = "corpus1",
                            SourceCorpora =
                            {
                                new List<V1.MonolingualCorpus>
                                {
                                    new()
                                    {
                                        Id = "corpus1-source1",
                                        Language = "es",
                                        Files =
                                        {
                                            new V1.CorpusFile
                                            {
                                                Location = "file1.txt",
                                                Format = V1.FileFormat.Text,
                                                TextId = "text1"
                                            }
                                        },
                                        WordAlignOnAll = true,
                                        TrainOnAll = true
                                    }
                                }
                            },
                            TargetCorpora =
                            {
                                new List<V1.MonolingualCorpus>
                                {
                                    new()
                                    {
                                        Id = "corpus1-target1",
                                        Language = "en",
                                        Files =
                                        {
                                            new V1.CorpusFile
                                            {
                                                Location = "file2.txt",
                                                Format = V1.FileFormat.Text,
                                                TextId = "text1"
                                            }
                                        },
                                        WordAlignOnAll = true,
                                        TrainOnAll = true
                                    }
                                }
                            }
                        }
                    }
                }
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
                        SourceFilters = new List<ParallelCorpusFilter>()
                        {
                            new() { CorpusRef = "corpus1-source1", TextIds = [] }
                        },
                        TargetFilters = new List<ParallelCorpusFilter>()
                        {
                            new() { CorpusRef = "corpus1-target1", TextIds = [] }
                        }
                    }
                ]
            }
        );
        _ = env.WordAlignmentServiceClient.Received()
            .StartBuildAsync(
                new StartBuildRequest
                {
                    BuildId = BUILD1_ID,
                    EngineId = engineId,
                    EngineType = "Statistical",
                    Corpora =
                    {
                        new V1.ParallelCorpus
                        {
                            Id = "corpus1",
                            SourceCorpora =
                            {
                                new List<V1.MonolingualCorpus>
                                {
                                    new()
                                    {
                                        Id = "corpus1-source1",
                                        Language = "es",
                                        TrainOnTextIds = { },
                                        Files =
                                        {
                                            new V1.CorpusFile
                                            {
                                                Location = "file1.txt",
                                                Format = V1.FileFormat.Text,
                                                TextId = "text1"
                                            }
                                        },
                                        WordAlignOnAll = true,
                                        TrainOnAll = false
                                    }
                                }
                            },
                            TargetCorpora =
                            {
                                new List<V1.MonolingualCorpus>
                                {
                                    new()
                                    {
                                        Id = "corpus1-target1",
                                        Language = "en",
                                        TrainOnTextIds = { },
                                        Files =
                                        {
                                            new V1.CorpusFile
                                            {
                                                Location = "file2.txt",
                                                Format = V1.FileFormat.Text,
                                                TextId = "text1"
                                            }
                                        },
                                        WordAlignOnAll = true,
                                        TrainOnAll = false
                                    }
                                }
                            }
                        }
                    }
                }
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
                        SourceFilters = new List<ParallelCorpusFilter>()
                        {
                            new() { CorpusRef = "corpus1-source1", TextIds = ["text1"] }
                        },
                        TargetFilters = new List<ParallelCorpusFilter>()
                        {
                            new() { CorpusRef = "corpus1-target1", TextIds = ["text1"] }
                        }
                    }
                ]
            }
        );
        _ = env.WordAlignmentServiceClient.Received()
            .StartBuildAsync(
                new StartBuildRequest
                {
                    BuildId = BUILD1_ID,
                    EngineId = engineId,
                    EngineType = "Statistical",
                    Corpora =
                    {
                        new V1.ParallelCorpus
                        {
                            Id = "corpus1",
                            SourceCorpora =
                            {
                                new List<V1.MonolingualCorpus>
                                {
                                    new()
                                    {
                                        Id = "corpus1-source1",
                                        Language = "es",
                                        TrainOnTextIds = { "text1" },
                                        Files =
                                        {
                                            new V1.CorpusFile
                                            {
                                                Location = "file1.txt",
                                                Format = V1.FileFormat.Text,
                                                TextId = "text1"
                                            }
                                        },
                                        WordAlignOnAll = true,
                                        TrainOnAll = false
                                    }
                                }
                            },
                            TargetCorpora =
                            {
                                new List<V1.MonolingualCorpus>
                                {
                                    new()
                                    {
                                        Id = "corpus1-target1",
                                        Language = "en",
                                        TrainOnTextIds = { "text1" },
                                        Files =
                                        {
                                            new V1.CorpusFile
                                            {
                                                Location = "file2.txt",
                                                Format = V1.FileFormat.Text,
                                                TextId = "text1"
                                            }
                                        },
                                        WordAlignOnAll = true,
                                        TrainOnAll = false
                                    }
                                }
                            }
                        }
                    }
                }
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
                        SourceFilters = new List<ParallelCorpusFilter>() { new() { CorpusRef = "corpus1-source1" } },
                        TargetFilters = new List<ParallelCorpusFilter>() { new() { CorpusRef = "corpus1-target1" } }
                    }
                ]
            }
        );
        _ = env.WordAlignmentServiceClient.Received()
            .StartBuildAsync(
                new StartBuildRequest
                {
                    BuildId = BUILD1_ID,
                    EngineId = engineId,
                    EngineType = "Statistical",
                    Corpora =
                    {
                        new V1.ParallelCorpus
                        {
                            Id = "corpus1",
                            SourceCorpora =
                            {
                                new List<V1.MonolingualCorpus>
                                {
                                    new()
                                    {
                                        Id = "corpus1-source1",
                                        Language = "es",
                                        Files =
                                        {
                                            new V1.CorpusFile
                                            {
                                                Location = "file1.txt",
                                                Format = V1.FileFormat.Text,
                                                TextId = "text1"
                                            }
                                        },
                                        WordAlignOnAll = true,
                                        TrainOnAll = true
                                    }
                                }
                            },
                            TargetCorpora =
                            {
                                new List<V1.MonolingualCorpus>
                                {
                                    new()
                                    {
                                        Id = "corpus1-target1",
                                        Language = "en",
                                        Files =
                                        {
                                            new V1.CorpusFile
                                            {
                                                Location = "file2.txt",
                                                Format = V1.FileFormat.Text,
                                                TextId = "text1"
                                            }
                                        },
                                        WordAlignOnAll = true,
                                        TrainOnAll = true
                                    }
                                }
                            }
                        }
                    }
                }
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
                WordAlignOn = [new WordAlignmentCorpus { ParallelCorpusRef = "corpus1" }]
            }
        );
        _ = env.WordAlignmentServiceClient.Received()
            .StartBuildAsync(
                new StartBuildRequest
                {
                    BuildId = BUILD1_ID,
                    EngineId = engineId,
                    EngineType = "Statistical",
                    Corpora =
                    {
                        new V1.ParallelCorpus
                        {
                            Id = "corpus1",
                            SourceCorpora =
                            {
                                new List<V1.MonolingualCorpus>
                                {
                                    new()
                                    {
                                        Id = "corpus1-source1",
                                        Language = "es",
                                        Files =
                                        {
                                            new V1.CorpusFile
                                            {
                                                Location = "file1.txt",
                                                Format = V1.FileFormat.Text,
                                                TextId = "MAT"
                                            }
                                        },
                                        WordAlignOnAll = true,
                                        TrainOnAll = true
                                    }
                                }
                            },
                            TargetCorpora =
                            {
                                new List<V1.MonolingualCorpus>
                                {
                                    new()
                                    {
                                        Id = "corpus1-target1",
                                        Language = "en",
                                        Files =
                                        {
                                            new V1.CorpusFile
                                            {
                                                Location = "file2.txt",
                                                Format = V1.FileFormat.Text,
                                                TextId = "MAT"
                                            }
                                        },
                                        WordAlignOnAll = true,
                                        TrainOnAll = true
                                    }
                                }
                            }
                        }
                    }
                }
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
                WordAlignOn = [new WordAlignmentCorpus { ParallelCorpusRef = "corpus2" }]
            }
        );
        _ = env.WordAlignmentServiceClient.Received()
            .StartBuildAsync(
                new StartBuildRequest
                {
                    BuildId = BUILD1_ID,
                    EngineId = engineId,
                    EngineType = "Statistical",
                    Corpora =
                    {
                        new V1.ParallelCorpus
                        {
                            Id = "corpus1",
                            SourceCorpora =
                            {
                                new List<V1.MonolingualCorpus>
                                {
                                    new()
                                    {
                                        Id = "corpus1-source1",
                                        Language = "es",
                                        Files =
                                        {
                                            new V1.CorpusFile
                                            {
                                                Location = "file1.txt",
                                                Format = V1.FileFormat.Text,
                                                TextId = "MAT"
                                            }
                                        },
                                        WordAlignOnAll = false,
                                        TrainOnAll = true
                                    }
                                }
                            },
                            TargetCorpora =
                            {
                                new List<V1.MonolingualCorpus>
                                {
                                    new()
                                    {
                                        Id = "corpus1-target1",
                                        Language = "en",
                                        Files =
                                        {
                                            new V1.CorpusFile
                                            {
                                                Location = "file2.txt",
                                                Format = V1.FileFormat.Text,
                                                TextId = "MAT"
                                            }
                                        },
                                        WordAlignOnAll = false,
                                        TrainOnAll = true
                                    }
                                }
                            }
                        },
                        new V1.ParallelCorpus
                        {
                            Id = "corpus2",
                            SourceCorpora =
                            {
                                new List<V1.MonolingualCorpus>
                                {
                                    new()
                                    {
                                        Id = "corpus2-source1",
                                        Language = "es",
                                        Files =
                                        {
                                            new V1.CorpusFile
                                            {
                                                Location = "file3.txt",
                                                Format = V1.FileFormat.Text,
                                                TextId = "MRK"
                                            }
                                        },
                                        WordAlignOnAll = true,
                                        TrainOnAll = false
                                    }
                                }
                            },
                            TargetCorpora =
                            {
                                new List<V1.MonolingualCorpus>
                                {
                                    new()
                                    {
                                        Id = "corpus2-target1",
                                        Language = "en",
                                        Files =
                                        {
                                            new V1.CorpusFile
                                            {
                                                Location = "file4.txt",
                                                Format = V1.FileFormat.Text,
                                                TextId = "MRK"
                                            }
                                        },
                                        WordAlignOnAll = true,
                                        TrainOnAll = false
                                    }
                                }
                            }
                        }
                    }
                }
            );
    }

    [Test]
    public async Task StartBuildAsync_TextFilesScriptureRangeSpecified()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineWithTextFilesAsync()).Id;
        Assert.ThrowsAsync<InvalidOperationException>(
            () =>
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
                                SourceFilters = new List<ParallelCorpusFilter>()
                                {
                                    new()
                                    {
                                        CorpusRef = "corpus1-source1",
                                        ScriptureRange = "MAT",
                                        TextIds = []
                                    }
                                },
                                TargetFilters = new List<ParallelCorpusFilter>()
                                {
                                    new()
                                    {
                                        CorpusRef = "corpus1-target1",
                                        ScriptureRange = "MAT",
                                        TextIds = []
                                    }
                                }
                            }
                        ]
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
                        SourceFilters = new List<ParallelCorpusFilter>()
                        {
                            new() { CorpusRef = "corpus1-source1", ScriptureRange = "MAT 1;MRK" }
                        },
                        TargetFilters = new List<ParallelCorpusFilter>()
                        {
                            new() { CorpusRef = "corpus1-target1", ScriptureRange = "MAT;MRK 1" }
                        }
                    }
                ]
            }
        );
        _ = env.WordAlignmentServiceClient.Received()
            .StartBuildAsync(
                new StartBuildRequest
                {
                    BuildId = BUILD1_ID,
                    EngineId = engineId,
                    EngineType = "Statistical",
                    Corpora =
                    {
                        new V1.ParallelCorpus
                        {
                            Id = "corpus1",
                            SourceCorpora =
                            {
                                new List<V1.MonolingualCorpus>
                                {
                                    new()
                                    {
                                        Id = "corpus1-source1",
                                        Language = "es",
                                        TrainOnChapters =
                                        {
                                            {
                                                "MAT",
                                                new ScriptureChapters { Chapters = { 1 } }
                                            },
                                            {
                                                "MRK",
                                                new ScriptureChapters { Chapters = { } }
                                            }
                                        },
                                        Files =
                                        {
                                            new V1.CorpusFile
                                            {
                                                Location = "file1.zip",
                                                Format = V1.FileFormat.Paratext,
                                                TextId = "file1.zip"
                                            }
                                        },
                                        WordAlignOnAll = true,
                                        TrainOnAll = false
                                    }
                                }
                            },
                            TargetCorpora =
                            {
                                new List<V1.MonolingualCorpus>
                                {
                                    new()
                                    {
                                        Id = "corpus1-target1",
                                        Language = "en",
                                        TrainOnChapters =
                                        {
                                            {
                                                "MAT",
                                                new ScriptureChapters { Chapters = { } }
                                            },
                                            {
                                                "MRK",
                                                new ScriptureChapters { Chapters = { 1 } }
                                            }
                                        },
                                        Files =
                                        {
                                            new V1.CorpusFile
                                            {
                                                Location = "file2.zip",
                                                Format = V1.FileFormat.Paratext,
                                                TextId = "file2.zip"
                                            }
                                        },
                                        WordAlignOnAll = true,
                                        TrainOnAll = false
                                    }
                                }
                            }
                        }
                    }
                }
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
                        SourceFilters = new List<ParallelCorpusFilter>()
                        {
                            new() { CorpusRef = "corpus1-source1", ScriptureRange = "" }
                        },
                        TargetFilters = new List<ParallelCorpusFilter>()
                        {
                            new() { CorpusRef = "corpus1-target1", ScriptureRange = "" }
                        }
                    }
                ]
            }
        );
        _ = env.WordAlignmentServiceClient.Received()
            .StartBuildAsync(
                new StartBuildRequest
                {
                    BuildId = BUILD1_ID,
                    EngineId = engineId,
                    EngineType = "Statistical",
                    Corpora =
                    {
                        new V1.ParallelCorpus
                        {
                            Id = "corpus1",
                            SourceCorpora =
                            {
                                new List<V1.MonolingualCorpus>
                                {
                                    new()
                                    {
                                        Id = "corpus1-source1",
                                        Language = "es",
                                        Files =
                                        {
                                            new V1.CorpusFile
                                            {
                                                Location = "file1.zip",
                                                Format = V1.FileFormat.Paratext,
                                                TextId = "file1.zip"
                                            }
                                        },
                                        WordAlignOnAll = true,
                                        TrainOnAll = false
                                    }
                                }
                            },
                            TargetCorpora =
                            {
                                new List<V1.MonolingualCorpus>
                                {
                                    new()
                                    {
                                        Id = "corpus1-target1",
                                        Language = "en",
                                        Files =
                                        {
                                            new V1.CorpusFile
                                            {
                                                Location = "file2.zip",
                                                Format = V1.FileFormat.Paratext,
                                                TextId = "file2.zip"
                                            }
                                        },
                                        WordAlignOnAll = true,
                                        TrainOnAll = false
                                    }
                                }
                            }
                        }
                    }
                }
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
                        SourceFilters = new List<ParallelCorpusFilter>()
                        {
                            new() { CorpusRef = "corpus1-source1", ScriptureRange = "MAT 1-2;MRK 1-2" },
                            new() { CorpusRef = "corpus1-source2", ScriptureRange = "MAT 3;MRK 1" }
                        },
                        TargetFilters = new List<ParallelCorpusFilter>()
                        {
                            new() { CorpusRef = "corpus1-target1", ScriptureRange = "MAT 2-3;MRK 2" },
                            new() { CorpusRef = "corpus1-target2", ScriptureRange = "MAT 1;MRK 1-2" }
                        }
                    }
                ]
            }
        );
        _ = env.WordAlignmentServiceClient.Received()
            .StartBuildAsync(
                new StartBuildRequest
                {
                    BuildId = BUILD1_ID,
                    EngineId = engineId,
                    EngineType = "Statistical",
                    Corpora =
                    {
                        new V1.ParallelCorpus
                        {
                            Id = "corpus1",
                            SourceCorpora =
                            {
                                new V1.MonolingualCorpus()
                                {
                                    Id = "corpus1-source1",
                                    Language = "es",
                                    Files =
                                    {
                                        new V1.CorpusFile
                                        {
                                            Location = "file1.zip",
                                            Format = V1.FileFormat.Paratext,
                                            TextId = "file1.zip"
                                        }
                                    },
                                    TrainOnChapters =
                                    {
                                        {
                                            "MAT",
                                            new ScriptureChapters { Chapters = { 1, 2 } }
                                        },
                                        {
                                            "MRK",
                                            new ScriptureChapters { Chapters = { 1, 2 } }
                                        }
                                    },
                                    WordAlignOnAll = true,
                                    TrainOnAll = false
                                },
                                new V1.MonolingualCorpus()
                                {
                                    Id = "corpus1-source2",
                                    Language = "es",
                                    Files =
                                    {
                                        new V1.CorpusFile
                                        {
                                            Location = "file3.zip",
                                            Format = V1.FileFormat.Paratext,
                                            TextId = "file3.zip"
                                        }
                                    },
                                    TrainOnChapters =
                                    {
                                        {
                                            "MAT",
                                            new ScriptureChapters { Chapters = { 3 } }
                                        },
                                        {
                                            "MRK",
                                            new ScriptureChapters { Chapters = { 1 } }
                                        }
                                    },
                                    WordAlignOnAll = true,
                                    TrainOnAll = false
                                }
                            },
                            TargetCorpora =
                            {
                                new V1.MonolingualCorpus()
                                {
                                    Id = "corpus1-target1",
                                    Language = "en",
                                    Files =
                                    {
                                        new V1.CorpusFile
                                        {
                                            Location = "file2.zip",
                                            Format = V1.FileFormat.Paratext,
                                            TextId = "file2.zip"
                                        }
                                    },
                                    TrainOnChapters =
                                    {
                                        {
                                            "MAT",
                                            new ScriptureChapters { Chapters = { 2, 3 } }
                                        },
                                        {
                                            "MRK",
                                            new ScriptureChapters { Chapters = { 2 } }
                                        }
                                    },
                                    WordAlignOnAll = true,
                                    TrainOnAll = false
                                },
                                new V1.MonolingualCorpus()
                                {
                                    Id = "corpus1-target2",
                                    Language = "en",
                                    Files =
                                    {
                                        new V1.CorpusFile
                                        {
                                            Location = "file4.zip",
                                            Format = V1.FileFormat.Paratext,
                                            TextId = "file4.zip"
                                        }
                                    },
                                    TrainOnChapters =
                                    {
                                        {
                                            "MAT",
                                            new ScriptureChapters { Chapters = { 1 } }
                                        },
                                        {
                                            "MRK",
                                            new ScriptureChapters { Chapters = { 1, 2 } }
                                        }
                                    },
                                    WordAlignOnAll = true,
                                    TrainOnAll = false
                                }
                            }
                        }
                    }
                }
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
                        SourceFilters = new List<ParallelCorpusFilter>()
                        {
                            new() { CorpusRef = "corpus1-source1", ScriptureRange = "MAT 1;MRK" }
                        },
                    }
                ]
            }
        );
        _ = env.WordAlignmentServiceClient.Received()
            .StartBuildAsync(
                new StartBuildRequest
                {
                    BuildId = BUILD1_ID,
                    EngineId = engineId,
                    EngineType = "Statistical",
                    Corpora =
                    {
                        new V1.ParallelCorpus
                        {
                            Id = "corpus1",
                            SourceCorpora =
                            {
                                new V1.MonolingualCorpus()
                                {
                                    Id = "corpus1-source1",
                                    Language = "es",
                                    Files =
                                    {
                                        new V1.CorpusFile
                                        {
                                            Location = "file1.zip",
                                            Format = V1.FileFormat.Paratext,
                                            TextId = "file1.zip"
                                        }
                                    },
                                    TrainOnChapters =
                                    {
                                        {
                                            "MAT",
                                            new ScriptureChapters { Chapters = { 1 } }
                                        },
                                        {
                                            "MRK",
                                            new ScriptureChapters { }
                                        }
                                    },
                                    WordAlignOnAll = true,
                                    TrainOnAll = false
                                },
                                new V1.MonolingualCorpus()
                                {
                                    Id = "corpus1-source2",
                                    Language = "es",
                                    Files =
                                    {
                                        new V1.CorpusFile
                                        {
                                            Location = "file3.zip",
                                            Format = V1.FileFormat.Paratext,
                                            TextId = "file3.zip"
                                        }
                                    },
                                    WordAlignOnAll = true,
                                    TrainOnAll = false
                                }
                            },
                            TargetCorpora =
                            {
                                new V1.MonolingualCorpus()
                                {
                                    Id = "corpus1-target1",
                                    Language = "en",
                                    Files =
                                    {
                                        new V1.CorpusFile
                                        {
                                            Location = "file2.zip",
                                            Format = V1.FileFormat.Paratext,
                                            TextId = "file2.zip"
                                        }
                                    },
                                    WordAlignOnAll = true,
                                    TrainOnAll = true
                                },
                                new V1.MonolingualCorpus()
                                {
                                    Id = "corpus1-target2",
                                    Language = "en",
                                    Files =
                                    {
                                        new V1.CorpusFile
                                        {
                                            Location = "file4.zip",
                                            Format = V1.FileFormat.Paratext,
                                            TextId = "file4.zip"
                                        }
                                    },
                                    WordAlignOnAll = true,
                                    TrainOnAll = true
                                }
                            }
                        }
                    }
                }
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

        Shared.Models.ParallelCorpus? corpus = await env.Service.UpdateParallelCorpusAsync(
            engine.Id,
            corpusId,
            sourceCorpora: new List<Shared.Models.MonolingualCorpus>
            {
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
                            Format = Shared.Contracts.FileFormat.Text,
                            TextId = "text1"
                        }
                    ]
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
                            Format = Shared.Contracts.FileFormat.Text,
                            TextId = "text2"
                        }
                    ]
                }
            },
            null
        );

        Assert.That(corpus, Is.Not.Null);
        Assert.That(corpus.SourceCorpora, Has.Count.EqualTo(2));
        Assert.That(corpus.SourceCorpora[0].Files[0].Id, Is.EqualTo("file1"));
        Assert.That(corpus.SourceCorpora[1].Files[0].Id, Is.EqualTo("file3"));
        Assert.That(corpus.TargetCorpora, Has.Count.EqualTo(1));
    }

    private class TestEnvironment
    {
        public TestEnvironment()
        {
            Engines = new MemoryRepository<Engine>();
            WordAlignmentServiceClient = Substitute.For<WordAlignmentEngineApi.WordAlignmentEngineApiClient>();
            var wordAlignmentResult = new V1.WordAlignmentResult
            {
                SourceTokens = { "esto es una prueba .".Split() },
                TargetTokens = { "this is a test .".Split() },
                Alignment =
                {
                    new V1.AlignedWordPair { SourceIndex = 0, TargetIndex = 0 },
                    new V1.AlignedWordPair { SourceIndex = 1, TargetIndex = 1 },
                    new V1.AlignedWordPair { SourceIndex = 2, TargetIndex = 2 },
                    new V1.AlignedWordPair { SourceIndex = 3, TargetIndex = 3 },
                    new V1.AlignedWordPair { SourceIndex = 4, TargetIndex = 4 }
                },
            };
            var wordAlignmentResponse = new GetWordAlignmentResponse { Result = wordAlignmentResult };
            WordAlignmentServiceClient
                .GetWordAlignmentAsync(Arg.Any<GetWordAlignmentRequest>())
                .Returns(CreateAsyncUnaryCall(wordAlignmentResponse));
            WordAlignmentServiceClient
                .CancelBuildAsync(Arg.Any<CancelBuildRequest>())
                .Returns(CreateAsyncUnaryCall(new Empty()));
            WordAlignmentServiceClient.CreateAsync(Arg.Any<CreateRequest>()).Returns(CreateAsyncUnaryCall(new Empty()));
            WordAlignmentServiceClient.DeleteAsync(Arg.Any<DeleteRequest>()).Returns(CreateAsyncUnaryCall(new Empty()));
            WordAlignmentServiceClient
                .StartBuildAsync(Arg.Any<StartBuildRequest>())
                .Returns(CreateAsyncUnaryCall(new Empty()));
            GrpcClientFactory grpcClientFactory = Substitute.For<GrpcClientFactory>();
            grpcClientFactory
                .CreateClient<WordAlignmentEngineApi.WordAlignmentEngineApiClient>("Statistical")
                .Returns(WordAlignmentServiceClient);
            IOptionsMonitor<DataFileOptions> dataFileOptions = Substitute.For<IOptionsMonitor<DataFileOptions>>();
            dataFileOptions.CurrentValue.Returns(new DataFileOptions());
            var scriptureDataFileService = Substitute.For<IScriptureDataFileService>();
            scriptureDataFileService
                .GetParatextProjectSettings(Arg.Any<string>())
                .Returns(
                    new ParatextProjectSettings(
                        name: "Tst",
                        fullName: "Test",
                        encoding: Encoding.UTF8,
                        versification: ScrVers.English,
                        stylesheet: new UsfmStylesheet("usfm.sty"),
                        fileNamePrefix: "TST",
                        fileNameForm: "MAT",
                        fileNameSuffix: ".USFM",
                        biblicalTermsListType: "BiblicalTerms",
                        biblicalTermsProjectName: "",
                        biblicalTermsFileName: "BiblicalTerms.xml",
                        languageCode: "en"
                    )
                );

            Service = new EngineService(
                Engines,
                new MemoryRepository<Build>(),
                new MemoryRepository<Models.WordAlignment>(),
                grpcClientFactory,
                dataFileOptions,
                new MemoryDataAccessContext(),
                new LoggerFactory(),
                scriptureDataFileService
            );
        }

        public EngineService Service { get; }
        public IRepository<Engine> Engines { get; }
        public WordAlignmentEngineApi.WordAlignmentEngineApiClient WordAlignmentServiceClient { get; }

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
                        SourceCorpora = new List<Shared.Models.MonolingualCorpus>()
                        {
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
                                        Format = Shared.Contracts.FileFormat.Text,
                                        TextId = "text1"
                                    }
                                ]
                            }
                        },
                        TargetCorpora = new List<Shared.Models.MonolingualCorpus>()
                        {
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
                                        Format = Shared.Contracts.FileFormat.Text,
                                        TextId = "text1"
                                    }
                                ]
                            }
                        }
                    }
                ]
            };
            await Engines.InsertAsync(engine);
            return engine;
        }

        public async Task<Engine> CreateEngineWithMulitipleTextFilesAsync()
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
                        SourceCorpora = new List<Shared.Models.MonolingualCorpus>()
                        {
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
                                        Format = Shared.Contracts.FileFormat.Text,
                                        TextId = "MAT"
                                    }
                                ]
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
                                        Format = Shared.Contracts.FileFormat.Text,
                                        TextId = "MRK"
                                    }
                                ]
                            }
                        },
                        TargetCorpora = new List<Shared.Models.MonolingualCorpus>()
                        {
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
                                        Format = Shared.Contracts.FileFormat.Text,
                                        TextId = "MAT"
                                    }
                                ]
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
                                        Format = Shared.Contracts.FileFormat.Text,
                                        TextId = "MRK"
                                    }
                                ]
                            }
                        }
                    }
                ]
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
                        SourceCorpora = new List<Shared.Models.MonolingualCorpus>()
                        {
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
                                        Format = Shared.Contracts.FileFormat.Text,
                                        TextId = "MAT"
                                    }
                                ]
                            }
                        },
                        TargetCorpora = new List<Shared.Models.MonolingualCorpus>()
                        {
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
                                        Format = Shared.Contracts.FileFormat.Text,
                                        TextId = "MAT"
                                    }
                                ]
                            }
                        }
                    },
                    new()
                    {
                        Id = "corpus2",
                        SourceCorpora = new List<Shared.Models.MonolingualCorpus>()
                        {
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
                                        Format = Shared.Contracts.FileFormat.Text,
                                        TextId = "MRK"
                                    }
                                ]
                            }
                        },
                        TargetCorpora = new List<Shared.Models.MonolingualCorpus>()
                        {
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
                                        Format = Shared.Contracts.FileFormat.Text,
                                        TextId = "MRK"
                                    }
                                ]
                            }
                        }
                    }
                ]
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
                        SourceCorpora = new List<Shared.Models.MonolingualCorpus>()
                        {
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
                                        Format = Shared.Contracts.FileFormat.Paratext,
                                        TextId = "file1.zip"
                                    }
                                ]
                            }
                        },
                        TargetCorpora = new List<Shared.Models.MonolingualCorpus>()
                        {
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
                                        Format = Shared.Contracts.FileFormat.Paratext,
                                        TextId = "file2.zip"
                                    }
                                ]
                            }
                        }
                    }
                ]
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
                        SourceCorpora = new List<Shared.Models.MonolingualCorpus>()
                        {
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
                                        Format = Shared.Contracts.FileFormat.Paratext,
                                        TextId = "file1.zip"
                                    }
                                ]
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
                                        Format = Shared.Contracts.FileFormat.Paratext,
                                        TextId = "file3.zip"
                                    }
                                ]
                            }
                        },
                        TargetCorpora = new List<Shared.Models.MonolingualCorpus>()
                        {
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
                                        Format = Shared.Contracts.FileFormat.Paratext,
                                        TextId = "file2.zip"
                                    }
                                ]
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
                                        Format = Shared.Contracts.FileFormat.Paratext,
                                        TextId = "file4.zip"
                                    }
                                ]
                            }
                        }
                    }
                ]
            };
            await Engines.InsertAsync(engine);
            return engine;
        }

        private static AsyncUnaryCall<TResponse> CreateAsyncUnaryCall<TResponse>(TResponse response)
        {
            return new AsyncUnaryCall<TResponse>(
                Task.FromResult(response),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { }
            );
        }
    }

    private static IReadOnlyList<Models.AlignedWordPair> CreateNAlignedWordPair(int numberOfAlignedWords)
    {
        var alignedWordPairs = new List<Models.AlignedWordPair>();
        for (int i = 0; i < numberOfAlignedWords; i++)
        {
            alignedWordPairs.Add(new Models.AlignedWordPair { SourceIndex = i, TargetIndex = i });
        }
        return alignedWordPairs;
    }
}
