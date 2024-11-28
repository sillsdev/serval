using Google.Protobuf.WellKnownTypes;
using MassTransit.Mediator;
using Serval.Translation.V1;

namespace Serval.Translation.Services;

[TestFixture]
public class EngineServiceTests
{
    const string BUILD1_ID = "b00000000000000000000001";

    [Test]
    public void TranslateAsync_EngineDoesNotExist()
    {
        var env = new TestEnvironment();
        Assert.ThrowsAsync<EntityNotFoundException>(() => env.Service.TranslateAsync("engine1", "esto es una prueba."));
    }

    [Test]
    public async Task TranslateAsync_EngineExists()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineWithTextFilesAsync()).Id;
        Models.TranslationResult? result = await env.Service.TranslateAsync(engineId, "esto es una prueba.");
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Translation, Is.EqualTo("this is a test."));
    }

    [Test]
    public void GetWordGraphAsync_EngineDoesNotExist()
    {
        var env = new TestEnvironment();
        Assert.ThrowsAsync<EntityNotFoundException>(
            () => env.Service.GetWordGraphAsync("engine1", "esto es una prueba.")
        );
    }

    [Test]
    public async Task GetWordGraphAsync_EngineExists()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineWithTextFilesAsync()).Id;
        Models.WordGraph? result = await env.Service.GetWordGraphAsync(engineId, "esto es una prueba.");
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Arcs.SelectMany(a => a.TargetTokens), Is.EqualTo("this is a test .".Split()));
    }

    [Test]
    public void TrainSegmentAsync_EngineDoesNotExist()
    {
        var env = new TestEnvironment();
        Assert.ThrowsAsync<EntityNotFoundException>(
            () => env.Service.TrainSegmentPairAsync("engine1", "esto es una prueba.", "this is a test.", true)
        );
    }

    [Test]
    public async Task TrainSegmentAsync_EngineExists()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineWithTextFilesAsync()).Id;
        Assert.DoesNotThrowAsync(
            () => env.Service.TrainSegmentPairAsync(engineId, "esto es una prueba.", "this is a test.", true)
        );
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
                Type = "Smt",
                Corpora = []
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
        _ = env.TranslationServiceClient.Received()
            .StartBuildAsync(
                new StartBuildRequest
                {
                    BuildId = BUILD1_ID,
                    EngineId = engineId,
                    EngineType = "Smt",
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
                                        PretranslateAll = true,
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
                                        PretranslateAll = true,
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
                TrainOn = [new TrainingCorpus { CorpusRef = "corpus1", TextIds = [] }]
            }
        );
        _ = env.TranslationServiceClient.Received()
            .StartBuildAsync(
                new StartBuildRequest
                {
                    BuildId = BUILD1_ID,
                    EngineId = engineId,
                    EngineType = "Smt",
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
                                        PretranslateAll = true,
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
                                        PretranslateAll = true,
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
                TrainOn = [new TrainingCorpus { CorpusRef = "corpus1", TextIds = ["text1"] }]
            }
        );
        _ = env.TranslationServiceClient.Received()
            .StartBuildAsync(
                new StartBuildRequest
                {
                    BuildId = BUILD1_ID,
                    EngineId = engineId,
                    EngineType = "Smt",
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
                                        PretranslateAll = true,
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
                                        PretranslateAll = true,
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
                TrainOn = [new TrainingCorpus { CorpusRef = "corpus1" }]
            }
        );
        _ = env.TranslationServiceClient.Received()
            .StartBuildAsync(
                new StartBuildRequest
                {
                    BuildId = BUILD1_ID,
                    EngineId = engineId,
                    EngineType = "Smt",
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
                                        PretranslateAll = true,
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
                                        PretranslateAll = true,
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
                TrainOn = [new TrainingCorpus { CorpusRef = "corpus1" }],
                Pretranslate = [new PretranslateCorpus { CorpusRef = "corpus1" }]
            }
        );
        _ = env.TranslationServiceClient.Received()
            .StartBuildAsync(
                new StartBuildRequest
                {
                    BuildId = BUILD1_ID,
                    EngineId = engineId,
                    EngineType = "Smt",
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
                                        PretranslateAll = true,
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
                                        PretranslateAll = true,
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
    public async Task StartBuildAsync_TrainOnOnePretranslateTheOther()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateMultipleCorporaEngineWithTextFilesAsync()).Id;
        await env.Service.StartBuildAsync(
            new Build
            {
                Id = BUILD1_ID,
                EngineRef = engineId,
                TrainOn = [new TrainingCorpus { CorpusRef = "corpus1" }],
                Pretranslate = [new PretranslateCorpus { CorpusRef = "corpus2" }]
            }
        );
        _ = env.TranslationServiceClient.Received()
            .StartBuildAsync(
                new StartBuildRequest
                {
                    BuildId = BUILD1_ID,
                    EngineId = engineId,
                    EngineType = "Smt",
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
                                        PretranslateAll = false,
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
                                        PretranslateAll = false,
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
                                        Language = "es",
                                        Files =
                                        {
                                            new V1.CorpusFile
                                            {
                                                Location = "file3.txt",
                                                Format = V1.FileFormat.Text,
                                                TextId = "text1"
                                            }
                                        },
                                        PretranslateAll = true,
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
                                        Language = "en",
                                        Files =
                                        {
                                            new V1.CorpusFile
                                            {
                                                Location = "file4.txt",
                                                Format = V1.FileFormat.Text,
                                                TextId = "text1"
                                            }
                                        },
                                        PretranslateAll = true,
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
                        TrainOn = [new TrainingCorpus { CorpusRef = "corpus1", ScriptureRange = "MAT" }]
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
                TrainOn = [new TrainingCorpus { CorpusRef = "corpus1", ScriptureRange = "MAT 1;MRK" }]
            }
        );
        _ = env.TranslationServiceClient.Received()
            .StartBuildAsync(
                new StartBuildRequest
                {
                    BuildId = BUILD1_ID,
                    EngineId = engineId,
                    EngineType = "Smt",
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
                                        PretranslateAll = true,
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
                                        Language = "en",
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
                                                Location = "file2.zip",
                                                Format = V1.FileFormat.Paratext,
                                                TextId = "file2.zip"
                                            }
                                        },
                                        PretranslateAll = true,
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
                TrainOn = [new TrainingCorpus { CorpusRef = "corpus1", ScriptureRange = "" }]
            }
        );
        _ = env.TranslationServiceClient.Received()
            .StartBuildAsync(
                new StartBuildRequest
                {
                    BuildId = BUILD1_ID,
                    EngineId = engineId,
                    EngineType = "Smt",
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
                                        PretranslateAll = true,
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
                                        PretranslateAll = true,
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
    public async Task StartBuildAsync_ParallelCorpus_TextFiles()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateParallelCorpusEngineWithTextFilesAsync()).Id;
        await env.Service.StartBuildAsync(
            new Build
            {
                Id = BUILD1_ID,
                EngineRef = engineId,
                TrainOn =
                [
                    new TrainingCorpus
                    {
                        ParallelCorpusRef = "parallel-corpus1",
                        SourceFilters = new List<ParallelCorpusFilter>()
                        {
                            new()
                            {
                                CorpusRef = "parallel-corpus1-source1",
                                TextIds = new List<string> { "MAT" }
                            }
                        },
                        TargetFilters = new List<ParallelCorpusFilter>()
                        {
                            new()
                            {
                                CorpusRef = "parallel-corpus1-target1",
                                TextIds = new List<string> { "MAT" }
                            }
                        }
                    }
                ]
            }
        );
        _ = env.TranslationServiceClient.Received()
            .StartBuildAsync(
                new StartBuildRequest
                {
                    BuildId = BUILD1_ID,
                    EngineId = engineId,
                    EngineType = "Smt",
                    Corpora =
                    {
                        new V1.ParallelCorpus
                        {
                            Id = "parallel-corpus1",
                            SourceCorpora =
                            {
                                new List<V1.MonolingualCorpus>
                                {
                                    new()
                                    {
                                        Id = "parallel-corpus1-source1",
                                        Language = "es",
                                        TrainOnTextIds = { "MAT" },
                                        Files =
                                        {
                                            new V1.CorpusFile
                                            {
                                                Location = "file1.txt",
                                                Format = V1.FileFormat.Text,
                                                TextId = "MAT"
                                            }
                                        },
                                        PretranslateAll = true,
                                        TrainOnAll = false
                                    },
                                    new()
                                    {
                                        Id = "parallel-corpus1-source2",
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
                                        PretranslateAll = true,
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
                                        Id = "parallel-corpus1-target1",
                                        Language = "en",
                                        TrainOnTextIds = { "MAT" },
                                        Files =
                                        {
                                            new V1.CorpusFile
                                            {
                                                Location = "file2.txt",
                                                Format = V1.FileFormat.Text,
                                                TextId = "MAT"
                                            }
                                        },
                                        PretranslateAll = true,
                                        TrainOnAll = false
                                    },
                                    new()
                                    {
                                        Id = "parallel-corpus1-target2",
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
                                        PretranslateAll = true,
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
    public async Task StartBuildAsync_ParallelCorpus_OneOfMultipleCorpora()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateMultipleParallelCorpusEngineWithTextFilesAsync()).Id;
        await env.Service.StartBuildAsync(
            new Build
            {
                Id = BUILD1_ID,
                EngineRef = engineId,
                TrainOn =
                [
                    new TrainingCorpus
                    {
                        ParallelCorpusRef = "parallel-corpus1",
                        SourceFilters = new List<ParallelCorpusFilter>()
                        {
                            new()
                            {
                                CorpusRef = "parallel-corpus1-source1",
                                TextIds = new List<string> { "MAT" }
                            }
                        },
                        TargetFilters = new List<ParallelCorpusFilter>()
                        {
                            new()
                            {
                                CorpusRef = "parallel-corpus1-target1",
                                TextIds = new List<string> { "MAT" }
                            }
                        }
                    }
                ],
                Pretranslate = [new PretranslateCorpus { ParallelCorpusRef = "parallel-corpus1" }]
            }
        );
        _ = env.TranslationServiceClient.Received()
            .StartBuildAsync(
                new StartBuildRequest
                {
                    BuildId = BUILD1_ID,
                    EngineId = engineId,
                    EngineType = "Smt",
                    Corpora =
                    {
                        new V1.ParallelCorpus
                        {
                            Id = "parallel-corpus1",
                            SourceCorpora =
                            {
                                new List<V1.MonolingualCorpus>
                                {
                                    new()
                                    {
                                        Id = "parallel-corpus1-source1",
                                        Language = "es",
                                        TrainOnTextIds = { "MAT" },
                                        Files =
                                        {
                                            new V1.CorpusFile
                                            {
                                                Location = "file1.txt",
                                                Format = V1.FileFormat.Text,
                                                TextId = "MAT"
                                            }
                                        },
                                        PretranslateAll = true,
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
                                        Id = "parallel-corpus1-target1",
                                        Language = "en",
                                        TrainOnTextIds = { "MAT" },
                                        Files =
                                        {
                                            new V1.CorpusFile
                                            {
                                                Location = "file2.txt",
                                                Format = V1.FileFormat.Text,
                                                TextId = "MAT"
                                            }
                                        },
                                        PretranslateAll = true,
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
    public async Task StartBuildAsync_ParallelCorpus_TrainOnOnePretranslateTheOther()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateMultipleParallelCorpusEngineWithTextFilesAsync()).Id;
        await env.Service.StartBuildAsync(
            new Build
            {
                Id = BUILD1_ID,
                EngineRef = engineId,
                TrainOn =
                [
                    new TrainingCorpus
                    {
                        ParallelCorpusRef = "parallel-corpus1",
                        SourceFilters = new List<ParallelCorpusFilter>()
                        {
                            new()
                            {
                                CorpusRef = "parallel-corpus1-source1",
                                TextIds = new List<string> { "MAT" }
                            }
                        },
                        TargetFilters = new List<ParallelCorpusFilter>()
                        {
                            new()
                            {
                                CorpusRef = "parallel-corpus1-target1",
                                TextIds = new List<string> { "MAT" }
                            }
                        }
                    }
                ],
                Pretranslate = [new PretranslateCorpus { ParallelCorpusRef = "parallel-corpus2" }]
            }
        );
        _ = env.TranslationServiceClient.Received()
            .StartBuildAsync(
                new StartBuildRequest
                {
                    BuildId = BUILD1_ID,
                    EngineId = engineId,
                    EngineType = "Smt",
                    Corpora =
                    {
                        new V1.ParallelCorpus
                        {
                            Id = "parallel-corpus1",
                            SourceCorpora =
                            {
                                new List<V1.MonolingualCorpus>
                                {
                                    new()
                                    {
                                        Id = "parallel-corpus1-source1",
                                        Language = "es",
                                        TrainOnTextIds = { "MAT" },
                                        Files =
                                        {
                                            new V1.CorpusFile
                                            {
                                                Location = "file1.txt",
                                                Format = V1.FileFormat.Text,
                                                TextId = "MAT"
                                            }
                                        },
                                        PretranslateAll = false,
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
                                        Id = "parallel-corpus1-target1",
                                        Language = "en",
                                        TrainOnTextIds = { "MAT" },
                                        Files =
                                        {
                                            new V1.CorpusFile
                                            {
                                                Location = "file2.txt",
                                                Format = V1.FileFormat.Text,
                                                TextId = "MAT"
                                            }
                                        },
                                        PretranslateAll = false,
                                        TrainOnAll = false
                                    }
                                }
                            }
                        },
                        new V1.ParallelCorpus
                        {
                            Id = "parallel-corpus2",
                            SourceCorpora =
                            {
                                new List<V1.MonolingualCorpus>
                                {
                                    new()
                                    {
                                        Id = "parallel-corpus2-source1",
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
                                        PretranslateAll = true,
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
                                        Id = "parallel-corpus2-target1",
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
                                        PretranslateAll = true,
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
    public async Task StartBuildAsync_TextIds_ParallelCorpus()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateParallelCorpusEngineWithParatextProjectAsync()).Id;
        await env.Service.StartBuildAsync(
            new Build
            {
                Id = BUILD1_ID,
                EngineRef = engineId,
                TrainOn =
                [
                    new TrainingCorpus
                    {
                        ParallelCorpusRef = "parallel-corpus1",
                        SourceFilters = new List<ParallelCorpusFilter>()
                        {
                            new()
                            {
                                CorpusRef = "parallel-corpus1-source1",
                                TextIds = new List<string>() { "MAT", "MRK" }
                            }
                        },
                        TargetFilters = new List<ParallelCorpusFilter>()
                        {
                            new()
                            {
                                CorpusRef = "parallel-corpus1-target1",
                                TextIds = new List<string>() { "MAT", "MRK" }
                            }
                        }
                    }
                ]
            }
        );
        _ = env.TranslationServiceClient.Received()
            .StartBuildAsync(
                new StartBuildRequest
                {
                    BuildId = BUILD1_ID,
                    EngineId = engineId,
                    EngineType = "Smt",
                    Corpora =
                    {
                        new V1.ParallelCorpus
                        {
                            Id = "parallel-corpus1",
                            SourceCorpora =
                            {
                                new List<V1.MonolingualCorpus>()
                                {
                                    new()
                                    {
                                        Id = "parallel-corpus1-source1",
                                        Language = "es",
                                        TrainOnTextIds = { "MAT", "MRK" },
                                        Files =
                                        {
                                            new V1.CorpusFile
                                            {
                                                Location = "file1.zip",
                                                Format = V1.FileFormat.Paratext,
                                                TextId = "file1.zip"
                                            }
                                        },
                                        PretranslateAll = true,
                                        TrainOnAll = false
                                    },
                                    new()
                                    {
                                        Id = "parallel-corpus1-source2",
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
                                        PretranslateAll = true,
                                        TrainOnAll = false
                                    }
                                }
                            },
                            TargetCorpora =
                            {
                                new List<V1.MonolingualCorpus>()
                                {
                                    new()
                                    {
                                        Id = "parallel-corpus1-target1",
                                        Language = "en",
                                        TrainOnTextIds = { "MAT", "MRK" },
                                        Files =
                                        {
                                            new V1.CorpusFile
                                            {
                                                Location = "file2.zip",
                                                Format = V1.FileFormat.Paratext,
                                                TextId = "file2.zip"
                                            }
                                        },
                                        PretranslateAll = true,
                                        TrainOnAll = false
                                    },
                                    new()
                                    {
                                        Id = "parallel-corpus1-target2",
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
                                        PretranslateAll = true,
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
    public async Task StartBuildAsync_ScriptureRange_ParallelCorpus()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateParallelCorpusEngineWithParatextProjectAsync()).Id;
        await env.Service.StartBuildAsync(
            new Build
            {
                Id = BUILD1_ID,
                EngineRef = engineId,
                TrainOn =
                [
                    new TrainingCorpus
                    {
                        ParallelCorpusRef = "parallel-corpus1",
                        SourceFilters = new List<ParallelCorpusFilter>()
                        {
                            new() { CorpusRef = "parallel-corpus1-source1", ScriptureRange = "MAT 1;MRK" }
                        },
                        TargetFilters = new List<ParallelCorpusFilter>()
                        {
                            new() { CorpusRef = "parallel-corpus1-target1", ScriptureRange = "MAT 1;MRK" }
                        }
                    }
                ],
                Pretranslate =
                [
                    new PretranslateCorpus
                    {
                        ParallelCorpusRef = "parallel-corpus1",
                        SourceFilters = new List<ParallelCorpusFilter>()
                        {
                            new() { CorpusRef = "parallel-corpus1-source1", ScriptureRange = "MAT 2" }
                        }
                    }
                ]
            }
        );
        _ = env.TranslationServiceClient.Received()
            .StartBuildAsync(
                new StartBuildRequest
                {
                    BuildId = BUILD1_ID,
                    EngineId = engineId,
                    EngineType = "Smt",
                    Corpora =
                    {
                        new V1.ParallelCorpus
                        {
                            Id = "parallel-corpus1",
                            SourceCorpora =
                            {
                                new List<V1.MonolingualCorpus>()
                                {
                                    new()
                                    {
                                        Id = "parallel-corpus1-source1",
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
                                        PretranslateChapters =
                                        {
                                            {
                                                "MAT",
                                                new ScriptureChapters { Chapters = { 2 } }
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
                                        PretranslateAll = false,
                                        TrainOnAll = false
                                    },
                                    new()
                                    {
                                        Id = "parallel-corpus1-source2",
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
                                        PretranslateAll = false,
                                        TrainOnAll = false
                                    }
                                }
                            },
                            TargetCorpora =
                            {
                                new List<V1.MonolingualCorpus>()
                                {
                                    new()
                                    {
                                        Id = "parallel-corpus1-target1",
                                        Language = "en",
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
                                        Files =
                                        {
                                            new V1.CorpusFile
                                            {
                                                Location = "file2.zip",
                                                Format = V1.FileFormat.Paratext,
                                                TextId = "file2.zip"
                                            }
                                        },
                                        PretranslateAll = true,
                                        TrainOnAll = false
                                    },
                                    new()
                                    {
                                        Id = "parallel-corpus1-target2",
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
                                        PretranslateAll = true,
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
    public async Task StartBuildAsync_MixedSourceAndTarget_ParallelCorpus()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateParallelCorpusEngineWithParatextProjectAsync()).Id;
        await env.Service.StartBuildAsync(
            new Build
            {
                Id = BUILD1_ID,
                EngineRef = engineId,
                TrainOn =
                [
                    new TrainingCorpus
                    {
                        ParallelCorpusRef = "parallel-corpus1",
                        SourceFilters = new List<ParallelCorpusFilter>()
                        {
                            new() { CorpusRef = "parallel-corpus1-source1", ScriptureRange = "MAT 1-2;MRK 1-2" },
                            new() { CorpusRef = "parallel-corpus1-source2", ScriptureRange = "MAT 3;MRK 1" }
                        },
                        TargetFilters = new List<ParallelCorpusFilter>()
                        {
                            new() { CorpusRef = "parallel-corpus1-target1", ScriptureRange = "MAT 2-3;MRK 2" },
                            new() { CorpusRef = "parallel-corpus1-target2", ScriptureRange = "MAT 1;MRK 1-2" }
                        }
                    }
                ]
            }
        );
        _ = env.TranslationServiceClient.Received()
            .StartBuildAsync(
                new StartBuildRequest
                {
                    BuildId = BUILD1_ID,
                    EngineId = engineId,
                    EngineType = "Smt",
                    Corpora =
                    {
                        new V1.ParallelCorpus
                        {
                            Id = "parallel-corpus1",
                            SourceCorpora =
                            {
                                new V1.MonolingualCorpus()
                                {
                                    Id = "parallel-corpus1-source1",
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
                                    PretranslateAll = true,
                                    TrainOnAll = false
                                },
                                new V1.MonolingualCorpus()
                                {
                                    Id = "parallel-corpus1-source2",
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
                                    PretranslateAll = true,
                                    TrainOnAll = false
                                }
                            },
                            TargetCorpora =
                            {
                                new V1.MonolingualCorpus()
                                {
                                    Id = "parallel-corpus1-target1",
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
                                    PretranslateAll = true,
                                    TrainOnAll = false
                                },
                                new V1.MonolingualCorpus()
                                {
                                    Id = "parallel-corpus1-target2",
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
                                    PretranslateAll = true,
                                    TrainOnAll = false
                                }
                            }
                        }
                    }
                }
            );
    }

    [Test]
    public async Task StartBuildAsync_NoFilters_ParallelCorpus()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateParallelCorpusEngineWithParatextProjectAsync()).Id;
        await env.Service.StartBuildAsync(
            new Build
            {
                Id = BUILD1_ID,
                EngineRef = engineId,
                TrainOn = [new TrainingCorpus { ParallelCorpusRef = "parallel-corpus1" }]
            }
        );
        _ = env.TranslationServiceClient.Received()
            .StartBuildAsync(
                new StartBuildRequest
                {
                    BuildId = BUILD1_ID,
                    EngineId = engineId,
                    EngineType = "Smt",
                    Corpora =
                    {
                        new V1.ParallelCorpus
                        {
                            Id = "parallel-corpus1",
                            SourceCorpora =
                            {
                                new V1.MonolingualCorpus()
                                {
                                    Id = "parallel-corpus1-source1",
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
                                    PretranslateAll = true,
                                    TrainOnAll = true
                                },
                                new V1.MonolingualCorpus()
                                {
                                    Id = "parallel-corpus1-source2",
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
                                    PretranslateAll = true,
                                    TrainOnAll = true
                                }
                            },
                            TargetCorpora =
                            {
                                new V1.MonolingualCorpus()
                                {
                                    Id = "parallel-corpus1-target1",
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
                                    PretranslateAll = true,
                                    TrainOnAll = true
                                },
                                new V1.MonolingualCorpus()
                                {
                                    Id = "parallel-corpus1-target2",
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
                                    PretranslateAll = true,
                                    TrainOnAll = true
                                }
                            }
                        }
                    }
                }
            );
    }

    [Test]
    public async Task StartBuildAsync_TrainOnNotSpecified_ParallelCorpus()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateParallelCorpusEngineWithParatextProjectAsync()).Id;
        await env.Service.StartBuildAsync(new Build { Id = BUILD1_ID, EngineRef = engineId });
        _ = env.TranslationServiceClient.Received()
            .StartBuildAsync(
                new StartBuildRequest
                {
                    BuildId = BUILD1_ID,
                    EngineId = engineId,
                    EngineType = "Smt",
                    Corpora =
                    {
                        new V1.ParallelCorpus
                        {
                            Id = "parallel-corpus1",
                            SourceCorpora =
                            {
                                new V1.MonolingualCorpus()
                                {
                                    Id = "parallel-corpus1-source1",
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
                                    PretranslateAll = true,
                                    TrainOnAll = true
                                },
                                new V1.MonolingualCorpus()
                                {
                                    Id = "parallel-corpus1-source2",
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
                                    PretranslateAll = true,
                                    TrainOnAll = true
                                }
                            },
                            TargetCorpora =
                            {
                                new V1.MonolingualCorpus()
                                {
                                    Id = "parallel-corpus1-target1",
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
                                    PretranslateAll = true,
                                    TrainOnAll = true
                                },
                                new V1.MonolingualCorpus()
                                {
                                    Id = "parallel-corpus1-target2",
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
                                    PretranslateAll = true,
                                    TrainOnAll = true
                                }
                            }
                        }
                    }
                }
            );
    }

    [Test]
    public async Task StartBuildAsync_NoTargetFilter_ParallelCorpus()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateParallelCorpusEngineWithParatextProjectAsync()).Id;
        await env.Service.StartBuildAsync(
            new Build
            {
                Id = BUILD1_ID,
                EngineRef = engineId,
                TrainOn =
                [
                    new TrainingCorpus
                    {
                        ParallelCorpusRef = "parallel-corpus1",
                        SourceFilters = new List<ParallelCorpusFilter>()
                        {
                            new() { CorpusRef = "parallel-corpus1-source1", ScriptureRange = "MAT 1;MRK" }
                        },
                    }
                ]
            }
        );
        _ = env.TranslationServiceClient.Received()
            .StartBuildAsync(
                new StartBuildRequest
                {
                    BuildId = BUILD1_ID,
                    EngineId = engineId,
                    EngineType = "Smt",
                    Corpora =
                    {
                        new V1.ParallelCorpus
                        {
                            Id = "parallel-corpus1",
                            SourceCorpora =
                            {
                                new V1.MonolingualCorpus()
                                {
                                    Id = "parallel-corpus1-source1",
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
                                    PretranslateAll = true,
                                    TrainOnAll = false
                                },
                                new V1.MonolingualCorpus()
                                {
                                    Id = "parallel-corpus1-source2",
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
                                    PretranslateAll = true,
                                    TrainOnAll = false
                                }
                            },
                            TargetCorpora =
                            {
                                new V1.MonolingualCorpus()
                                {
                                    Id = "parallel-corpus1-target1",
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
                                    PretranslateAll = true,
                                    TrainOnAll = true
                                },
                                new V1.MonolingualCorpus()
                                {
                                    Id = "parallel-corpus1-target2",
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
                                    PretranslateAll = true,
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
        string corpusId = engine.Corpora[0].Id;

        Models.Corpus? corpus = await env.Service.UpdateCorpusAsync(
            engine.Id,
            corpusId,
            sourceFiles:
            [
                new()
                {
                    Id = "file1",
                    Format = Shared.Contracts.FileFormat.Text,
                    TextId = "text1"
                },
                new()
                {
                    Id = "file3",
                    Format = Shared.Contracts.FileFormat.Text,
                    TextId = "text2"
                },
            ],
            null
        );

        Assert.That(corpus, Is.Not.Null);
        Assert.That(corpus.SourceFiles, Has.Count.EqualTo(2));
        Assert.That(corpus.SourceFiles[0].Id, Is.EqualTo("file1"));
        Assert.That(corpus.SourceFiles[1].Id, Is.EqualTo("file3"));
        Assert.That(corpus.TargetFiles, Has.Count.EqualTo(1));
    }

    private class TestEnvironment
    {
        public TestEnvironment()
        {
            Engines = new MemoryRepository<Engine>();
            TranslationServiceClient = Substitute.For<TranslationEngineApi.TranslationEngineApiClient>();
            var translationResult = new V1.TranslationResult
            {
                Translation = "this is a test.",
                SourceTokens = { "esto es una prueba .".Split() },
                TargetTokens = { "this is a test .".Split() },
                Confidences = { 1.0, 1.0, 1.0, 1.0, 1.0 },
                Sources =
                {
                    new TranslationSources { Values = { V1.TranslationSource.Primary } },
                    new TranslationSources { Values = { V1.TranslationSource.Primary } },
                    new TranslationSources { Values = { V1.TranslationSource.Primary } },
                    new TranslationSources { Values = { V1.TranslationSource.Primary } },
                    new TranslationSources { Values = { V1.TranslationSource.Primary } }
                },
                Alignment =
                {
                    new V1.AlignedWordPair { SourceIndex = 0, TargetIndex = 0 },
                    new V1.AlignedWordPair { SourceIndex = 1, TargetIndex = 1 },
                    new V1.AlignedWordPair { SourceIndex = 2, TargetIndex = 2 },
                    new V1.AlignedWordPair { SourceIndex = 3, TargetIndex = 3 },
                    new V1.AlignedWordPair { SourceIndex = 4, TargetIndex = 4 }
                },
                Phrases =
                {
                    new V1.Phrase
                    {
                        SourceSegmentStart = 0,
                        SourceSegmentEnd = 5,
                        TargetSegmentCut = 5
                    }
                }
            };
            var translateResponse = new TranslateResponse { Results = { translationResult } };
            TranslationServiceClient
                .TranslateAsync(Arg.Any<TranslateRequest>())
                .Returns(CreateAsyncUnaryCall(translateResponse));
            var wordGraph = new V1.WordGraph
            {
                SourceTokens = { "esto es una prueba .".Split() },
                FinalStates = { 3 },
                Arcs =
                {
                    new V1.WordGraphArc
                    {
                        PrevState = 0,
                        NextState = 1,
                        Score = 1.0,
                        TargetTokens = { "this is".Split() },
                        Alignment =
                        {
                            new V1.AlignedWordPair { SourceIndex = 0, TargetIndex = 0 },
                            new V1.AlignedWordPair { SourceIndex = 1, TargetIndex = 1 }
                        },
                        SourceSegmentStart = 0,
                        SourceSegmentEnd = 2,
                        Sources = { GetSources(2, false) },
                        Confidences = { 1.0, 1.0 }
                    },
                    new V1.WordGraphArc
                    {
                        PrevState = 1,
                        NextState = 2,
                        Score = 1.0,
                        TargetTokens = { "a test".Split() },
                        Alignment =
                        {
                            new V1.AlignedWordPair { SourceIndex = 0, TargetIndex = 0 },
                            new V1.AlignedWordPair { SourceIndex = 1, TargetIndex = 1 }
                        },
                        SourceSegmentStart = 2,
                        SourceSegmentEnd = 4,
                        Sources = { GetSources(2, false) },
                        Confidences = { 1.0, 1.0 }
                    },
                    new V1.WordGraphArc
                    {
                        PrevState = 2,
                        NextState = 3,
                        Score = 1.0,
                        TargetTokens = { ".".Split() },
                        Alignment =
                        {
                            new V1.AlignedWordPair { SourceIndex = 0, TargetIndex = 0 }
                        },
                        SourceSegmentStart = 4,
                        SourceSegmentEnd = 5,
                        Sources = { GetSources(1, false) },
                        Confidences = { 1.0 }
                    }
                }
            };
            var getWordGraphResponse = new GetWordGraphResponse { WordGraph = wordGraph };
            TranslationServiceClient
                .GetWordGraphAsync(Arg.Any<GetWordGraphRequest>())
                .Returns(CreateAsyncUnaryCall(getWordGraphResponse));
            TranslationServiceClient
                .CancelBuildAsync(Arg.Any<CancelBuildRequest>())
                .Returns(CreateAsyncUnaryCall(new Empty()));
            TranslationServiceClient
                .CreateAsync(Arg.Any<CreateRequest>())
                .Returns(CreateAsyncUnaryCall(new CreateResponse()));
            TranslationServiceClient.DeleteAsync(Arg.Any<DeleteRequest>()).Returns(CreateAsyncUnaryCall(new Empty()));
            TranslationServiceClient
                .StartBuildAsync(Arg.Any<StartBuildRequest>())
                .Returns(CreateAsyncUnaryCall(new Empty()));
            TranslationServiceClient
                .TrainSegmentPairAsync(Arg.Any<TrainSegmentPairRequest>())
                .Returns(CreateAsyncUnaryCall(new Empty()));
            GrpcClientFactory grpcClientFactory = Substitute.For<GrpcClientFactory>();
            grpcClientFactory
                .CreateClient<TranslationEngineApi.TranslationEngineApiClient>("Smt")
                .Returns(TranslationServiceClient);
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
                new MemoryRepository<Pretranslation>(),
                Substitute.For<IScopedMediator>(),
                grpcClientFactory,
                dataFileOptions,
                new MemoryDataAccessContext(),
                new LoggerFactory(),
                scriptureDataFileService,
                Substitute.For<IRequestClient<GetDataFile>>()
            );
        }

        public EngineService Service { get; }
        public IRepository<Engine> Engines { get; }
        public TranslationEngineApi.TranslationEngineApiClient TranslationServiceClient { get; }

        public async Task<Engine> CreateEngineWithTextFilesAsync()
        {
            var engine = new Engine
            {
                Id = "engine1",
                Owner = "owner1",
                SourceLanguage = "es",
                TargetLanguage = "en",
                Type = "Smt",
                Corpora = new Models.Corpus[]
                {
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
                                Format = Shared.Contracts.FileFormat.Text,
                                TextId = "text1"
                            }
                        ],
                        TargetFiles =
                        [
                            new()
                            {
                                Id = "file2",
                                Format = Shared.Contracts.FileFormat.Text,
                                TextId = "text1"
                            }
                        ],
                    }
                }
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
                Type = "Smt",
                Corpora = new Models.Corpus[]
                {
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
                                Format = Shared.Contracts.FileFormat.Text,
                                TextId = "text1"
                            }
                        ],
                        TargetFiles =
                        [
                            new()
                            {
                                Id = "file2",
                                Format = Shared.Contracts.FileFormat.Text,
                                TextId = "text1"
                            }
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
                                Format = Shared.Contracts.FileFormat.Text,
                                TextId = "text1"
                            }
                        ],
                        TargetFiles =
                        [
                            new()
                            {
                                Id = "file4",
                                Format = Shared.Contracts.FileFormat.Text,
                                TextId = "text1"
                            }
                        ],
                    }
                }
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
                Type = "Smt",
                Corpora = new Models.Corpus[]
                {
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
                                Format = Shared.Contracts.FileFormat.Paratext,
                                TextId = "file1.zip"
                            }
                        ],
                        TargetFiles =
                        [
                            new()
                            {
                                Id = "file2",
                                Format = Shared.Contracts.FileFormat.Paratext,
                                TextId = "file2.zip"
                            }
                        ],
                    }
                }
            };
            await Engines.InsertAsync(engine);
            return engine;
        }

        public async Task<Engine> CreateParallelCorpusEngineWithTextFilesAsync()
        {
            var engine = new Engine
            {
                Id = "engine1",
                Owner = "owner1",
                SourceLanguage = "es",
                TargetLanguage = "en",
                Type = "Smt",
                ParallelCorpora = new Models.ParallelCorpus[]
                {
                    new()
                    {
                        Id = "parallel-corpus1",
                        SourceCorpora = new List<Shared.Models.MonolingualCorpus>()
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
                                        Format = Shared.Contracts.FileFormat.Text,
                                        TextId = "MAT"
                                    }
                                ]
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
                                Id = "parallel-corpus1-target1",
                                Name = "",
                                Language = "en",
                                Files =
                                [
                                    new()
                                    {
                                        Id = "file2",
                                        Format = Shared.Contracts.FileFormat.Text,
                                        TextId = "MAT"
                                    }
                                ]
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
                                        Format = Shared.Contracts.FileFormat.Text,
                                        TextId = "MRK"
                                    }
                                ]
                            }
                        }
                    }
                }
            };
            await Engines.InsertAsync(engine);
            return engine;
        }

        public async Task<Engine> CreateMultipleParallelCorpusEngineWithTextFilesAsync()
        {
            var engine = new Engine
            {
                Id = "engine1",
                Owner = "owner1",
                SourceLanguage = "es",
                TargetLanguage = "en",
                Type = "Smt",
                ParallelCorpora = new Models.ParallelCorpus[]
                {
                    new()
                    {
                        Id = "parallel-corpus1",
                        SourceCorpora = new List<Shared.Models.MonolingualCorpus>()
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
                                Id = "parallel-corpus1-target1",
                                Name = "",
                                Language = "en",
                                Files =
                                [
                                    new()
                                    {
                                        Id = "file2",
                                        Format = Shared.Contracts.FileFormat.Text,
                                        TextId = "MAT"
                                    }
                                ]
                            }
                        }
                    },
                    new()
                    {
                        Id = "parallel-corpus2",
                        SourceCorpora = new List<Shared.Models.MonolingualCorpus>()
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
                                Id = "parallel-corpus2-target1",
                                Name = "",
                                Language = "en",
                                Files =
                                [
                                    new()
                                    {
                                        Id = "file4",
                                        Format = Shared.Contracts.FileFormat.Text,
                                        TextId = "MRK"
                                    }
                                ]
                            }
                        }
                    }
                }
            };
            await Engines.InsertAsync(engine);
            return engine;
        }

        public async Task<Engine> CreateParallelCorpusEngineWithParatextProjectAsync()
        {
            var engine = new Engine
            {
                Id = "engine1",
                Owner = "owner1",
                SourceLanguage = "es",
                TargetLanguage = "en",
                Type = "Smt",
                ParallelCorpora = new Models.ParallelCorpus[]
                {
                    new()
                    {
                        Id = "parallel-corpus1",
                        SourceCorpora = new List<Shared.Models.MonolingualCorpus>()
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
                                        Format = Shared.Contracts.FileFormat.Paratext,
                                        TextId = "file1.zip"
                                    }
                                ]
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
                                Id = "parallel-corpus1-target1",
                                Name = "",
                                Language = "en",
                                Files =
                                [
                                    new()
                                    {
                                        Id = "file2",
                                        Format = Shared.Contracts.FileFormat.Paratext,
                                        TextId = "file2.zip"
                                    }
                                ]
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
                                        Format = Shared.Contracts.FileFormat.Paratext,
                                        TextId = "file4.zip"
                                    }
                                ]
                            }
                        }
                    }
                }
            };
            await Engines.InsertAsync(engine);
            return engine;
        }

        private static TranslationSources[] GetSources(int count, bool isUnknown)
        {
            var sources = new TranslationSources[count];
            for (int i = 0; i < count; i++)
            {
                sources[i] = new TranslationSources();
                if (!isUnknown)
                    sources[i].Values.Add(V1.TranslationSource.Primary);
            }
            return sources;
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
}
