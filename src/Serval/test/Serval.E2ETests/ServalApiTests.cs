namespace Serval.E2ETests;

[TestFixture]
[Category("E2E")]
public class ServalApiTests
{
    private ServalClientHelper _helperClient;

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        _helperClient = new ServalClientHelper("https://serval-api.org/", ignoreSslErrors: true);
        await _helperClient.InitAsync();
    }

    [SetUp]
    public void Setup()
    {
        _helperClient.Setup();
    }

    [Test]
    [Obsolete("Legacy corpora are deprecated")]
    public async Task Echo_LegacyCorpus()
    {
        string engineId = await _helperClient.CreateNewEngineAsync("Echo", "es", "es", "Echo1");
        string[] books = ["1JN.txt", "2JN.txt", "3JN.txt"];
        string corpusId = await _helperClient.AddLegacyCorpusToEngineAsync(engineId, books, "es", "es", true);
        await _helperClient.BuildEngineAsync(engineId);

        // Test Pretranslation
        IList<Pretranslation> pretranslations =
            await _helperClient.TranslationEnginesClient.GetAllCorpusPretranslationsAsync(engineId, corpusId);
        Assert.That(pretranslations, Has.Count.GreaterThan(1));

        // Test Suggestion
        TranslationResult tResult = await _helperClient.TranslationEnginesClient.TranslateAsync(engineId, "Espíritu");
        Assert.That(tResult.Translation, Is.EqualTo("Espíritu"));
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Echo_ParallelCorpus(bool paratext)
    {
        string engineId = await _helperClient.CreateNewEngineAsync("Echo", "es", "es", "Echo2");
        string corpusId;
        if (paratext)
        {
            (string parallelCorpusId, ParallelCorpusConfig?) corpus =
                await _helperClient.AddParatextCorpusToEngineAsync(engineId, "es", "es", true);
            corpusId = corpus.parallelCorpusId;
        }
        else
        {
            string[] books = ["1JN.txt", "2JN.txt"];
            ParallelCorpusConfig trainCorpus = await _helperClient.MakeParallelTextCorpus(books, "es", "es", true);
            await _helperClient.AddParallelTextCorpusToEngineAsync(engineId, trainCorpus, false);
            books = ["3JN.txt"];
            ParallelCorpusConfig pretranslateCorpus = await _helperClient.MakeParallelTextCorpus(
                books,
                "es",
                "es",
                true
            );
            corpusId = await _helperClient.AddParallelTextCorpusToEngineAsync(engineId, pretranslateCorpus, true);
        }

        await _helperClient.BuildEngineAsync(engineId);

        // Test Pretranslation
        IList<Pretranslation> pretranslations = await _helperClient.TranslationEnginesClient.GetAllPretranslationsAsync(
            engineId,
            corpusId
        );
        Assert.That(pretranslations, Has.Count.GreaterThan(1));

        // Test Suggestion
        TranslationResult tResult = await _helperClient.TranslationEnginesClient.TranslateAsync(engineId, "Espíritu");
        Assert.That(tResult.Translation, Is.EqualTo("Espíritu"));
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Echo_WordAlignment(bool paratext)
    {
        string engineId = await _helperClient.CreateNewEngineAsync("EchoWordAlignment", "es", "en", "Echo4");
        if (paratext)
        {
            await _helperClient.AddParatextCorpusToEngineAsync(engineId, "es", "en", false);
        }
        else
        {
            string[] books = ["1JN.txt", "2JN.txt", "3JN.txt"];
            ParallelCorpusConfig trainCorpus = await _helperClient.MakeParallelTextCorpus(books, "es", "en", false);
            await _helperClient.AddParallelTextCorpusToEngineAsync(engineId, trainCorpus, false);
        }

        await _helperClient.BuildEngineAsync(engineId);
        WordAlignmentResult tResult = await _helperClient.WordAlignmentEnginesClient.AlignAsync(
            engineId,
            new WordAlignmentRequest { SourceSegment = "espíritu verdad", TargetSegment = "espíritu verdad" }
        );
        AlignedWordPair pair = tResult.Alignment.First();
        Assert.Multiple(() =>
        {
            Assert.That(pair.SourceIndex, Is.EqualTo(0));
            Assert.That(pair.TargetIndex, Is.EqualTo(0));
            Assert.That(pair.Score, Is.EqualTo(1.0).Within(1e-6)); // tolerate tiny fp deviations
        });
    }

    [Test]
    public async Task Nmt_Batch()
    {
        string engineId = await _helperClient.CreateNewEngineAsync("Nmt", "es", "en", "NMT1");
        string[] books = ["MAT.txt", "1JN.txt", "2JN.txt"];
        ParallelCorpusConfig trainCorpus = await _helperClient.MakeParallelTextCorpus(books, "es", "en", false);
        string cId1 = await _helperClient.AddParallelTextCorpusToEngineAsync(engineId, trainCorpus, false);
        books = ["2JN.txt", "3JN.txt"];
        ParallelCorpusConfig pretranslateCorpus = await _helperClient.MakeParallelTextCorpus(books, "es", "en", true);
        string cId2 = await _helperClient.AddParallelTextCorpusToEngineAsync(engineId, pretranslateCorpus, false);
        _helperClient.TranslationBuildConfig.TrainOn =
        [
            new TrainingCorpusConfig
            {
                ParallelCorpusId = cId1,
                SourceFilters =
                [
                    new ParallelCorpusFilterConfig
                    {
                        CorpusId = trainCorpus.SourceCorpusIds.Single(),
                        TextIds = ["1JN.txt"]
                    },
                ],
                TargetFilters =
                [
                    new ParallelCorpusFilterConfig
                    {
                        CorpusId = trainCorpus.TargetCorpusIds.Single(),
                        TextIds = ["1JN.txt"]
                    },
                ],
            }
        ];
        _helperClient.TranslationBuildConfig.Pretranslate =
        [
            new PretranslateCorpusConfig
            {
                ParallelCorpusId = cId2,
                SourceFilters =
                [
                    new ParallelCorpusFilterConfig
                    {
                        CorpusId = pretranslateCorpus.SourceCorpusIds.Single(),
                        TextIds = ["2JN.txt"]
                    },
                ],
            }
        ];

        // Validate that a build can be started and canceled twice
        await StartAndCancelTwiceAsync(engineId);

        // Validate an NMT build using text files
        string buildId = await _helperClient.BuildEngineAsync(engineId);
        await Task.Delay(1000);
        IList<Pretranslation> lTrans1 = await _helperClient.TranslationEnginesClient.GetAllPretranslationsAsync(
            engineId,
            cId1
        );
        Assert.That(lTrans1, Has.Count.EqualTo(0)); // should be nothing
        IList<Pretranslation> lTrans2 = await _helperClient.TranslationEnginesClient.GetAllPretranslationsAsync(
            engineId,
            cId2
        );

        TranslationBuild build = await _helperClient.TranslationEnginesClient.GetBuildAsync(engineId, buildId);
        Assert.That(build.ExecutionData, Is.Not.Null);

        var executionData = build.ExecutionData;

        Assert.That(executionData, Contains.Key("trainCount"));
        Assert.That(executionData, Contains.Key("pretranslateCount"));

        int trainCount = Convert.ToInt32(executionData["trainCount"], CultureInfo.InvariantCulture);
        int pretranslateCount = Convert.ToInt32(executionData["pretranslateCount"], CultureInfo.InvariantCulture);

        Assert.That(trainCount, Is.GreaterThan(0));
        Assert.That(pretranslateCount, Is.GreaterThan(0));

        Assert.That(lTrans2, Has.Count.EqualTo(13)); // just 2 John
    }

    [Test]
    public async Task Nmt_LargeBatchAndDownload()
    {
        string engineId = await _helperClient.CreateNewEngineAsync("Nmt", "es", "en", "NMT3", isModelPersisted: true);
        string[] books = ["bible_LARGEFILE.txt"];
        ParallelCorpusConfig trainCorpus = await _helperClient.MakeParallelTextCorpus(books, "es", "en", false);
        ParallelCorpusConfig pretranslateCorpus = await _helperClient.MakeParallelTextCorpus(
            ["3JN.txt"],
            "es",
            "en",
            true
        );
        await _helperClient.AddParallelTextCorpusToEngineAsync(engineId, trainCorpus, false);
        string cId = await _helperClient.AddParallelTextCorpusToEngineAsync(engineId, pretranslateCorpus, true);
        await _helperClient.BuildEngineAsync(engineId);
        await Task.Delay(1000);
        IList<Pretranslation> lTrans = await _helperClient.TranslationEnginesClient.GetAllPretranslationsAsync(
            engineId,
            cId
        );
        Assert.That(lTrans, Has.Count.EqualTo(14));
        // Download the model from the s3 bucket
        ModelDownloadUrl url = await _helperClient.TranslationEnginesClient.GetModelDownloadUrlAsync(engineId);
        using Task<Stream> s = new HttpClient().GetStreamAsync(url.Url);
        using var ms = new MemoryStream();
        s.Result.CopyTo(ms);
        Assert.That(ms.Length, Is.GreaterThan(1_000_000));
    }

    [Test]
    public async Task Nmt_Paratext()
    {
        const string SourceLanguageCode = "en";
        const string TargetLanguageCode = "sbp";
        string engineId = await _helperClient.CreateNewEngineAsync(
            "Nmt",
            SourceLanguageCode,
            TargetLanguageCode,
            "NMT4"
        );
        (string parallelCorpusId, ParallelCorpusConfig parallelCorpusConfig) =
            await _helperClient.AddParatextCorpusToEngineAsync(engineId, SourceLanguageCode, TargetLanguageCode, false);

        _helperClient.TranslationBuildConfig.Pretranslate!.Add(
            new PretranslateCorpusConfig
            {
                ParallelCorpusId = parallelCorpusId,
                SourceFilters =
                [
                    new ParallelCorpusFilterConfig
                    {
                        CorpusId = parallelCorpusConfig.SourceCorpusIds.Single(),
                        ScriptureRange = "1JN"
                    },
                ]
            }
        );
        _helperClient.TranslationBuildConfig.Options =
            "{\"max_steps\":10, \"use_key_terms\":true, \"train_params\": {\"per_device_train_batch_size\":4}}";

        await _helperClient.BuildEngineAsync(engineId);
        Assert.That(
            (await _helperClient.TranslationEnginesClient.GetAllBuildsAsync(engineId)).First().State,
            Is.EqualTo(JobState.Completed)
        );
        IList<Pretranslation> lTrans = await _helperClient.TranslationEnginesClient.GetAllPretranslationsAsync(
            engineId,
            parallelCorpusId
        );
        Assert.That(lTrans, Is.Not.Empty);
        string usfm = await _helperClient.TranslationEnginesClient.GetPretranslatedUsfmAsync(
            engineId,
            parallelCorpusId,
            "1JN"
        );
        Assert.That(usfm, Does.Contain("\\v 1"));
    }

    [Test]
    public async Task Nmt_QueueMultiple()
    {
        const int NumberOfEngines = 10;
        const int NumberOfWorkers = 8;
        string[] engineIds = new string[NumberOfEngines];
        string[] books = ["MAT.txt", "1JN.txt", "2JN.txt"];
        ParallelCorpusConfig trainCorpus = await _helperClient.MakeParallelTextCorpus(books, "es", "en", false);
        ParallelCorpusConfig pretranslateCorpus = await _helperClient.MakeParallelTextCorpus(
            ["3JN.txt"],
            "es",
            "en",
            true
        );

        // Verify the corpora are readable
        IList<Corpus> allCorpora = await _helperClient.CorporaClient.GetAllAsync();
        Assert.That(allCorpora, Has.Count.GreaterThan(0));

        for (int i = 0; i < NumberOfEngines; i++)
        {
            _helperClient.InitTranslationBuildConfig();
            engineIds[i] = await _helperClient.CreateNewEngineAsync("Nmt", "es", "en", $"NMT1_{i}");
            string engineId = engineIds[i];
            await _helperClient.AddParallelTextCorpusToEngineAsync(engineId, trainCorpus, false);
            await _helperClient.AddParallelTextCorpusToEngineAsync(engineId, pretranslateCorpus, true);
            await _helperClient.StartTranslationBuildAsync(engineId);
            // Ensure that tasks are enqueued roughly in order
            await Task.Delay(1_000);
        }

        // Wait for at least some tasks to be queued
        await Task.Delay(4_000);
        string builds = string.Empty;
        for (int i = 0; i < NumberOfEngines; i++)
        {
            TranslationBuild build = await _helperClient.TranslationEnginesClient.GetCurrentBuildAsync(engineIds[i]);
            builds += $"{JsonSerializer.Serialize(build)}\n";
        }

        builds +=
            "Depth = "
            + (await _helperClient.TranslationEngineTypesClient.GetQueueAsync("Nmt")).Size.ToString(
                provider: CultureInfo.InvariantCulture
            );

        const int Tries = 5;
        for (int i = 0; i < Tries; i++)
        {
            //Status message of last started build says that there is at least one job ahead of it in the queue
            // (this variable due to how many jobs may already exist in the production queue from other Serval instances)
            TranslationBuild newestEngineCurrentBuild =
                await _helperClient.TranslationEnginesClient.GetCurrentBuildAsync(engineIds[NumberOfEngines - 1]);
            int? queueDepth = newestEngineCurrentBuild.QueueDepth;
            Queue queue = await _helperClient.TranslationEngineTypesClient.GetQueueAsync("Nmt");
            if (queueDepth is null)
            {
                await Task.Delay(2_000);
                continue;
            }
            Assert.That(
                queueDepth,
                Is.Not.Null,
                message: JsonSerializer.Serialize(newestEngineCurrentBuild) + "|||" + builds
            );
            Assert.Multiple(() =>
            {
                Assert.That(queueDepth, Is.GreaterThan(0), message: builds);
                Assert.That(queue.Size, Is.GreaterThanOrEqualTo(NumberOfEngines - NumberOfWorkers));
            });
            break;
        }

        for (int i = 0; i < NumberOfEngines; i++)
        {
            try
            {
                TranslationBuild currentBuild = await _helperClient.TranslationEnginesClient.GetCurrentBuildAsync(
                    engineIds[i]
                );
                TranslationBuild canceledBuild = await _helperClient.TranslationEnginesClient.CancelBuildAsync(
                    engineIds[i]
                );
                Assert.That(currentBuild.Id, Is.EqualTo(canceledBuild.Id));
            }
            catch (ServalApiException ex) when (ex.StatusCode == 204) { }
        }
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Smt(bool legacyCorpus)
    {
        string engineId = await _helperClient.CreateNewEngineAsync("SmtTransfer", "es", "en", "SMT1");

        // Validate that get word graph fails when the engine is not built
        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            await _helperClient.TranslationEnginesClient.GetWordGraphAsync(engineId, "verdad");
        });
        Assert.That(ex, Is.Not.Null);
        Assert.That(ex.StatusCode, Is.EqualTo(409));

        // Validate that a build can be started and canceled twice
        await StartAndCancelTwiceAsync(engineId);

        // Validate suggestion where one word is the corpus
        string corpusId1 = await _helperClient.AddTextCorpusToEngineAsync(
            engineId,
            ["3JN"],
            "es",
            "en",
            false,
            legacyCorpus
        );
        await _helperClient.BuildEngineAsync(engineId);
        TranslationResult tResult = await _helperClient.TranslationEnginesClient.TranslateAsync(
            engineId,
            "verdad mundo"
        );
        Assert.That(tResult.Translation, Is.EqualTo("truth mundo"));

        // Validate suggestion where both words are in the corpus
        string corpusId2 = await _helperClient.AddTextCorpusToEngineAsync(
            engineId,
            ["1JN", "2JN"],
            "es",
            "en",
            false,
            legacyCorpus
        );
        await _helperClient.BuildEngineAsync(engineId);
        TranslationResult tResult2 = await _helperClient.TranslationEnginesClient.TranslateAsync(
            engineId,
            "verdad mundo"
        );
        Assert.That(tResult2.Translation, Is.EqualTo("truth world"));

        // Validate addition of a new segment
        TranslationResult tResult3 = await _helperClient.TranslationEnginesClient.TranslateAsync(
            engineId,
            "ungidos espíritu"
        );
        Assert.That(tResult3.Translation, Is.EqualTo("ungidos spirit"));
        await _helperClient.TranslationEnginesClient.TrainSegmentAsync(
            engineId,
            new SegmentPair
            {
                SourceSegment = "ungidos espíritu",
                TargetSegment = "unction spirit",
                SentenceStart = true
            }
        );
        TranslationResult tResult4 = await _helperClient.TranslationEnginesClient.TranslateAsync(
            engineId,
            "ungidos espíritu"
        );
        Assert.That(tResult4.Translation, Is.EqualTo("unction spirit"));

        // Validate top `N` translations
        const int N = 3;
        ICollection<TranslationResult> results = await _helperClient.TranslationEnginesClient.TranslateNAsync(
            engineId,
            N,
            "amor"
        );
        Assert.That(
            results.MaxBy(t => t.Confidences.Average())?.Translation.Contains("love") ?? false,
            message: "Expected best translation to contain 'love' but results were this:\n"
                + JsonSerializer.Serialize(results)
        );

        // Validate confidence and corpus size
        var engine = await _helperClient.TranslationEnginesClient.GetAsync(engineId);
        Assert.That(engine.Confidence, Is.GreaterThan(25));
        Assert.That(engine.CorpusSize, Is.EqualTo(133));

        // Validate get word graph works after corpora removal then re-adding
        await _helperClient.DeleteCorpusAsync(engineId, corpusId1, legacyCorpus);
        await _helperClient.DeleteCorpusAsync(engineId, corpusId2, legacyCorpus);
        await _helperClient.AddTextCorpusToEngineAsync(
            engineId,
            ["1JN", "2JN", "3JN"],
            "es",
            "en",
            false,
            legacyCorpus
        );
        await _helperClient.BuildEngineAsync(engineId);

        WordGraph result = await _helperClient.TranslationEnginesClient.GetWordGraphAsync(engineId, "verdad");
        Assert.That(result.SourceTokens, Has.Count.EqualTo(1));
        Assert.That(
            result.Arcs.MaxBy(arc => arc.Confidences.Average())?.TargetTokens.All(tk => tk == "truth"),
            Is.True,
            message: $"Best translation should have been 'truth' but returned word graph: \n{JsonSerializer.Serialize(result)}"
        );
    }

    [Test]
    public async Task WordAlignment()
    {
        string engineId = await _helperClient.CreateNewEngineAsync("Statistical", "es", "en", "STAT1");
        string[] books = ["1JN.txt", "2JN.txt", "MAT.txt"];
        ParallelCorpusConfig trainCorpus = await _helperClient.MakeParallelTextCorpus(books, "es", "en", false);
        ParallelCorpusConfig testCorpus = await _helperClient.MakeParallelTextCorpus(["3JN.txt"], "es", "en", false);
        string trainCorpusId = await _helperClient.AddParallelTextCorpusToEngineAsync(engineId, trainCorpus, false);
        string corpusId = await _helperClient.AddParallelTextCorpusToEngineAsync(engineId, testCorpus, true);
        _helperClient.WordAlignmentBuildConfig.TrainOn =
        [
            new TrainingCorpusConfig() { ParallelCorpusId = trainCorpusId }
        ];
        _helperClient.WordAlignmentBuildConfig.WordAlignOn =
        [
            new WordAlignmentCorpusConfig() { ParallelCorpusId = corpusId }
        ];
        string buildId = await _helperClient.BuildEngineAsync(engineId);
        WordAlignmentResult tResult = await _helperClient.WordAlignmentEnginesClient.AlignAsync(
            engineId,
            new WordAlignmentRequest() { SourceSegment = "espíritu verdad", TargetSegment = "spirit truth" }
        );

        Assert.That(tResult.Alignment, Has.Count.EqualTo(2));

        AlignedWordPair firstPair = tResult.Alignment[0];
        AlignedWordPair secondPair = tResult.Alignment[1];

        Assert.Multiple(() =>
        {
            Assert.That(firstPair.SourceIndex, Is.EqualTo(0));
            Assert.That(firstPair.TargetIndex, Is.EqualTo(0));
            Assert.That(firstPair.Score, Is.EqualTo(0.9).Within(0.1));
        });

        Assert.Multiple(() =>
        {
            Assert.That(secondPair.SourceIndex, Is.EqualTo(1));
            Assert.That(secondPair.TargetIndex, Is.EqualTo(1));
            Assert.That(secondPair.Score, Is.EqualTo(0.9).Within(0.1));
        });

        WordAlignmentBuild build = await _helperClient.WordAlignmentEnginesClient.GetBuildAsync(engineId, buildId);
        Assert.That(build.ExecutionData, Is.Not.Null);

        var executionData = build.ExecutionData;

        Assert.That(executionData, Contains.Key("trainCount"));
        Assert.That(executionData, Contains.Key("wordAlignCount"));

        int trainCount = Convert.ToInt32(executionData["trainCount"], CultureInfo.InvariantCulture);
        int wordAlignmentCount = Convert.ToInt32(executionData["wordAlignCount"], CultureInfo.InvariantCulture);

        Assert.That(trainCount, Is.GreaterThan(0));
        Assert.That(wordAlignmentCount, Is.GreaterThan(0));

        IList<Client.WordAlignment> wordAlignments =
            await _helperClient.WordAlignmentEnginesClient.GetAllWordAlignmentsAsync(engineId, corpusId);
        Assert.That(wordAlignments, Has.Count.EqualTo(14)); //Number of verses in 3JN
    }

    [TearDown]
    public async Task TearDown()
    {
        await _helperClient.TearDown();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _helperClient.DisposeAsync();
    }

    private async Task StartAndCancelTwiceAsync(string engineId)
    {
        // start and first job
        TranslationBuild build = await _helperClient.StartTranslationBuildAsync(engineId);
        await Task.Delay(1000);
        build = await _helperClient.TranslationEnginesClient.GetBuildAsync(engineId, build.Id);
        Assert.That(build.State == JobState.Active || build.State == JobState.Pending);

        // and then cancel it
        await _helperClient.CancelBuildAsync(engineId, build.Id);
        build = await _helperClient.TranslationEnginesClient.GetBuildAsync(engineId, build.Id);
        Assert.That(build.State == JobState.Canceled);

        // do a second job normally and make sure it works.
        build = await _helperClient.StartTranslationBuildAsync(engineId);
        await Task.Delay(1000);
        build = await _helperClient.TranslationEnginesClient.GetBuildAsync(engineId, build.Id);
        Assert.That(build.State == JobState.Active || build.State == JobState.Pending);

        // and cancel again - let's not wait forever
        await _helperClient.CancelBuildAsync(engineId, build.Id);
        build = await _helperClient.TranslationEnginesClient.GetBuildAsync(engineId, build.Id);
        Assert.That(build.State == JobState.Canceled);
    }
}
