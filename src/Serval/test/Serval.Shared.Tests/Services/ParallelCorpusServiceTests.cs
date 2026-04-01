namespace Serval.Shared.Services;

[TestFixture]
public class ParallelCorpusServiceTests
{
    [Test]
    public void AnalyzeTargetQuoteConvention_FileFormatParatext()
    {
        using var env = new TestEnvironment();
        ParallelCorpusContract parallelCorpus = env.GetCorpora(paratextProject: true).First();
        const string ExpectedTargetName = "typewriter_english";

        string targetQuotationConvention = env.Processor.AnalyzeTargetQuoteConvention([parallelCorpus]);

        Assert.That(targetQuotationConvention, Is.EqualTo(ExpectedTargetName));
    }

    [Test]
    public void AnalyzeTargetQuoteConvention_FileFormatText()
    {
        using var env = new TestEnvironment();
        ParallelCorpusContract parallelCorpus = env.GetCorpora(paratextProject: false).First();

        string targetQuotationConvention = env.Processor.AnalyzeTargetQuoteConvention([parallelCorpus]);

        Assert.That(targetQuotationConvention, Is.Empty);
    }

    [Test]
    public async Task PreprocessAsync_FileFormatText()
    {
        using var env = new TestEnvironment();
        IReadOnlyList<ParallelCorpusContract> corpora = env.GetCorpora(paratextProject: false);
        int trainCount = 0;
        int inferenceCount = 0;
        await env.Processor.PreprocessAsync(
            corpora,
            (row, _) =>
            {
                if (row.SourceSegment.Length > 0 && row.TargetSegment.Length > 0)
                    trainCount++;
                return Task.CompletedTask;
            },
            (row, isInTrainingData, _) =>
            {
                if (row.SourceSegment.Length > 0 && !isInTrainingData)
                {
                    inferenceCount++;
                }

                return Task.CompletedTask;
            },
            false
        );

        Assert.Multiple(() =>
        {
            Assert.That(trainCount, Is.EqualTo(2));
            Assert.That(inferenceCount, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task PreprocessAsync_FileFormatParatext()
    {
        using var env = new TestEnvironment();
        IReadOnlyList<ParallelCorpusContract> corpora = env.GetCorpora(paratextProject: true);
        int trainCount = 0;
        int inferenceCount = 0;
        var trainRefs = new List<string>();
        var inferenceRefs = new List<string>();
        await env.Processor.PreprocessAsync(
            corpora,
            (row, _) =>
            {
                if (row.SourceSegment.Length > 0 && row.TargetSegment.Length > 0)
                {
                    trainCount++;
                    trainRefs.Add(row.TargetRefs[0].ToString() ?? "");
                }
                return Task.CompletedTask;
            },
            (row, isInTrainingData, _) =>
            {
                if (row.SourceSegment.Length > 0 && !isInTrainingData)
                {
                    inferenceCount++;
                    inferenceRefs.Add(row.TargetRefs[0].ToString() ?? "");
                }

                return Task.CompletedTask;
            },
            false,
            ["mt"]
        );

        Assert.Multiple(() =>
        {
            Assert.That(trainCount, Is.EqualTo(5));
            Assert.That(inferenceCount, Is.EqualTo(17));
        });
    }

    [Test]
    public async Task PreprocessAsync_FilterOutEverything()
    {
        using var env = new TestEnvironment();
        ParallelCorpusContract corpus = env.DefaultTextFileCorpus with { };

        PreprocessResult result = await env.RunPreprocessAsync([corpus]);

        (int src1Count, int src2Count, int trgCount, int termCount) = result.GetTrainCount();
        Assert.Multiple(() =>
        {
            Assert.That(src1Count, Is.EqualTo(0));
            Assert.That(src2Count, Is.EqualTo(0));
            Assert.That(trgCount, Is.EqualTo(0));
            Assert.That(termCount, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task PreprocessAsync_TrainOnAll()
    {
        using var env = new TestEnvironment();
        ParallelCorpusContract corpus = TestEnvironment.TextFileCorpus(trainOnTextIds: null, inferenceTextIds: []);

        PreprocessResult result = await env.RunPreprocessAsync([corpus]);

        (int src1Count, int src2Count, int trgCount, int termCount) = result.GetTrainCount();
        Assert.Multiple(() =>
        {
            Assert.That(src1Count, Is.EqualTo(4));
            Assert.That(src2Count, Is.EqualTo(0));
            Assert.That(trgCount, Is.EqualTo(1));
            Assert.That(termCount, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task PreprocessAsync_TrainOnTextIds()
    {
        using var env = new TestEnvironment();
        ParallelCorpusContract corpus = TestEnvironment.TextFileCorpus(
            trainOnTextIds: ["textId1"],
            inferenceTextIds: []
        );

        PreprocessResult result = await env.RunPreprocessAsync([corpus]);

        (int src1Count, int src2Count, int trgCount, int termCount) = result.GetTrainCount();
        Assert.Multiple(() =>
        {
            Assert.That(src1Count, Is.EqualTo(4));
            Assert.That(src2Count, Is.EqualTo(0));
            Assert.That(trgCount, Is.EqualTo(1));
            Assert.That(termCount, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task PreprocessAsync_TrainAndPretranslateAll()
    {
        using var env = new TestEnvironment();
        ParallelCorpusContract corpus = TestEnvironment.TextFileCorpus(trainOnTextIds: null, inferenceTextIds: null);

        PreprocessResult result = await env.RunPreprocessAsync([corpus]);

        Assert.That(result.Pretranslations.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task PreprocessAsync_PretranslateAll()
    {
        using var env = new TestEnvironment();
        ParallelCorpusContract corpus = TestEnvironment.TextFileCorpus(trainOnTextIds: [], inferenceTextIds: null);

        PreprocessResult result = await env.RunPreprocessAsync([corpus]);

        Assert.That(result.Pretranslations.Count, Is.EqualTo(4));
    }

    [Test]
    public async Task PreprocessAsync_InferenceTextIds()
    {
        using var env = new TestEnvironment();
        ParallelCorpusContract corpus = TestEnvironment.TextFileCorpus(
            inferenceTextIds: ["textId1"],
            trainOnTextIds: null
        );

        PreprocessResult result = await env.RunPreprocessAsync([corpus]);

        Assert.That(result.Pretranslations.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task PreprocessAsync_InferenceTextIdsOverlapWithTrainOnTextIds()
    {
        using var env = new TestEnvironment();
        ParallelCorpusContract corpus = TestEnvironment.TextFileCorpus(
            inferenceTextIds: ["textId1"],
            trainOnTextIds: ["textId1"]
        );

        PreprocessResult result = await env.RunPreprocessAsync([corpus]);

        Assert.Multiple(() =>
        {
            Assert.That(result.GetTrainCount().Source1Count, Is.EqualTo(4));
            Assert.That(result.Pretranslations.Count, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task PreprocessAsync_EnableKeyTerms()
    {
        using var env = new TestEnvironment();
        ParallelCorpusContract corpus = env.DefaultParatextCorpus;

        PreprocessResult result = await env.RunPreprocessAsync([corpus], useKeyTerms: true);

        (int src1Count, int src2Count, int trgCount, int termCount) = result.GetTrainCount();
        Assert.Multiple(() =>
        {
            Assert.That(src1Count, Is.EqualTo(14));
            Assert.That(src2Count, Is.EqualTo(0));
            Assert.That(trgCount, Is.EqualTo(1));
            Assert.That(termCount, Is.EqualTo(3652));
        });
    }

    [Test]
    public async Task PreprocessAsync_EnableKeyTermsNoTrainingData()
    {
        using var env = new TestEnvironment();
        ParallelCorpusContract corpus = env.DefaultParatextCorpus;
        corpus.SourceCorpora[0].TrainOnTextIds = [];
        corpus.TargetCorpora[0].TrainOnTextIds = [];

        PreprocessResult result = await env.RunPreprocessAsync([corpus], useKeyTerms: true);

        (int src1Count, int src2Count, int trgCount, int termCount) = result.GetTrainCount();
        Assert.Multiple(() =>
        {
            Assert.That(src1Count, Is.EqualTo(0));
            Assert.That(src2Count, Is.EqualTo(0));
            Assert.That(trgCount, Is.EqualTo(0));
            Assert.That(termCount, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task PreprocessAsync_DisableKeyTerms()
    {
        using var env = new TestEnvironment();
        ParallelCorpusContract corpus = env.DefaultParatextCorpus;

        PreprocessResult result = await env.RunPreprocessAsync([corpus], useKeyTerms: false);

        (int src1Count, int src2Count, int trgCount, int termCount) = result.GetTrainCount();
        Assert.Multiple(() =>
        {
            Assert.That(src1Count, Is.EqualTo(14));
            Assert.That(src2Count, Is.EqualTo(0));
            Assert.That(trgCount, Is.EqualTo(1));
            Assert.That(termCount, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task PreprocessAsync_InferenceChapters()
    {
        using var env = new TestEnvironment();
        ParallelCorpusContract corpus = env.ParatextCorpus(
            trainOnChapters: [],
            inferenceChapters: new Dictionary<string, HashSet<int>> { { "1CH", [12] } }
        );

        PreprocessResult result = await env.RunPreprocessAsync([corpus]);

        Assert.That(result.Pretranslations.Count, Is.EqualTo(4));
    }

    [Test]
    public async Task PreprocessAsync_DoNotPretranslateRemark()
    {
        using var env = new TestEnvironment();
        ParallelCorpusContract corpus = env.DefaultParatextCorpus;

        PreprocessResult result = await env.RunPreprocessAsync([corpus]);

        Assert.That(result.Pretranslations.Count, Is.EqualTo(20));
    }

    [Test]
    public async Task PreprocessAsync_TrainOnChapters()
    {
        using var env = new TestEnvironment();
        ParallelCorpusContract corpus = env.ParatextCorpus(
            trainOnChapters: new Dictionary<string, HashSet<int>> { { "MAT", [1] } },
            inferenceChapters: []
        );

        PreprocessResult result = await env.RunPreprocessAsync([corpus], useKeyTerms: false);

        (int src1Count, int src2Count, int trgCount, int termCount) = result.GetTrainCount();
        Assert.Multiple(() =>
        {
            Assert.That(src1Count, Is.EqualTo(5));
            Assert.That(src2Count, Is.EqualTo(0));
            Assert.That(trgCount, Is.EqualTo(0));
            Assert.That(termCount, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task PreprocessAsync_MixedSource_Paratext()
    {
        using var env = new TestEnvironment();
        ParallelCorpusContract corpus = env.DefaultMixedSourceParatextCorpus;

        PreprocessResult result = await env.RunPreprocessAsync([corpus], useKeyTerms: false);

        (int src1Count, int src2Count, int trgCount, int termCount) = result.GetTrainCount();
        Assert.Multiple(() =>
        {
            Assert.That(src1Count, Is.EqualTo(7));
            Assert.That(src2Count, Is.EqualTo(14));
            Assert.That(trgCount, Is.EqualTo(1));
            Assert.That(termCount, Is.EqualTo(0));
        });
        Assert.That(result.Pretranslations.Count, Is.EqualTo(21));
    }

    [Test]
    public async Task PreprocessAsync_MixedSource_Text()
    {
        using var env = new TestEnvironment();
        ParallelCorpusContract corpus = env.DefaultMixedSourceTextFileCorpus;

        PreprocessResult result = await env.RunPreprocessAsync([corpus]);

        (int src1Count, int src2Count, int trgCount, int termCount) = result.GetTrainCount();
        Assert.Multiple(() =>
        {
            Assert.That(src1Count, Is.EqualTo(1));
            Assert.That(src2Count, Is.EqualTo(4));
            Assert.That(trgCount, Is.EqualTo(1));
            Assert.That(termCount, Is.EqualTo(0));
        });
        Assert.That(result.Pretranslations.Count, Is.EqualTo(3));
    }

    [Test]
    public async Task PreprocessAsync_RemoveFreestandingEllipses()
    {
        using var env = new TestEnvironment();
        ParallelCorpusContract corpus = env.ParatextCorpus(
            trainOnChapters: new Dictionary<string, HashSet<int>> { { "MAT", [2] } },
            inferenceChapters: new Dictionary<string, HashSet<int>> { { "MAT", [2] } }
        );

        PreprocessResult result = await env.RunPreprocessAsync([corpus], useKeyTerms: false);

        string sourceExtract = result.GetSourceExtract();
        Assert.That(
            sourceExtract,
            Is.EqualTo(
                "Source one, chapter two, verse one.\nSource one, chapter two, verse two. \u201ca quotation\u201d\n\n"
            ),
            sourceExtract
        );
        string targetExtract = result.GetTargetExtract();
        Assert.That(
            targetExtract,
            Is.EqualTo(
                "Target one, chapter two, verse one.\n\nTarget one, chapter two, verse three. \"a quotation\"\n"
            ),
            targetExtract
        );
        Assert.That(result.Pretranslations.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task PreprocessAsync_ParallelCorpus()
    {
        using var env = new TestEnvironment();
        List<ParallelCorpusContract> corpora =
        [
            new ParallelCorpusContract()
            {
                Id = "1",
                SourceCorpora =
                [
                    new()
                    {
                        Id = "_1",
                        Language = "en",
                        Files = [env.ParatextFile("pt-source1")],
                        TrainOnChapters = new() { { "MAT", [1] }, { "LEV", [] } },
                        InferenceChapters = new() { { "1CH", [] } },
                    },
                    new()
                    {
                        Id = "_1",
                        Language = "en",
                        Files = [env.ParatextFile("pt-source2")],
                        TrainOnChapters = new() { { "MAT", [1] }, { "MRK", [] } },
                        InferenceChapters = new() { { "1CH", [] } },
                    },
                ],
                TargetCorpora =
                [
                    new()
                    {
                        Id = "_1",
                        Language = "en",
                        Files = [env.ParatextFile("pt-target1")],
                        TrainOnChapters = new() { { "MAT", [1] }, { "MRK", [] } },
                    },
                    new()
                    {
                        Id = "_2",
                        Language = "en",
                        Files = [env.ParatextFile("pt-target2")],
                        TrainOnChapters = new()
                        {
                            { "MAT", [1] },
                            { "MRK", [] },
                            { "LEV", [] },
                        },
                    },
                ],
            },
        ];

        PreprocessResult result = await env.RunPreprocessAsync(corpora, useKeyTerms: false);

        Assert.Multiple(() =>
        {
            string src = result.GetSourceExtract();
            Assert.That(
                src,
                Is.EqualTo(
                        @"Source one, chapter fourteen, verse fifty-five. Segment b.
Source one, chapter fourteen, verse fifty-six.
Source one, chapter one, verse one.
Source one, chapter one, verse two and three.
Source one, chapter one, verse four.
Source one, chapter one, verse five. Source two, chapter one, verse six.
Source two, chapter one, verse seven. Source two, chapter one, verse eight.
Source two, chapter one, verse nine. Source one, chapter one, verse ten.
Source two, chapter one, verse one.
"
                    )
                    .IgnoreLineEndings(),
                src
            );
            string trg = result.GetTargetExtract();
            Assert.That(
                trg,
                Is.EqualTo(
                        @"Target two, chapter fourteen, verse fifty-five.
Target two, chapter fourteen, verse fifty-six.
Target one, chapter one, verse one.
Target one, chapter one, verse two. Target one, chapter one, verse three.

Target one, chapter one, verse five and six.
Target one, chapter one, verse seven and eight.
Target one, chapter one, verse nine and ten.

"
                    )
                    .IgnoreLineEndings(),
                trg
            );
            Assert.That(result.Pretranslations.Count, Is.EqualTo(7));
            Assert.That(result.Pretranslations[2].Translation, Is.EqualTo("Source one, chapter twelve, verse one."));
        });
    }

    private record PretranslationEntry(string CorpusId, string TextId, IReadOnlyList<object> Refs, string Translation);

    private class PreprocessResult
    {
        public List<string> SourceLines { get; } = [];
        public List<string> TargetLines { get; } = [];
        public int TermCount { get; set; }
        public List<PretranslationEntry> Pretranslations { get; } = [];

        public (int Source1Count, int Source2Count, int TargetOnlyCount, int TermCount) GetTrainCount()
        {
            int src1 = 0,
                src2 = 0,
                trgOnly = 0;
            foreach (string line in SourceLines)
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("Source one"))
                    src1++;
                else if (trimmed.StartsWith("Source two"))
                    src2++;
                else if (trimmed.Length == 0)
                    trgOnly++;
            }
            return (src1, src2, trgOnly, TermCount);
        }

        public string GetSourceExtract()
        {
            return string.Join("\n", SourceLines) + "\n";
        }

        public string GetTargetExtract()
        {
            return string.Join("\n", TargetLines) + "\n";
        }
    }

    private class TestEnvironment : DisposableBase
    {
        private static readonly string TestDataPath = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "Services",
            "data"
        );
        private readonly TempDirectory _tempDir = new TempDirectory(name: "ParallelCorpusServiceTests");

        public IParallelCorpusService Processor { get; } = new ParallelCorpusService();

        public ParallelCorpusContract DefaultTextFileCorpus { get; } =
            new()
            {
                Id = "corpusId1",
                SourceCorpora =
                [
                    new()
                    {
                        Id = "src_1",
                        Language = "es",
                        Files = [TextFile("source1")],
                        TrainOnTextIds = [],
                        InferenceTextIds = [],
                    },
                ],
                TargetCorpora =
                [
                    new()
                    {
                        Id = "trg_1",
                        Language = "en",
                        Files = [TextFile("target1")],
                        TrainOnTextIds = [],
                    },
                ],
            };

        public ParallelCorpusContract DefaultMixedSourceTextFileCorpus { get; } =
            new()
            {
                Id = "corpusId1",
                SourceCorpora =
                [
                    new()
                    {
                        Id = "src_1",
                        Language = "es",
                        Files = [TextFile("source1"), TextFile("source2")],
                        TrainOnTextIds = null,
                        TrainOnChapters = null,
                        InferenceTextIds = null,
                        InferenceChapters = null,
                    },
                ],
                TargetCorpora =
                [
                    new()
                    {
                        Id = "trg_1",
                        Language = "en",
                        Files = [TextFile("target1")],
                        TrainOnChapters = null,
                        TrainOnTextIds = null,
                    },
                ],
            };

        public ParallelCorpusContract DefaultParatextCorpus =>
            new()
            {
                Id = "corpusId1",
                SourceCorpora =
                [
                    new()
                    {
                        Id = "src_1",
                        Language = "es",
                        Files = [ParatextFile("pt-source1")],
                        TrainOnTextIds = null,
                        InferenceTextIds = null,
                    },
                ],
                TargetCorpora =
                [
                    new()
                    {
                        Id = "trg_1",
                        Language = "en",
                        Files = [ParatextFile("pt-target1")],
                        TrainOnTextIds = null,
                    },
                ],
            };

        public ParallelCorpusContract DefaultMixedSourceParatextCorpus =>
            new()
            {
                Id = "corpusId1",
                SourceCorpora =
                [
                    new()
                    {
                        Id = "src_1",
                        Language = "es",
                        Files = [ParatextFile("pt-source1")],
                        TrainOnTextIds = null,
                        InferenceTextIds = null,
                    },
                    new()
                    {
                        Id = "src_1",
                        Language = "es",
                        Files = [ParatextFile("pt-source2")],
                        TrainOnTextIds = null,
                        InferenceTextIds = null,
                    },
                ],
                TargetCorpora =
                [
                    new()
                    {
                        Id = "trg_1",
                        Language = "en",
                        Files = [ParatextFile("pt-target1")],
                        TrainOnTextIds = null,
                    },
                ],
            };

        public static ParallelCorpusContract TextFileCorpus(
            HashSet<string>? trainOnTextIds,
            HashSet<string>? inferenceTextIds
        )
        {
            return new()
            {
                Id = "corpusId1",
                SourceCorpora =
                [
                    new()
                    {
                        Id = "src_1",
                        Language = "es",
                        Files = [TextFile("source1")],
                        TrainOnTextIds = trainOnTextIds,
                        InferenceTextIds = inferenceTextIds,
                    },
                ],
                TargetCorpora =
                [
                    new()
                    {
                        Id = "trg_1",
                        Language = "en",
                        Files = [TextFile("target1")],
                        TrainOnTextIds = trainOnTextIds,
                    },
                ],
            };
        }

        public ParallelCorpusContract ParatextCorpus(
            Dictionary<string, HashSet<int>>? trainOnChapters,
            Dictionary<string, HashSet<int>>? inferenceChapters
        )
        {
            return new()
            {
                Id = "corpusId1",
                SourceCorpora =
                [
                    new()
                    {
                        Id = "src_1",
                        Language = "es",
                        Files = [ParatextFile("pt-source1")],
                        TrainOnChapters = trainOnChapters,
                        InferenceChapters = inferenceChapters,
                    },
                ],
                TargetCorpora =
                [
                    new()
                    {
                        Id = "trg_1",
                        Language = "en",
                        Files = [ParatextFile("pt-target1")],
                        TrainOnChapters = trainOnChapters,
                    },
                ],
            };
        }

        public async Task<PreprocessResult> RunPreprocessAsync(
            IEnumerable<ParallelCorpusContract> corpora,
            bool useKeyTerms = true,
            HashSet<string>? ignoreUsfmMarkers = null
        )
        {
            var result = new PreprocessResult();
            await Processor.PreprocessAsync(
                corpora,
                (row, dataType) =>
                {
                    if (row.SourceSegment.Length > 0 || row.TargetSegment.Length > 0)
                    {
                        if (dataType == TrainingDataType.KeyTerm)
                        {
                            result.TermCount++;
                        }
                        else
                        {
                            result.SourceLines.Add(row.SourceSegment);
                            result.TargetLines.Add(row.TargetSegment);
                        }
                    }
                    return Task.CompletedTask;
                },
                (row, isInTrainingData, corpusId) =>
                {
                    if (row.SourceSegment.Length > 0 && !isInTrainingData)
                    {
                        result.Pretranslations.Add(
                            new PretranslationEntry(corpusId, row.TextId, row.TargetRefs, row.SourceSegment)
                        );
                    }
                    return Task.CompletedTask;
                },
                useKeyTerms,
                ignoreUsfmMarkers ?? ["rem", "r"]
            );
            return result;
        }

        public ParallelCorpusContract[] GetCorpora(bool paratextProject)
        {
            if (paratextProject)
            {
                return
                [
                    new ParallelCorpusContract
                    {
                        Id = "corpus1",
                        SourceCorpora =
                        [
                            new MonolingualCorpusContract
                            {
                                Id = "pt-source1",
                                Language = "en",
                                Files =
                                [
                                    new CorpusFileContract
                                    {
                                        TextId = "textId1",
                                        Format = FileFormat.Paratext,
                                        Location = ZipParatextProject("pt-source1"),
                                    },
                                ],
                                InferenceTextIds = [],
                            },
                        ],
                        TargetCorpora =
                        [
                            new MonolingualCorpusContract
                            {
                                Id = "pt-target1",
                                Language = "en",
                                Files =
                                [
                                    new CorpusFileContract
                                    {
                                        TextId = "textId1",
                                        Format = FileFormat.Paratext,
                                        Location = ZipParatextProject("pt-target1"),
                                    },
                                ],
                            },
                        ],
                    },
                    new ParallelCorpusContract
                    {
                        Id = "corpus2",
                        SourceCorpora =
                        [
                            new MonolingualCorpusContract
                            {
                                Id = "pt-source1",
                                Language = "en",
                                Files =
                                [
                                    new CorpusFileContract
                                    {
                                        TextId = "textId1",
                                        Format = FileFormat.Paratext,
                                        Location = ZipParatextProject("pt-source1"),
                                    },
                                ],
                                TrainOnTextIds = [],
                            },
                        ],
                        TargetCorpora =
                        [
                            new MonolingualCorpusContract
                            {
                                Id = "pt-target1",
                                Language = "en",
                                Files =
                                [
                                    new CorpusFileContract
                                    {
                                        TextId = "textId1",
                                        Format = FileFormat.Paratext,
                                        Location = ZipParatextProject("pt-target1"),
                                    },
                                ],
                                TrainOnTextIds = [],
                            },
                        ],
                    },
                ];
            }

            return
            [
                new ParallelCorpusContract
                {
                    Id = "corpus1",
                    SourceCorpora =
                    [
                        new MonolingualCorpusContract
                        {
                            Id = "source-corpus1",
                            Language = "en",
                            Files =
                            [
                                new CorpusFileContract
                                {
                                    TextId = "textId1",
                                    Format = FileFormat.Text,
                                    Location = Path.Combine(TestDataPath, "source1.txt"),
                                },
                            ],
                        },
                        new MonolingualCorpusContract
                        {
                            Id = "source-corpus2",
                            Language = "en",
                            Files =
                            [
                                new CorpusFileContract
                                {
                                    TextId = "textId1",
                                    Format = FileFormat.Text,
                                    Location = Path.Combine(TestDataPath, "source2.txt"),
                                },
                            ],
                        },
                    ],
                    TargetCorpora =
                    [
                        new MonolingualCorpusContract
                        {
                            Id = "target-corpus1",
                            Language = "en",
                            Files =
                            [
                                new CorpusFileContract
                                {
                                    TextId = "textId1",
                                    Format = FileFormat.Text,
                                    Location = Path.Combine(TestDataPath, "target1.txt"),
                                },
                            ],
                        },
                    ],
                },
            ];
        }

        public CorpusFileContract ParatextFile(string name)
        {
            return new()
            {
                TextId = name,
                Format = FileFormat.Paratext,
                Location = ZipParatextProject(name),
            };
        }

        private static CorpusFileContract TextFile(string name)
        {
            return new()
            {
                TextId = "textId1",
                Format = FileFormat.Text,
                Location = Path.Combine(TestDataPath, $"{name}.txt"),
            };
        }

        protected override void DisposeManagedResources()
        {
            _tempDir.Dispose();
        }

        private string ZipParatextProject(string name)
        {
            string fileName = Path.Combine(_tempDir.Path, $"{name}.zip");
            if (!File.Exists(fileName))
                ZipFile.CreateFromDirectory(Path.Combine(TestDataPath, name), fileName);
            return fileName;
        }
    }
}
