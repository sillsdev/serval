using MassTransit.Mediator;
using Serval.Translation.Configuration;
using EngineApiTranslation = Serval.EngineApi.Translation;

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
        Assert.ThrowsAsync<EntityNotFoundException>(() =>
            env.Service.GetWordGraphAsync("engine1", "esto es una prueba.")
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
        Assert.ThrowsAsync<EntityNotFoundException>(() =>
            env.Service.TrainSegmentPairAsync("engine1", "esto es una prueba.", "this is a test.", true)
        );
    }

    [Test]
    public async Task TrainSegmentAsync_EngineExists()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineWithTextFilesAsync()).Id;
        Assert.DoesNotThrowAsync(() =>
            env.Service.TrainSegmentPairAsync(engineId, "esto es una prueba.", "this is a test.", true)
        );
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
            Type = "Smt",
            Corpora = [],
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
        await env.Service.StartBuildAsync(
            new Build
            {
                Id = BUILD1_ID,
                EngineRef = engineId,
                Owner = "owner1",
            }
        );
        await env
            .TranslationEngine.Received()
            .StartBuildAsync(
                engineId,
                BUILD1_ID,
                Arg.Any<IReadOnlyList<FilteredParallelCorpus>>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>()
                                        Id = "corpus1",
                                        Id = "corpus1",
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
                Owner = "owner1",
                TrainOn = [new TrainingCorpus { CorpusRef = "corpus1", TextIds = [] }],
            }
        );
        await env
            .TranslationEngine.Received()
            .StartBuildAsync(
                engineId,
                BUILD1_ID,
                Arg.Any<IReadOnlyList<FilteredParallelCorpus>>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>()
                                        Id = "corpus1",

                                        Id = "corpus1",

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
                Owner = "owner1",
                TrainOn = [new TrainingCorpus { CorpusRef = "corpus1", TextIds = ["text1"] }],
            }
        );
        await env
            .TranslationEngine.Received()
            .StartBuildAsync(
                engineId,
                BUILD1_ID,
                Arg.Any<IReadOnlyList<FilteredParallelCorpus>>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>()
                                        Id = "corpus1",

                                        Id = "corpus1",

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
                Owner = "owner1",
                TrainOn = [new TrainingCorpus { CorpusRef = "corpus1" }],
            }
        );
        await env
            .TranslationEngine.Received()
            .StartBuildAsync(
                engineId,
                BUILD1_ID,
                Arg.Any<IReadOnlyList<FilteredParallelCorpus>>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>()
                                        Id = "corpus1",

                                        Id = "corpus1",

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
                Owner = "owner1",
                TrainOn = [new TrainingCorpus { CorpusRef = "corpus1" }],
                Pretranslate = [new PretranslateCorpus { CorpusRef = "corpus1" }],
            }
        );
        await env
            .TranslationEngine.Received()
            .StartBuildAsync(
                engineId,
                BUILD1_ID,
                Arg.Any<IReadOnlyList<FilteredParallelCorpus>>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>()
                                        Id = "corpus1",

                                        Id = "corpus1",

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
                Owner = "owner1",
                TrainOn = [new TrainingCorpus { CorpusRef = "corpus1" }],
                Pretranslate = [new PretranslateCorpus { CorpusRef = "corpus2" }],
            }
        );
        await env
            .TranslationEngine.Received()
            .StartBuildAsync(
                engineId,
                BUILD1_ID,
                Arg.Any<IReadOnlyList<FilteredParallelCorpus>>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>()
                                        Id = "corpus1",

                                        Id = "corpus1",

                                        Id = "corpus2",

                                        Id = "corpus2",

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
                    Owner = "owner1",
                    TrainOn = [new TrainingCorpus { CorpusRef = "corpus1", ScriptureRange = "MAT" }],
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
                Owner = "owner1",
                TrainOn = [new TrainingCorpus { CorpusRef = "corpus1", ScriptureRange = "MAT 1;MRK" }],
            }
        );
        await env
            .TranslationEngine.Received()
            .StartBuildAsync(
                engineId,
                BUILD1_ID,
                Arg.Any<IReadOnlyList<FilteredParallelCorpus>>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>()
                                        Id = "corpus1",

                                        Id = "corpus1",

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
                Owner = "owner1",
                TrainOn = [new TrainingCorpus { CorpusRef = "corpus1", ScriptureRange = "" }],
            }
        );
        await env
            .TranslationEngine.Received()
            .StartBuildAsync(
                engineId,
                BUILD1_ID,
                Arg.Any<IReadOnlyList<FilteredParallelCorpus>>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>()
                                        Id = "corpus1",

                                        Id = "corpus1",

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
                Owner = "owner1",
                TrainOn =
                [
                    new TrainingCorpus
                    {
                        ParallelCorpusRef = "parallel-corpus1",
                        SourceFilters = [new() { CorpusRef = "parallel-corpus1-source1", TextIds = ["MAT"] }],
                        TargetFilters = [new() { CorpusRef = "parallel-corpus1-target1", TextIds = ["MAT"] }],
                    },
                ],
            }
        );
        await env
            .TranslationEngine.Received()
            .StartBuildAsync(
                engineId,
                BUILD1_ID,
                Arg.Any<IReadOnlyList<FilteredParallelCorpus>>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>()
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
                Owner = "owner1",
                TrainOn =
                [
                    new TrainingCorpus
                    {
                        ParallelCorpusRef = "parallel-corpus1",
                        SourceFilters = [new() { CorpusRef = "parallel-corpus1-source1", TextIds = ["MAT"] }],
                        TargetFilters = [new() { CorpusRef = "parallel-corpus1-target1", TextIds = ["MAT"] }],
                    },
                ],
                Pretranslate = [new PretranslateCorpus { ParallelCorpusRef = "parallel-corpus1" }],
            }
        );
        await env
            .TranslationEngine.Received()
            .StartBuildAsync(
                engineId,
                BUILD1_ID,
                Arg.Any<IReadOnlyList<FilteredParallelCorpus>>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>()
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
                Owner = "owner1",
                TrainOn =
                [
                    new TrainingCorpus
                    {
                        ParallelCorpusRef = "parallel-corpus1",
                        SourceFilters = [new() { CorpusRef = "parallel-corpus1-source1", TextIds = ["MAT"] }],
                        TargetFilters = [new() { CorpusRef = "parallel-corpus1-target1", TextIds = ["MAT"] }],
                    },
                ],
                Pretranslate = [new PretranslateCorpus { ParallelCorpusRef = "parallel-corpus2" }],
            }
        );
        await env
            .TranslationEngine.Received()
            .StartBuildAsync(
                engineId,
                BUILD1_ID,
                Arg.Any<IReadOnlyList<FilteredParallelCorpus>>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>()
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
                Owner = "owner1",
                TrainOn =
                [
                    new TrainingCorpus
                    {
                        ParallelCorpusRef = "parallel-corpus1",
                        SourceFilters = [new() { CorpusRef = "parallel-corpus1-source1", TextIds = ["MAT", "MRK"] }],
                        TargetFilters = [new() { CorpusRef = "parallel-corpus1-target1", TextIds = ["MAT", "MRK"] }],
                    },
                ],
            }
        );
        await env
            .TranslationEngine.Received()
            .StartBuildAsync(
                engineId,
                BUILD1_ID,
                Arg.Any<IReadOnlyList<FilteredParallelCorpus>>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>()
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
                Owner = "owner1",
                TrainOn =
                [
                    new TrainingCorpus
                    {
                        ParallelCorpusRef = "parallel-corpus1",
                        SourceFilters =
                        [
                            new() { CorpusRef = "parallel-corpus1-source1", ScriptureRange = "MAT 1;MRK" },
                        ],
                        TargetFilters =
                        [
                            new() { CorpusRef = "parallel-corpus1-target1", ScriptureRange = "MAT 1;MRK" },
                        ],
                    },
                ],
                Pretranslate =
                [
                    new PretranslateCorpus
                    {
                        ParallelCorpusRef = "parallel-corpus1",
                        SourceFilters = [new() { CorpusRef = "parallel-corpus1-source1", ScriptureRange = "MAT 2" }],
                    },
                ],
            }
        );
        await env
            .TranslationEngine.Received()
            .StartBuildAsync(
                engineId,
                BUILD1_ID,
                Arg.Any<IReadOnlyList<FilteredParallelCorpus>>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>()
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
                Owner = "owner1",
                TrainOn =
                [
                    new TrainingCorpus
                    {
                        ParallelCorpusRef = "parallel-corpus1",
                        SourceFilters =
                        [
                            new() { CorpusRef = "parallel-corpus1-source1", ScriptureRange = "MAT 1-2;MRK 1-2" },
                            new() { CorpusRef = "parallel-corpus1-source2", ScriptureRange = "MAT 3;MRK 1" },
                        ],
                        TargetFilters =
                        [
                            new() { CorpusRef = "parallel-corpus1-target1", ScriptureRange = "MAT 2-3;MRK 2" },
                            new() { CorpusRef = "parallel-corpus1-target2", ScriptureRange = "MAT 1;MRK 1-2" },
                        ],
                    },
                ],
            }
        );
        await env
            .TranslationEngine.Received()
            .StartBuildAsync(
                engineId,
                BUILD1_ID,
                Arg.Any<IReadOnlyList<FilteredParallelCorpus>>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Test]
    public async Task StartBuildAsync_TextFilesScriptureRangeSpecified_ParallelCorpus()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateParallelCorpusEngineWithParatextProjectAsync()).Id;
        Assert.ThrowsAsync<InvalidOperationException>(() =>
            env.Service.StartBuildAsync(
                new Build
                {
                    Id = BUILD1_ID,
                    EngineRef = engineId,
                    Owner = "owner1",
                    TrainOn =
                    [
                        new TrainingCorpus
                        {
                            ParallelCorpusRef = "parallel-corpus1",
                            SourceFilters =
                            [
                                new()
                                {
                                    CorpusRef = "parallel-corpus1-source1",
                                    ScriptureRange = "MAT",
                                    TextIds = [],
                                },
                            ],
                            TargetFilters =
                            [
                                new()
                                {
                                    CorpusRef = "parallel-corpus1-target1",
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
    public async Task StartBuildAsync_NoFilters_ParallelCorpus()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateParallelCorpusEngineWithParatextProjectAsync()).Id;
        await env.Service.StartBuildAsync(
            new Build
            {
                Id = BUILD1_ID,
                EngineRef = engineId,
                Owner = "owner1",
                TrainOn = [new TrainingCorpus { ParallelCorpusRef = "parallel-corpus1" }],
            }
        );
        await env
            .TranslationEngine.Received()
            .StartBuildAsync(
                engineId,
                BUILD1_ID,
                Arg.Any<IReadOnlyList<FilteredParallelCorpus>>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Test]
    public async Task StartBuildAsync_TrainOnNotSpecified_ParallelCorpus()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateParallelCorpusEngineWithParatextProjectAsync()).Id;
        await env.Service.StartBuildAsync(
            new Build
            {
                Id = BUILD1_ID,
                EngineRef = engineId,
                Owner = "owner1",
            }
        );
        await env
            .TranslationEngine.Received()
            .StartBuildAsync(
                engineId,
                BUILD1_ID,
                Arg.Any<IReadOnlyList<FilteredParallelCorpus>>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>()
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
                Owner = "owner1",
                TrainOn =
                [
                    new TrainingCorpus
                    {
                        ParallelCorpusRef = "parallel-corpus1",
                        SourceFilters =
                        [
                            new() { CorpusRef = "parallel-corpus1-source1", ScriptureRange = "MAT 1;MRK" },
                        ],
                    },
                ],
            }
        );
        await env
            .TranslationEngine.Received()
            .StartBuildAsync(
                engineId,
                BUILD1_ID,
                Arg.Any<IReadOnlyList<FilteredParallelCorpus>>(),
                Arg.Any<string?>(),
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
        string corpusId = engine.Corpora[0].Id;

        Models.Corpus? corpus = await env.Service.UpdateCorpusAsync(
            engine.Id,
            corpusId,
            sourceFiles:
            [
                new()
                {
                    Id = "file1",
                    Filename = "file1.txt",
                    Format = Shared.Contracts.FileFormat.Text,
                    TextId = "text1",
                },
                new()
                {
                    Id = "file3",
                    Filename = "file3.txt",
                    Format = Shared.Contracts.FileFormat.Text,
                    TextId = "text2",
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

    [Test]
    public async Task UpdateAsync_ShouldUpdateLanguages_WhenRequestIsValid()
    {
        var env = new TestEnvironment();
        var engine = await env.CreateEngineWithTextFilesAsync();

        var request = new TranslationEngineUpdateConfigDto { SourceLanguage = "en", TargetLanguage = "fr" };

        await env.Service.UpdateAsync(engine.Id, request.SourceLanguage, request.TargetLanguage);

        engine = await env.Engines.GetAsync(engine.Id);

        Assert.That(engine, Is.Not.Null);
        Assert.That(engine.SourceLanguage, Is.Not.Null);
        Assert.That(engine.SourceLanguage, Is.EqualTo("en"));
        Assert.That(engine.TargetLanguage, Is.Not.Null);
        Assert.That(engine.TargetLanguage, Is.EqualTo("fr"));
    }

    [Test]
    public async Task UpdateAsync_ShouldNotUpdateSourceLanguage_WhenSourceLanguageNotProvided()
    {
        var env = new TestEnvironment();
        Engine engine = await env.CreateEngineWithTextFilesAsync();

        await env.Service.UpdateAsync(engine.Id, sourceLanguage: null, targetLanguage: "fr");

        Engine? updatedEngine = await env.Engines.GetAsync(engine.Id);

        Assert.That(updatedEngine, Is.Not.Null);
        Assert.That(updatedEngine.SourceLanguage, Is.Not.Null);
        Assert.That(updatedEngine.SourceLanguage, Is.EqualTo(engine.SourceLanguage));
        Assert.That(updatedEngine.TargetLanguage, Is.Not.Null);
        Assert.That(updatedEngine.TargetLanguage, Is.EqualTo("fr"));
    }

    [Test]
    public async Task UpdateAsync_ShouldNotUpdateTargetLanguage_WhenTargetLanguageNotProvided()
    {
        var env = new TestEnvironment();
        Engine engine = await env.CreateEngineWithTextFilesAsync();

        await env.Service.UpdateAsync(engine.Id, sourceLanguage: "en", targetLanguage: null);

        Engine? updatedEngine = await env.Engines.GetAsync(engine.Id);

        Assert.That(updatedEngine, Is.Not.Null);
        Assert.That(updatedEngine.SourceLanguage, Is.Not.Null);
        Assert.That(updatedEngine.SourceLanguage, Is.EqualTo("en"));
        Assert.That(updatedEngine.TargetLanguage, Is.Not.Null);
        Assert.That(updatedEngine.TargetLanguage, Is.EqualTo(engine.TargetLanguage));
    }

    [Test]
    public async Task UpdateAsync_ShouldNotUpdate_WhenSourceAndTargetLanguagesNotProvided()
    {
        var env = new TestEnvironment();
        Engine engine = await env.CreateEngineWithTextFilesAsync();

        await env.Service.UpdateAsync(engine.Id, sourceLanguage: null, targetLanguage: null);

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
        await env.Service.UpdateCorpusAsync(engine.Id, "corpus1", sourceFiles: [], targetFiles: []);
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
        await env.Service.UpdateParallelCorpusAsync(
            engine.Id,
            "parallel-corpus1",
            sourceCorpora: [],
            targetCorpora: []
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
        await env.Service.DeleteAllCorpusFilesAsync("file1");
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
        await env.Service.UpdateCorpusFilesAsync(
            "corpus1",
            [
                new()
                {
                    Id = "file1",
                    Filename = "newfilename",
                    TextId = "text1",
                    Format = Shared.Contracts.FileFormat.Text,
                },
            ]
        );
        Assert.That(await env.Pretranslations.GetAsync(pretranslation.Id), Is.Null);
    }

    private class TestEnvironment
    {
        public TestEnvironment()
        {
            Engines = new MemoryRepository<Engine>();
            TranslationEngine = Substitute.For<EngineApiTranslation.ITranslationEngine>();
            TranslationEngine.EngineType.Returns("Smt");

            var translationResult = new EngineApiTranslation.TranslationResult
            {
                Translation = "this is a test.",
                SourceTokens = ["esto", "es", "una", "prueba", "."],
                TargetTokens = ["this", "is", "a", "test", "."],
                Confidences = [1.0, 1.0, 1.0, 1.0, 1.0],
                Sources =
                [
                    new HashSet<EngineApiTranslation.TranslationSource>
                    {
                        EngineApiTranslation.TranslationSource.Primary,
                    },
                    new HashSet<EngineApiTranslation.TranslationSource>
                    {
                        EngineApiTranslation.TranslationSource.Primary,
                    },
                    new HashSet<EngineApiTranslation.TranslationSource>
                    {
                        EngineApiTranslation.TranslationSource.Primary,
                    },
                    new HashSet<EngineApiTranslation.TranslationSource>
                    {
                        EngineApiTranslation.TranslationSource.Primary,
                    },
                    new HashSet<EngineApiTranslation.TranslationSource>
                    {
                        EngineApiTranslation.TranslationSource.Primary,
                    },
                ],
                Alignment =
                [
                    new EngineApiTranslation.AlignedWordPair { SourceIndex = 0, TargetIndex = 0 },
                    new EngineApiTranslation.AlignedWordPair { SourceIndex = 1, TargetIndex = 1 },
                    new EngineApiTranslation.AlignedWordPair { SourceIndex = 2, TargetIndex = 2 },
                    new EngineApiTranslation.AlignedWordPair { SourceIndex = 3, TargetIndex = 3 },
                    new EngineApiTranslation.AlignedWordPair { SourceIndex = 4, TargetIndex = 4 },
                ],
                Phrases =
                [
                    new EngineApiTranslation.Phrase
                    {
                        SourceSegmentStart = 0,
                        SourceSegmentEnd = 5,
                        TargetSegmentCut = 5,
                    },
                ],
            };
            TranslationEngine
                .TranslateAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyList<EngineApiTranslation.TranslationResult>>([translationResult]));

            var wordGraph = new EngineApiTranslation.WordGraph
            {
                SourceTokens = ["esto", "es", "una", "prueba", "."],
                InitialStateScore = 0.0,
                FinalStates = new HashSet<int> { 3 },
                Arcs =
                [
                    new EngineApiTranslation.WordGraphArc
                    {
                        PrevState = 0,
                        NextState = 1,
                        Score = 1.0,
                        TargetTokens = ["this", "is"],
                        Alignment =
                        [
                            new EngineApiTranslation.AlignedWordPair { SourceIndex = 0, TargetIndex = 0 },
                            new EngineApiTranslation.AlignedWordPair { SourceIndex = 1, TargetIndex = 1 },
                        ],
                        SourceSegmentStart = 0,
                        SourceSegmentEnd = 2,
                        Sources =
                        [
                            new HashSet<EngineApiTranslation.TranslationSource>
                            {
                                EngineApiTranslation.TranslationSource.Primary,
                            },
                            new HashSet<EngineApiTranslation.TranslationSource>
                            {
                                EngineApiTranslation.TranslationSource.Primary,
                            },
                        ],
                        Confidences = [1.0, 1.0],
                    },
                    new EngineApiTranslation.WordGraphArc
                    {
                        PrevState = 1,
                        NextState = 2,
                        Score = 1.0,
                        TargetTokens = ["a", "test"],
                        Alignment =
                        [
                            new EngineApiTranslation.AlignedWordPair { SourceIndex = 0, TargetIndex = 0 },
                            new EngineApiTranslation.AlignedWordPair { SourceIndex = 1, TargetIndex = 1 },
                        ],
                        SourceSegmentStart = 2,
                        SourceSegmentEnd = 4,
                        Sources =
                        [
                            new HashSet<EngineApiTranslation.TranslationSource>
                            {
                                EngineApiTranslation.TranslationSource.Primary,
                            },
                            new HashSet<EngineApiTranslation.TranslationSource>
                            {
                                EngineApiTranslation.TranslationSource.Primary,
                            },
                        ],
                        Confidences = [1.0, 1.0],
                    },
                    new EngineApiTranslation.WordGraphArc
                    {
                        PrevState = 2,
                        NextState = 3,
                        Score = 1.0,
                        TargetTokens = ["."],
                        Alignment = [new EngineApiTranslation.AlignedWordPair { SourceIndex = 0, TargetIndex = 0 }],
                        SourceSegmentStart = 4,
                        SourceSegmentEnd = 5,
                        Sources =
                        [
                            new HashSet<EngineApiTranslation.TranslationSource>
                            {
                                EngineApiTranslation.TranslationSource.Primary,
                            },
                        ],
                        Confidences = [1.0],
                    },
                ],
            };
            TranslationEngine
                .GetWordGraphAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(wordGraph));
            TranslationEngine
                .CancelBuildAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<string?>(null));
            TranslationEngine
                .CreateAsync(
                    Arg.Any<string>(),
                    Arg.Any<string>(),
                    Arg.Any<string>(),
                    Arg.Any<bool>(),
                    Arg.Any<string?>(),
                    Arg.Any<CancellationToken>()
                )
                .Returns(Task.CompletedTask);
            TranslationEngine.DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            TranslationEngine
                .StartBuildAsync(
                    Arg.Any<string>(),
                    Arg.Any<string>(),
                    Arg.Any<IReadOnlyList<FilteredParallelCorpus>>(),
                    Arg.Any<string?>(),
                    Arg.Any<CancellationToken>()
                )
                .Returns(Task.CompletedTask);
            TranslationEngine
                .UpdateAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            TranslationEngine
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
                        guid: "",
                        translationType: ""

            Pretranslations = new MemoryRepository<Pretranslation>();
            IOptionsMonitor<TranslationOptions> translationOptions = Substitute.For<
                IOptionsMonitor<TranslationOptions>
            >();
            translationOptions.CurrentValue.Returns(
                new TranslationOptions { Engines = [new EngineInfo { Type = "Smt" }] }
            );
            var parallelCorpusService = Substitute.For<IParallelCorpusService>();
            parallelCorpusService
                .GetChapters(
                    Arg.Any<IReadOnlyList<SIL.ServiceToolkit.Models.ParallelCorpus>>(),
                    Arg.Any<string>(),
                    Arg.Any<string>()
                )
                .Returns(callInfo =>
                {
                    return ScriptureRangeParser.GetChapters(callInfo.ArgAt<string>(2));
                });

            Service = new EngineService(
                Engines,
                new MemoryRepository<Build>(),
                Pretranslations,
                Substitute.For<IScopedMediator>(),
                [TranslationEngine],
                new MemoryDataAccessContext(),
                new LoggerFactory(),
                translationOptions,
                new CorpusMappingService(dataFileOptions, parallelCorpusService)
            );
        }

        public EngineService Service { get; }
        public IRepository<Engine> Engines { get; }
        public IRepository<Pretranslation> Pretranslations { get; }
        public EngineApiTranslation.ITranslationEngine TranslationEngine { get; }

        public async Task<Engine> CreateEngineWithTextFilesAsync()
        {
            var engine = new Engine
            {
                Id = "engine1",
                Owner = "owner1",
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
                                Format = Shared.Contracts.FileFormat.Text,
                                TextId = "text1",
                            },
                        ],
                        TargetFiles =
                        [
                            new()
                            {
                                Id = "file2",
                                Filename = "file2.txt",
                                Format = Shared.Contracts.FileFormat.Text,
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
                Owner = "owner1",
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
                                Format = Shared.Contracts.FileFormat.Text,
                                TextId = "text1",
                            },
                        ],
                        TargetFiles =
                        [
                            new()
                            {
                                Id = "file2",
                                Filename = "file2.txt",
                                Format = Shared.Contracts.FileFormat.Text,
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
                                Format = Shared.Contracts.FileFormat.Text,
                                TextId = "text1",
                            },
                        ],
                        TargetFiles =
                        [
                            new()
                            {
                                Id = "file4",
                                Filename = "file4.txt",
                                Format = Shared.Contracts.FileFormat.Text,
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
                Owner = "owner1",
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
                                Format = Shared.Contracts.FileFormat.Paratext,
                                TextId = "file1.zip",
                            },
                        ],
                        TargetFiles =
                        [
                            new()
                            {
                                Id = "file2",
                                Filename = "file2.zip",
                                Format = Shared.Contracts.FileFormat.Paratext,
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
                Owner = "owner1",
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
                                        Format = Shared.Contracts.FileFormat.Text,
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
                                        Format = Shared.Contracts.FileFormat.Text,
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
                                        Format = Shared.Contracts.FileFormat.Text,
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
                                        Format = Shared.Contracts.FileFormat.Text,
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
                Owner = "owner1",
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
                                        Format = Shared.Contracts.FileFormat.Text,
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
                                        Format = Shared.Contracts.FileFormat.Text,
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
                                        Format = Shared.Contracts.FileFormat.Text,
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
                                        Format = Shared.Contracts.FileFormat.Text,
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
                Owner = "owner1",
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
                                        Format = Shared.Contracts.FileFormat.Paratext,
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
                                        Format = Shared.Contracts.FileFormat.Paratext,
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
                                        Format = Shared.Contracts.FileFormat.Paratext,
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
                                        Format = Shared.Contracts.FileFormat.Paratext,
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
