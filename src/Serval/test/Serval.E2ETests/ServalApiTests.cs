namespace Serval.E2ETests;

#pragma warning disable CS0612 // Type or member is obsolete

[TestFixture]
[Category("E2E")]
public class ServalApiTests
{
    private ServalClientHelper _helperClient;

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        _helperClient = new ServalClientHelper("https://serval-api.org/", ignoreSSLErrors: true);
        await _helperClient.InitAsync();
    }

    [SetUp]
    public void Setup()
    {
        _helperClient.Setup();
    }

    [Test]
    public async Task GetEchoSuggestion()
    {
        string engineId = await _helperClient.CreateNewEngineAsync("Echo", "es", "es", "Echo1");
        string[] books = ["1JN.txt", "2JN.txt", "3JN.txt"];
        await _helperClient.AddTextCorpusToEngineAsync(engineId, books, "es", "es", false);
        await _helperClient.BuildEngineAsync(engineId);
        TranslationResult tResult = await _helperClient.TranslationEnginesClient.TranslateAsync(engineId, "Espíritu");
        Assert.That(tResult.Translation, Is.EqualTo("Espíritu"));
    }

    [Test]
    public async Task GetEchoPretranslate()
    {
        string engineId = await _helperClient.CreateNewEngineAsync("Echo", "es", "es", "Echo2");
        string[] books = ["1JN.txt", "2JN.txt"];
        await _helperClient.AddTextCorpusToEngineAsync(engineId, books, "es", "es", false);
        books = ["3JN.txt"];
        string corpusId = await _helperClient.AddTextCorpusToEngineAsync(engineId, books, "es", "es", true);
        await _helperClient.BuildEngineAsync(engineId);
        IList<Pretranslation> pretranslations = await _helperClient.TranslationEnginesClient.GetAllPretranslationsAsync(
            engineId,
            corpusId
        );
        Assert.That(pretranslations, Has.Count.GreaterThan(1));
    }

    [Test]
    public async Task GetEchoWordAlignment()
    {
        string engineId = await _helperClient.CreateNewEngineAsync("EchoWordAlignment", "es", "es", "Echo3");
        string[] books = ["1JN.txt", "2JN.txt", "3JN.txt"];
        ParallelCorpusConfig train_corpus = await _helperClient.MakeParallelTextCorpus(books, "es", "en", false);
        await _helperClient.AddParallelTextCorpusToEngineAsync(engineId, train_corpus, false);
        await _helperClient.BuildEngineAsync(engineId);
        WordAlignmentResult tResult = await _helperClient.WordAlignmentEnginesClient.AlignAsync(
            engineId,
            new WordAlignmentRequest() { SourceSegment = "espíritu verdad", TargetSegment = "espíritu verdad" }
        );
        Assert.That(
            tResult.Alignment.First(),
            Is.EqualTo(
                new AlignedWordPair()
                {
                    SourceIndex = 0,
                    TargetIndex = 0,
                    Score = 1.0
                }
            )
        );
    }

    [Test]
    public async Task GetSmtTranslation()
    {
        string engineId = await _helperClient.CreateNewEngineAsync("SmtTransfer", "es", "en", "SMT1");
        string[] books = ["1JN.txt", "2JN.txt", "3JN.txt"];
        await _helperClient.AddTextCorpusToEngineAsync(engineId, books, "es", "en", false);
        await _helperClient.BuildEngineAsync(engineId);
        TranslationResult tResult = await _helperClient.TranslationEnginesClient.TranslateAsync(engineId, "Espíritu");
        Assert.That(tResult.Translation.Contains("spirit"));
        var engine = await _helperClient.TranslationEnginesClient.GetAsync(engineId);
        Assert.That(engine.Confidence, Is.GreaterThan(25));
        Assert.That(engine.CorpusSize, Is.EqualTo(132));
    }

    [Test]
    public async Task GetSmtAddSegment()
    {
        string engineId = await _helperClient.CreateNewEngineAsync("SmtTransfer", "es", "en", "SMT3");
        string[] books = ["1JN.txt", "2JN.txt", "3JN.txt"];
        await _helperClient.AddTextCorpusToEngineAsync(engineId, books, "es", "en", false);
        await _helperClient.BuildEngineAsync(engineId);
        TranslationResult tResult = await _helperClient.TranslationEnginesClient.TranslateAsync(
            engineId,
            "ungidos espíritu"
        );
        Assert.That(tResult.Translation, Is.EqualTo("ungidos spirit"));
        await _helperClient.TranslationEnginesClient.TrainSegmentAsync(
            engineId,
            new SegmentPair
            {
                SourceSegment = "ungidos espíritu",
                TargetSegment = "unction spirit",
                SentenceStart = true
            }
        );
        TranslationResult tResult2 = await _helperClient.TranslationEnginesClient.TranslateAsync(
            engineId,
            "ungidos espíritu"
        );
        Assert.That(tResult2.Translation, Is.EqualTo("unction spirit"));
    }

    [Test]
    public async Task GetSmtMoreCorpus()
    {
        string engineId = await _helperClient.CreateNewEngineAsync("SmtTransfer", "es", "en", "SMT4");
        await _helperClient.AddTextCorpusToEngineAsync(engineId, ["3JN.txt"], "es", "en", false);
        await _helperClient.BuildEngineAsync(engineId);
        TranslationResult tResult = await _helperClient.TranslationEnginesClient.TranslateAsync(
            engineId,
            "verdad mundo"
        );
        Assert.That(tResult.Translation, Is.EqualTo("truth mundo"));
        await _helperClient.AddTextCorpusToEngineAsync(engineId, ["1JN.txt", "2JN.txt"], "es", "en", false);
        await _helperClient.BuildEngineAsync(engineId);
        TranslationResult tResult2 = await _helperClient.TranslationEnginesClient.TranslateAsync(
            engineId,
            "verdad mundo"
        );
        Assert.That(tResult2.Translation, Is.EqualTo("truth world"));
    }

    [Test]
    public async Task NmtBatch()
    {
        string engineId = await _helperClient.CreateNewEngineAsync("Nmt", "es", "en", "NMT1");
        string[] books = ["MAT.txt", "1JN.txt", "2JN.txt"];
        string cId1 = await _helperClient.AddTextCorpusToEngineAsync(engineId, books, "es", "en", false);
        _helperClient.TranslationBuildConfig.TrainOn = [new() { CorpusId = cId1, TextIds = ["1JN.txt"] }];
        string cId2 = await _helperClient.AddTextCorpusToEngineAsync(
            engineId,
            ["2JN.txt", "3JN.txt"],
            "es",
            "en",
            true
        );
        _helperClient.TranslationBuildConfig.Pretranslate = [new() { CorpusId = cId2, TextIds = ["2JN.txt"] }];
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
    public async Task NmtQueueMultiple()
    {
        const int NUM_ENGINES = 10;
        const int NUM_WORKERS = 8;
        string[] engineIds = new string[NUM_ENGINES];
        string[] books = ["MAT.txt", "1JN.txt", "2JN.txt"];
        ParallelCorpusConfig train_corpus = await _helperClient.MakeParallelTextCorpus(books, "es", "en", false);
        ParallelCorpusConfig pretranslate_corpus = await _helperClient.MakeParallelTextCorpus(
            ["3JN.txt"],
            "es",
            "en",
            true
        );
        // Verify the corpora are readable
        var allCorpora = await _helperClient.CorporaClient.GetAllAsync();
        Assert.That(allCorpora, Has.Count.GreaterThan(0));

        for (int i = 0; i < NUM_ENGINES; i++)
        {
            _helperClient.InitTranslationBuildConfig();
            engineIds[i] = await _helperClient.CreateNewEngineAsync("Nmt", "es", "en", $"NMT1_{i}");
            string engineId = engineIds[i];
            await _helperClient.AddParallelTextCorpusToEngineAsync(engineId, train_corpus, false);
            await _helperClient.AddParallelTextCorpusToEngineAsync(engineId, pretranslate_corpus, true);
            await _helperClient.StartTranslationBuildAsync(engineId);
            //Ensure that tasks are enqueued roughly in order
            await Task.Delay(1_000);
        }
        //Wait for at least some tasks to be queued
        await Task.Delay(4_000);
        string builds = "";
        for (int i = 0; i < NUM_ENGINES; i++)
        {
            TranslationBuild build = await _helperClient.TranslationEnginesClient.GetCurrentBuildAsync(engineIds[i]);
            builds += $"{JsonSerializer.Serialize(build)}\n";
        }

        builds +=
            "Depth = "
            + (await _helperClient.TranslationEngineTypesClient.GetQueueAsync("Nmt")).Size.ToString(
                provider: CultureInfo.InvariantCulture
            );

        int tries = 5;
        for (int i = 0; i < tries; i++)
        {
            //Status message of last started build says that there is at least one job ahead of it in the queue
            // (this variable due to how many jobs may already exist in the production queue from other Serval instances)
            TranslationBuild newestEngineCurrentBuild =
                await _helperClient.TranslationEnginesClient.GetCurrentBuildAsync(engineIds[NUM_ENGINES - 1]);
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
                Assert.That(queue.Size, Is.GreaterThanOrEqualTo(NUM_ENGINES - NUM_WORKERS));
            });
            break;
        }
        for (int i = 0; i < NUM_ENGINES; i++)
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

    [Test]
    public async Task NmtLargeBatchAndDownload()
    {
        string engineId = await _helperClient.CreateNewEngineAsync("Nmt", "es", "en", "NMT3", isModelPersisted: true);
        TranslationEngine engine = await _helperClient.TranslationEnginesClient.GetAsync(engineId);
        Assert.That(engine.IsModelPersisted, Is.True);
        string[] books = ["bible_LARGEFILE.txt"];
        ParallelCorpusConfig train_corpus = await _helperClient.MakeParallelTextCorpus(books, "es", "en", false);
        ParallelCorpusConfig pretranslate_corpus = await _helperClient.MakeParallelTextCorpus(
            ["3JN.txt"],
            "es",
            "en",
            true
        );
        await _helperClient.AddParallelTextCorpusToEngineAsync(engineId, train_corpus, false);
        string cId = await _helperClient.AddParallelTextCorpusToEngineAsync(engineId, pretranslate_corpus, true);
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
    public async Task GetNmtCancelAndRestartBuild()
    {
        string engineId = await _helperClient.CreateNewEngineAsync("Nmt", "es", "en", "NMT2");
        TranslationEngine engine = await _helperClient.TranslationEnginesClient.GetAsync(engineId);
        // NMT engines auto-fill IsModelPersisted as true
        Assert.That(engine.IsModelPersisted, Is.False);
        string[] books = ["1JN.txt", "2JN.txt", "3JN.txt"];
        await _helperClient.AddTextCorpusToEngineAsync(engineId, books, "es", "en", false);
        await StartAndCancelTwice(engineId);
    }

    [Test]
    public async Task CircuitousRouteGetWordGraphAsync()
    {
        //Create smt engine
        string smtEngineId = await _helperClient.CreateNewEngineAsync("SmtTransfer", "es", "en", "SMT5");
        TranslationEngine engine = await _helperClient.TranslationEnginesClient.GetAsync(smtEngineId);
        Assert.That(engine.IsModelPersisted, Is.True); // SMT engines auto-fill IsModelPersisted as true

        //Try to get word graph - should fail: the engine is not built
        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            await _helperClient.TranslationEnginesClient.GetWordGraphAsync(smtEngineId, "verdad");
        });
        Assert.That(ex, Is.Not.Null);
        Assert.That(ex.StatusCode, Is.EqualTo(409));

        //Add corpus
        var corpus1 = await _helperClient.MakeParallelTextCorpus(["2JN.txt", "3JN.txt"], "es", "en", false);
        string cId = await _helperClient.AddParallelTextCorpusToEngineAsync(smtEngineId, corpus1, false);

        //Build the new engine
        await _helperClient.BuildEngineAsync(smtEngineId);

        //Remove added corpus (shouldn't affect translation)
        await _helperClient.TranslationEnginesClient.DeleteParallelCorpusAsync(smtEngineId, cId);

        // Add corpus
        var corpus2 = await _helperClient.MakeParallelTextCorpus(["1JN.txt", "2JN.txt", "3JN.txt"], "es", "en", false);
        await _helperClient.AddParallelTextCorpusToEngineAsync(smtEngineId, corpus2, false);

        //Build the new engine
        await _helperClient.BuildEngineAsync(smtEngineId);

        WordGraph result = await _helperClient.TranslationEnginesClient.GetWordGraphAsync(smtEngineId, "verdad");
        Assert.That(result.SourceTokens, Has.Count.EqualTo(1));
        Assert.That(
            result.Arcs.MaxBy(arc => arc.Confidences.Average())?.TargetTokens.All(tk => tk == "truth"),
            Is.True,
            message: $"Best translation should have been 'truth'but returned word graph: \n{JsonSerializer.Serialize(result)}"
        );
    }

    [Test]
    public async Task CircuitousRouteTranslateTopNAsync()
    {
        const int N = 3;

        //Create engine
        string engineId = await _helperClient.CreateNewEngineAsync("SmtTransfer", "en", "fa", "SMT6");

        //Retrieve engine
        TranslationEngine engine = await _helperClient.TranslationEnginesClient.GetAsync(engineId);
        Assert.That(engine.Type, Is.EqualTo("smt-transfer"));

        //Add corpus
        string cId = await _helperClient.AddTextCorpusToEngineAsync(
            engineId,
            ["1JN.txt", "2JN.txt", "3JN.txt"],
            "en",
            "fa",
            false
        );

        //Retrieve corpus
        TranslationCorpus corpus = await _helperClient.TranslationEnginesClient.GetCorpusAsync(engineId, cId);
        Assert.That(corpus.SourceLanguage, Is.EqualTo("en"));
        Assert.That(corpus.TargetFiles, Has.Count.EqualTo(3));

        //Build engine
        await _helperClient.BuildEngineAsync(engineId);

        //Get top `N` translations
        ICollection<TranslationResult> results = await _helperClient.TranslationEnginesClient.TranslateNAsync(
            engineId,
            N,
            "love"
        );
        Assert.That(
            results.MaxBy(t => t.Confidences.Average())?.Translation.Contains("amour") ?? false,
            message: "Expected best translation to contain 'amour' but results were this:\n"
                + JsonSerializer.Serialize(results)
        );
    }

    [Test]
    public async Task GetSmtCancelAndRestartBuild()
    {
        string engineId = await _helperClient.CreateNewEngineAsync("SmtTransfer", "es", "en", "SMT7");
        string[] books = ["1JN.txt", "2JN.txt", "3JN.txt"];
        await _helperClient.AddTextCorpusToEngineAsync(engineId, books, "es", "en", false);

        await StartAndCancelTwice(engineId);

        // do a job normally and make sure it works.
        await _helperClient.BuildEngineAsync(engineId);
        TranslationResult tResult = await _helperClient.TranslationEnginesClient.TranslateAsync(engineId, "Espíritu");
        Assert.That(tResult.Translation.Contains("spirit"));
    }

    async Task StartAndCancelTwice(string engineId)
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

    [Test]
    public async Task ParatextProjectNmtJobAsync()
    {
        string tempDirectory = Path.GetTempPath();
        DataFile file1,
            file2;
        try
        {
            ZipFile.CreateFromDirectory(
                Path.Combine("..", "..", "..", "data", "TestProject"),
                Path.Combine(tempDirectory, "TestProject.zip")
            );
            ZipFile.CreateFromDirectory(
                Path.Combine("..", "..", "..", "data", "TestProjectTarget"),
                Path.Combine(tempDirectory, "TestProjectTarget.zip")
            );

            file1 = await _helperClient.DataFilesClient.CreateAsync(
                new FileParameter(data: File.OpenRead(Path.Combine(tempDirectory, "TestProject.zip"))),
                FileFormat.Paratext
            );
            file2 = await _helperClient.DataFilesClient.CreateAsync(
                new FileParameter(data: File.OpenRead(Path.Combine(tempDirectory, "TestProjectTarget.zip"))),
                FileFormat.Paratext
            );
        }
        finally
        {
            File.Delete(Path.Combine(tempDirectory, "TestProject.zip"));
            File.Delete(Path.Combine(tempDirectory, "TestProjectTarget.zip"));
        }

        string engineId = await _helperClient.CreateNewEngineAsync("Nmt", "en", "sbp", "NMT4");

        TranslationCorpus corpus = await _helperClient.TranslationEnginesClient.AddCorpusAsync(
            engineId,
            new TranslationCorpusConfig
            {
                SourceLanguage = "en",
                TargetLanguage = "sbp",
                SourceFiles = [new() { FileId = file1.Id }],
                TargetFiles = [new() { FileId = file2.Id }]
            }
        );
        _helperClient.TranslationBuildConfig.Pretranslate!.Add(
            new PretranslateCorpusConfig { CorpusId = corpus.Id, ScriptureRange = "JHN" }
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
            corpus.Id
        );
        Assert.That(lTrans, Is.Not.Empty);
        string usfm = await _helperClient.TranslationEnginesClient.GetPretranslatedUsfmAsync(
            engineId,
            corpus.Id,
            "JHN"
        );
        Assert.That(usfm, Does.Contain("\\v 1"));
    }

    [Test]
    public async Task GetWordAlignment()
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
}

#pragma warning restore CS0612 // Type or member is obsolete
