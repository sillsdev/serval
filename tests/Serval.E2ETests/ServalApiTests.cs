namespace Serval.E2ETests;

[TestFixture]
[Category("E2E")]
public class ServalApiTests
{
    private ServalClientHelper? _helperClient;

    [SetUp]
    public void SetUp()
    {
        _helperClient = new ServalClientHelper("https://serval-api.org/", ignoreSSLErrors: true);
    }

    [Test]
    public async Task GetEchoSuggestion()
    {
        await _helperClient!.ClearEngines();
        string engineId = await _helperClient.CreateNewEngine("Echo", "es", "es", "Echo1");
        var books = new string[] { "1JN.txt", "2JN.txt", "3JN.txt" };
        await _helperClient.AddTextCorpusToEngine(engineId, books, "es", "es", false);
        await _helperClient.BuildEngine(engineId);
        TranslationResult tResult = await _helperClient.translationEnginesClient.TranslateAsync(engineId, "Espíritu");
        Assert.AreEqual(tResult.Translation, "Espíritu");
    }

    [Test]
    public async Task GetEchoPretranslate()
    {
        await _helperClient!.ClearEngines();
        string engineId = await _helperClient.CreateNewEngine("Echo", "es", "es", "Echo2");
        var books = new string[] { "1JN.txt", "2JN.txt" };
        await _helperClient.AddTextCorpusToEngine(engineId, books, "es", "es", false);
        books = ["3JN.txt"];
        var corpusId = await _helperClient.AddTextCorpusToEngine(engineId, books, "es", "es", true);
        await _helperClient.BuildEngine(engineId);
        var corpora = _helperClient.translationEnginesClient.GetAllCorporaAsync(engineId);
        var pretranslations = await _helperClient.translationEnginesClient.GetAllPretranslationsAsync(
            engineId,
            corpusId
        );
        Assert.IsTrue(pretranslations.Count > 1);
    }

    [Test]
    public async Task GetSmtTranslation()
    {
        await _helperClient!.ClearEngines();
        string engineId = await _helperClient.CreateNewEngine("SmtTransfer", "es", "en", "SMT1");
        var books = new string[] { "1JN.txt", "2JN.txt", "3JN.txt" };
        await _helperClient.AddTextCorpusToEngine(engineId, books, "es", "en", false);
        await _helperClient.BuildEngine(engineId);
        TranslationResult tResult = await _helperClient.translationEnginesClient.TranslateAsync(engineId, "Espíritu");
        Assert.AreEqual(tResult.Translation, "spirit");
    }

    [Test]
    public async Task GetSmtAddSegment()
    {
        await _helperClient!.ClearEngines();
        string engineId = await _helperClient.CreateNewEngine("smt-transfer", "es", "en", "SMT3");
        var books = new string[] { "1JN.txt", "2JN.txt", "3JN.txt" };
        await _helperClient.AddTextCorpusToEngine(engineId, books, "es", "en", false);
        await _helperClient.BuildEngine(engineId);
        TranslationResult tResult = await _helperClient.translationEnginesClient.TranslateAsync(
            engineId,
            "ungidos espíritu"
        );
        Assert.AreEqual(tResult.Translation, "ungidos spirit");
        await _helperClient.translationEnginesClient.TrainSegmentAsync(
            engineId,
            new SegmentPair
            {
                SourceSegment = "ungidos espíritu",
                TargetSegment = "unction spirit",
                SentenceStart = true
            }
        );
        TranslationResult tResult2 = await _helperClient.translationEnginesClient.TranslateAsync(
            engineId,
            "ungidos espíritu"
        );
        Assert.AreEqual(tResult2.Translation, "unction spirit");
    }

    [Test]
    public async Task GetSmtMoreCorpus()
    {
        await _helperClient!.ClearEngines();
        string engineId = await _helperClient.CreateNewEngine("SmtTransfer", "es", "en", "SMT4");
        await _helperClient.AddTextCorpusToEngine(engineId, ["3JN.txt"], "es", "en", false);
        await _helperClient.BuildEngine(engineId);
        TranslationResult tResult = await _helperClient.translationEnginesClient.TranslateAsync(
            engineId,
            "verdad mundo"
        );
        Assert.AreEqual(tResult.Translation, "truth mundo");
        await _helperClient.AddTextCorpusToEngine(engineId, ["1JN.txt", "2JN.txt"], "es", "en", false);
        await _helperClient.BuildEngine(engineId);
        TranslationResult tResult2 = await _helperClient.translationEnginesClient.TranslateAsync(
            engineId,
            "verdad mundo"
        );
        Assert.AreEqual(tResult2.Translation, "truth world");
    }

    [Test]
    public async Task NmtBatch()
    {
        await _helperClient!.ClearEngines();
        string engineId = await _helperClient.CreateNewEngine("Nmt", "es", "en", "NMT1");
        var books = new string[] { "MAT.txt", "1JN.txt", "2JN.txt" };
        var cId1 = await _helperClient.AddTextCorpusToEngine(engineId, books, "es", "en", false);
        _helperClient.TranslationBuildConfig.TrainOn = new List<TrainingCorpusConfig>
        {
            new TrainingCorpusConfig { CorpusId = cId1, TextIds = ["1JN.txt"] }
        };
        var cId2 = await _helperClient.AddTextCorpusToEngine(engineId, ["3JN.txt"], "es", "en", true);
        await _helperClient.BuildEngine(engineId);
        await Task.Delay(1000);
        IList<Pretranslation> lTrans = await _helperClient.translationEnginesClient.GetAllPretranslationsAsync(
            engineId,
            cId2
        );
        Assert.IsTrue(lTrans.Count == 14);
    }

    [Test]
    public async Task NmtQueueMultiple()
    {
        await _helperClient!.ClearEngines();
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
            var books = new string[] { "MAT.txt", "1JN.txt", "2JN.txt" };
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
            TranslationBuild build = await _helperClient.translationEnginesClient.GetCurrentBuildAsync(engineIds[i]);
            builds += $"{JsonSerializer.Serialize(build)}\n";
        }

        builds += "Depth = " + (await _helperClient.translationClient.GetQueueAsync("Nmt")).Size.ToString();

        //Status message of last started build says that there is at least one job ahead of it in the queue
        // (this variable due to how many jobs may already exist in the production queue from other Serval instances)
        TranslationBuild newestEngineCurrentBuild = await _helperClient.translationEnginesClient.GetCurrentBuildAsync(
            engineIds[NUM_ENGINES - 1]
        );
        int? queueDepth = newestEngineCurrentBuild.QueueDepth;
        Queue queue = await _helperClient.translationClient.GetQueueAsync("Nmt");
        for (int i = 0; i < NUM_ENGINES; i++)
        {
            try
            {
                await _helperClient.translationEnginesClient.CancelBuildAsync(engineIds[i]);
            }
            catch { }
        }
        Assert.NotNull(queueDepth, JsonSerializer.Serialize(newestEngineCurrentBuild) + "|||" + builds);
        Assert.Multiple(() =>
        {
            Assert.That(queueDepth, Is.GreaterThan(0), message: builds);
            Assert.That(queue.Size, Is.GreaterThanOrEqualTo(NUM_ENGINES - NUM_WORKERS));
        });
    }

    [Test]
    public async Task NmtLargeBatch()
    {
        await _helperClient!.ClearEngines();
        string engineId = await _helperClient.CreateNewEngine("Nmt", "es", "en", "NMT3");
        var books = new string[] { "bible_LARGEFILE.txt" };
        await _helperClient.AddTextCorpusToEngine(engineId, books, "es", "en", false);
        var cId = await _helperClient.AddTextCorpusToEngine(engineId, ["3JN.txt"], "es", "en", true);
        await _helperClient.BuildEngine(engineId);
        await Task.Delay(1000);
        IList<Pretranslation> lTrans = await _helperClient.translationEnginesClient.GetAllPretranslationsAsync(
            engineId,
            cId
        );
        TestContext.WriteLine(lTrans[0].Translation);
    }

    [Test]
    public async Task GetNmtCancelAndRestartBuild()
    {
        await _helperClient!.ClearEngines();
        string engineId = await _helperClient.CreateNewEngine("Nmt", "es", "en", "NMT2");
        var books = new string[] { "1JN.txt", "2JN.txt", "3JN.txt" };
        await _helperClient.AddTextCorpusToEngine(engineId, books, "es", "en", false);
        await StartAndCancelTwice(engineId);
    }

    [Test]
    public async Task CircuitousRouteGetWordGraphAsync()
    {
        await _helperClient!.ClearEngines();

        //Create smt engine
        string smtEngineId = await _helperClient.CreateNewEngine("SmtTransfer", "es", "en", "SMT5");

        //Try to get word graph - should fail: unbuilt
        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            await _helperClient.translationEnginesClient.GetWordGraphAsync(smtEngineId, "verdad");
        });
        Assert.NotNull(ex);
        Assert.That(ex!.StatusCode, Is.EqualTo(409));

        //Add corpus
        var cId = await _helperClient.AddTextCorpusToEngine(smtEngineId, ["2JN.txt", "3JN.txt"], "es", "en", false);

        //Build the new engine
        await _helperClient.BuildEngine(smtEngineId);

        //Remove added corpus (shouldn't affect translation)
        await _helperClient.translationEnginesClient.DeleteCorpusAsync(smtEngineId, cId);

        //Add corpus
        await _helperClient.AddTextCorpusToEngine(smtEngineId, ["1JN.txt", "2JN.txt", "3JN.txt"], "es", "en", false);

        //Build the new engine
        await _helperClient.BuildEngine(smtEngineId);

        WordGraph result = await _helperClient.translationEnginesClient.GetWordGraphAsync(smtEngineId, "verdad");
        Assert.That(result.SourceTokens, Has.Count.EqualTo(1));
        Assert.That(
            result
                .Arcs.Where(arc => arc != null && arc.Confidences != null)!
                .MaxBy(arc => arc.Confidences.Average())!
                .TargetTokens.All(tk => tk == "truth"),
            "Best translation should have been 'truth'but returned word graph: \n{0}",
            JsonSerializer.Serialize(result)
        );
    }

    [Test]
    public async Task CircuitousRouteTranslateTopNAsync()
    {
        const int N = 3;

        //Create engine
        string engineId = await _helperClient!.CreateNewEngine("smt-transfer", "en", "fa", "SMT6");

        //Retrieve engine
        TranslationEngine? engine = await _helperClient.translationEnginesClient.GetAsync(engineId);
        Assert.NotNull(engine);
        Assert.That(engine.Type, Is.EqualTo("smt-transfer"));

        //Add corpus
        string cId = await _helperClient.AddTextCorpusToEngine(
            engineId,
            ["1JN.txt", "2JN.txt", "3JN.txt"],
            "en",
            "fa",
            false
        );

        //Retrieve corpus
        TranslationCorpus? corpus = await _helperClient.translationEnginesClient.GetCorpusAsync(engineId, cId);
        Assert.NotNull(corpus);
        Assert.That(corpus.SourceLanguage, Is.EqualTo("en"));
        Assert.That(corpus.TargetFiles, Has.Count.EqualTo(3));

        //Build engine
        await _helperClient.BuildEngine(engineId);

        //Get top `N` translations
        ICollection<TranslationResult> results = await _helperClient.translationEnginesClient.TranslateNAsync(
            engineId,
            N,
            "love"
        );
        Assert.NotNull(results);
        Assert.That(
            results.MaxBy(t => t.Confidences.Average())!.Translation,
            Is.EqualTo("amour"),
            "Expected best translation to be 'amour' but results were this:\n" + JsonSerializer.Serialize(results)
        );
    }

    [Test]
    public async Task GetSmtCancelAndRestartBuild()
    {
        await _helperClient!.ClearEngines();
        string engineId = await _helperClient.CreateNewEngine("smt-transfer", "es", "en", "SMT7");
        var books = new string[] { "1JN.txt", "2JN.txt", "3JN.txt" };
        await _helperClient.AddTextCorpusToEngine(engineId, books, "es", "en", false);

        await StartAndCancelTwice(engineId);

        // do a job normally and make sure it works.
        await _helperClient.BuildEngine(engineId);
        TranslationResult tResult = await _helperClient.translationEnginesClient.TranslateAsync(engineId, "Espíritu");
        Assert.AreEqual(tResult.Translation, "spirit");
    }

    async Task StartAndCancelTwice(string engineId)
    {
        // start and first job
        var build = await _helperClient!.StartBuildAsync(engineId);
        await Task.Delay(1000);
        build = await _helperClient.translationEnginesClient.GetBuildAsync(engineId, build.Id);
        Assert.That(build.State == JobState.Active || build.State == JobState.Pending);

        // and then cancel it
        await _helperClient.CancelBuild(engineId, build.Id);
        build = await _helperClient.translationEnginesClient.GetBuildAsync(engineId, build.Id);
        Assert.That(build.State == JobState.Canceled);

        // do a second job normally and make sure it works.
        build = await _helperClient.StartBuildAsync(engineId);
        await Task.Delay(1000);
        build = await _helperClient.translationEnginesClient.GetBuildAsync(engineId, build.Id);
        Assert.That(build.State == JobState.Active || build.State == JobState.Pending);

        // and cancel again - let's not wait forever
        await _helperClient.CancelBuild(engineId, build.Id);
        build = await _helperClient.translationEnginesClient.GetBuildAsync(engineId, build.Id);
        Assert.That(build.State == JobState.Canceled);
    }

    [Test]
    public async Task ParatextProjectNmtJobAsync()
    {
        await _helperClient!.ClearEngines();
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

            file1 = await _helperClient.dataFilesClient.CreateAsync(
                new FileParameter(data: File.OpenRead(Path.Combine(tempDirectory, "TestProject.zip"))),
                FileFormat.Paratext
            );
            file2 = await _helperClient.dataFilesClient.CreateAsync(
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

        TranslationCorpus corpus = await _helperClient.translationEnginesClient.AddCorpusAsync(
            engineId,
            new TranslationCorpusConfig
            {
                SourceLanguage = "en",
                TargetLanguage = "sbp",
                SourceFiles = [new TranslationCorpusFileConfig { FileId = file1.Id }],
                TargetFiles = [new TranslationCorpusFileConfig { FileId = file2.Id }]
            }
        );
        _helperClient.TranslationBuildConfig.Pretranslate!.Add(
            new PretranslateCorpusConfig { CorpusId = corpus.Id, Chapters = "JHN" }
        );
        _helperClient.TranslationBuildConfig.Options = "{\"max_steps\":10, \"use_key_terms\":true}";

        await _helperClient.BuildEngine(engineId);
        Assert.That(
            (await _helperClient.translationEnginesClient.GetAllBuildsAsync(engineId)).First().State
                == JobState.Completed,
            JsonSerializer.Serialize((await _helperClient.translationEnginesClient.GetAllBuildsAsync(engineId)).First())
        );
        IList<Pretranslation> lTrans = await _helperClient.translationEnginesClient.GetAllPretranslationsAsync(
            engineId,
            corpus.Id
        );
        Assert.That(lTrans, Is.Not.Empty);
    }
}
