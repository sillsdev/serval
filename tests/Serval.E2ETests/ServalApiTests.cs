namespace Serval.E2ETests;

[TestFixture]
[Category("E2E")]
public class ServalApiTests
{
    private ServalClientHelper _helperClient;

    [SetUp]
    public void SetUp()
    {
        _helperClient = new ServalClientHelper("https://serval-api.org/", ignoreSSLErrors: true);
    }

    [Test]
    public async Task GetEchoSuggestion()
    {
        string engineId = await _helperClient.CreateNewEngine("Echo", "es", "es", "Echo1");
        string[] books = ["1JN.txt", "2JN.txt", "3JN.txt"];
        await _helperClient.AddTextCorpusToEngine(engineId, books, "es", "es", false);
        await _helperClient.BuildEngine(engineId);
        TranslationResult tResult = await _helperClient.TranslationEnginesClient.TranslateAsync(engineId, "Espíritu");
        Assert.That(tResult.Translation, Is.EqualTo("Espíritu"));
    }

    [Test]
    public async Task GetEchoPretranslate()
    {
        string engineId = await _helperClient.CreateNewEngine("Echo", "es", "es", "Echo2");
        string[] books = ["1JN.txt", "2JN.txt"];
        await _helperClient.AddTextCorpusToEngine(engineId, books, "es", "es", false);
        books = ["3JN.txt"];
        var corpusId = await _helperClient.AddTextCorpusToEngine(engineId, books, "es", "es", true);
        await _helperClient.BuildEngine(engineId);
        var pretranslations = await _helperClient.TranslationEnginesClient.GetAllPretranslationsAsync(
            engineId,
            corpusId
        );
        Assert.That(pretranslations.Count, Is.GreaterThan(1));
    }

    [Test]
    public async Task GetSmtTranslation()
    {
        string engineId = await _helperClient.CreateNewEngine("SmtTransfer", "es", "en", "SMT1");
        string[] books = ["1JN.txt", "2JN.txt", "3JN.txt"];
        await _helperClient.AddTextCorpusToEngine(engineId, books, "es", "en", false);
        await _helperClient.BuildEngine(engineId);
        TranslationResult tResult = await _helperClient.TranslationEnginesClient.TranslateAsync(engineId, "Espíritu");
        Assert.That(tResult.Translation, Is.EqualTo("spirit"));
    }

    [Test]
    public async Task GetSmtAddSegment()
    {
        string engineId = await _helperClient.CreateNewEngine("smt-transfer", "es", "en", "SMT3");
        string[] books = ["1JN.txt", "2JN.txt", "3JN.txt"];
        await _helperClient.AddTextCorpusToEngine(engineId, books, "es", "en", false);
        await _helperClient.BuildEngine(engineId);
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
        string engineId = await _helperClient.CreateNewEngine("SmtTransfer", "es", "en", "SMT4");
        await _helperClient.AddTextCorpusToEngine(engineId, ["3JN.txt"], "es", "en", false);
        await _helperClient.BuildEngine(engineId);
        TranslationResult tResult = await _helperClient.TranslationEnginesClient.TranslateAsync(
            engineId,
            "verdad mundo"
        );
        Assert.That(tResult.Translation, Is.EqualTo("truth mundo"));
        await _helperClient.AddTextCorpusToEngine(engineId, ["1JN.txt", "2JN.txt"], "es", "en", false);
        await _helperClient.BuildEngine(engineId);
        TranslationResult tResult2 = await _helperClient.TranslationEnginesClient.TranslateAsync(
            engineId,
            "verdad mundo"
        );
        Assert.That(tResult2.Translation, Is.EqualTo("truth world"));
    }

    [Test]
    public async Task NmtBatch()
    {
        string engineId = await _helperClient.CreateNewEngine("Nmt", "es", "en", "NMT1");
        string[] books = ["MAT.txt", "1JN.txt", "2JN.txt"];
        var cId1 = await _helperClient.AddTextCorpusToEngine(engineId, books, "es", "en", false);
        _helperClient.TranslationBuildConfig.TrainOn = new List<TrainingCorpusConfig>
        {
            new() { CorpusId = cId1, TextIds = ["1JN.txt"] }
        };
        var cId2 = await _helperClient.AddTextCorpusToEngine(engineId, ["3JN.txt"], "es", "en", true);
        await _helperClient.BuildEngine(engineId);
        await Task.Delay(1000);
        IList<Pretranslation> lTrans = await _helperClient.TranslationEnginesClient.GetAllPretranslationsAsync(
            engineId,
            cId2
        );
        Assert.That(lTrans.Count, Is.EqualTo(14));
    }

    [Test]
    public async Task NmtQueueMultiple()
    {
        const int NUM_ENGINES = 9;
        const int NUM_WORKERS = 1;
        string[] engineIds = new string[NUM_ENGINES];
        for (int i = 0; i < NUM_ENGINES; i++)
        {
            _helperClient.TranslationBuildConfig = new()
            {
                Pretranslate = new List<PretranslateCorpusConfig>(),
                Options = "{\"max_steps\":10}"
            };
            engineIds[i] = await _helperClient.CreateNewEngine("Nmt", "es", "en", $"NMT1_{i}");
            string engineId = engineIds[i];
            string[] books = ["MAT.txt", "1JN.txt", "2JN.txt"];
            await _helperClient.AddTextCorpusToEngine(engineId, books, "es", "en", false);
            await _helperClient.AddTextCorpusToEngine(engineId, ["3JN.txt"], "es", "en", true);
            await _helperClient.StartBuildAsync(engineId);
            //Ensure that tasks are enqueued roughly in order
            await Task.Delay(1_000);
        }
        //Wait for at least some tasks to be queued
        await Task.Delay(40_000);
        string builds = "";
        for (int i = 0; i < NUM_ENGINES; i++)
        {
            TranslationBuild build = await _helperClient.TranslationEnginesClient.GetCurrentBuildAsync(engineIds[i]);
            builds += $"{JsonSerializer.Serialize(build)}\n";
        }

        builds += "Depth = " + (await _helperClient.TranslationEngineTypesClient.GetQueueAsync("Nmt")).Size.ToString();

        //Status message of last started build says that there is at least one job ahead of it in the queue
        // (this variable due to how many jobs may already exist in the production queue from other Serval instances)
        TranslationBuild newestEngineCurrentBuild = await _helperClient.TranslationEnginesClient.GetCurrentBuildAsync(
            engineIds[NUM_ENGINES - 1]
        );
        int? queueDepth = newestEngineCurrentBuild.QueueDepth;
        Queue queue = await _helperClient.TranslationEngineTypesClient.GetQueueAsync("Nmt");
        for (int i = 0; i < NUM_ENGINES; i++)
        {
            try
            {
                await _helperClient.TranslationEnginesClient.CancelBuildAsync(engineIds[i]);
            }
            catch { }
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
    }

    [Test]
    public async Task NmtLargeBatch()
    {
        string engineId = await _helperClient.CreateNewEngine("Nmt", "es", "en", "NMT3");
        string[] books = ["bible_LARGEFILE.txt"];
        await _helperClient.AddTextCorpusToEngine(engineId, books, "es", "en", false);
        var cId = await _helperClient.AddTextCorpusToEngine(engineId, ["3JN.txt"], "es", "en", true);
        await _helperClient.BuildEngine(engineId);
        await Task.Delay(1000);
        IList<Pretranslation> lTrans = await _helperClient.TranslationEnginesClient.GetAllPretranslationsAsync(
            engineId,
            cId
        );
        TestContext.WriteLine(lTrans[0].Translation);
    }

    [Test]
    public async Task GetNmtCancelAndRestartBuild()
    {
        string engineId = await _helperClient.CreateNewEngine("Nmt", "es", "en", "NMT2");
        string[] books = ["1JN.txt", "2JN.txt", "3JN.txt"];
        await _helperClient.AddTextCorpusToEngine(engineId, books, "es", "en", false);
        await StartAndCancelTwice(engineId);
    }

    [Test]
    public async Task CircuitousRouteGetWordGraphAsync()
    {
        //Create smt engine
        string smtEngineId = await _helperClient.CreateNewEngine("SmtTransfer", "es", "en", "SMT5");

        //Try to get word graph - should fail: unbuilt
        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            await _helperClient.TranslationEnginesClient.GetWordGraphAsync(smtEngineId, "verdad");
        });
        Assert.That(ex, Is.Not.Null);
        Assert.That(ex.StatusCode, Is.EqualTo(409));

        //Add corpus
        var cId = await _helperClient.AddTextCorpusToEngine(smtEngineId, ["2JN.txt", "3JN.txt"], "es", "en", false);

        //Build the new engine
        await _helperClient.BuildEngine(smtEngineId);

        //Remove added corpus (shouldn't affect translation)
        await _helperClient.TranslationEnginesClient.DeleteCorpusAsync(smtEngineId, cId);

        //Add corpus
        await _helperClient.AddTextCorpusToEngine(smtEngineId, ["1JN.txt", "2JN.txt", "3JN.txt"], "es", "en", false);

        //Build the new engine
        await _helperClient.BuildEngine(smtEngineId);

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
        string engineId = await _helperClient.CreateNewEngine("smt-transfer", "en", "fa", "SMT6");

        //Retrieve engine
        TranslationEngine engine = await _helperClient.TranslationEnginesClient.GetAsync(engineId);
        Assert.That(engine.Type, Is.EqualTo("SmtTransfer"));

        //Add corpus
        string cId = await _helperClient.AddTextCorpusToEngine(
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
        await _helperClient.BuildEngine(engineId);

        //Get top `N` translations
        ICollection<TranslationResult> results = await _helperClient.TranslationEnginesClient.TranslateNAsync(
            engineId,
            N,
            "love"
        );
        Assert.That(
            results.MaxBy(t => t.Confidences.Average())?.Translation,
            Is.EqualTo("amour"),
            message: "Expected best translation to be 'amour' but results were this:\n"
                + JsonSerializer.Serialize(results)
        );
    }

    [Test]
    public async Task GetSmtCancelAndRestartBuild()
    {
        string engineId = await _helperClient.CreateNewEngine("smt-transfer", "es", "en", "SMT7");
        string[] books = ["1JN.txt", "2JN.txt", "3JN.txt"];
        await _helperClient.AddTextCorpusToEngine(engineId, books, "es", "en", false);

        await StartAndCancelTwice(engineId);

        // do a job normally and make sure it works.
        await _helperClient.BuildEngine(engineId);
        TranslationResult tResult = await _helperClient.TranslationEnginesClient.TranslateAsync(engineId, "Espíritu");
        Assert.That(tResult.Translation, Is.EqualTo("spirit"));
    }

    async Task StartAndCancelTwice(string engineId)
    {
        // start and first job
        var build = await _helperClient.StartBuildAsync(engineId);
        await Task.Delay(1000);
        build = await _helperClient.TranslationEnginesClient.GetBuildAsync(engineId, build.Id);
        Assert.That(build.State == JobState.Active || build.State == JobState.Pending);

        // and then cancel it
        await _helperClient.CancelBuild(engineId, build.Id);
        build = await _helperClient.TranslationEnginesClient.GetBuildAsync(engineId, build.Id);
        Assert.That(build.State == JobState.Canceled);

        // do a second job normally and make sure it works.
        build = await _helperClient.StartBuildAsync(engineId);
        await Task.Delay(1000);
        build = await _helperClient.TranslationEnginesClient.GetBuildAsync(engineId, build.Id);
        Assert.That(build.State == JobState.Active || build.State == JobState.Pending);

        // and cancel again - let's not wait forever
        await _helperClient.CancelBuild(engineId, build.Id);
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

        string engineId = await _helperClient.CreateNewEngine("Nmt", "en", "sbp", "NMT4");

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
            new PretranslateCorpusConfig { CorpusId = corpus.Id, TextIds = ["JHN", "REV"] }
        );
        _helperClient.TranslationBuildConfig.Options = "{\"max_steps\":10, \"use_key_terms\":true}";

        await _helperClient.BuildEngine(engineId);
        Assert.That(
            (await _helperClient.TranslationEnginesClient.GetAllBuildsAsync(engineId)).First().State,
            Is.EqualTo(JobState.Completed)
        );
        IList<Pretranslation> lTrans = await _helperClient.TranslationEnginesClient.GetAllPretranslationsAsync(
            engineId,
            corpus.Id
        );
        Assert.That(lTrans, Is.Not.Empty);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _helperClient.DisposeAsync();
    }
}
