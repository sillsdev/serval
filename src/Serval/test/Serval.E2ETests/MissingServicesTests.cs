namespace Serval.E2ETests;

[TestFixture]
[Category("E2EMissingServices")]
[Explicit("These are only run from the missing services E2E tests")]
public class MissingServicesTests
{
    private ServalClientHelper _helperClient;

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        _helperClient = new ServalClientHelper("https://serval-api.org/", ignoreSslErrors: true);
        try
        {
            await _helperClient.InitAsync();
        }
        catch (ServalApiException)
        {
            // An error will be thrown when the services are missing
        }
    }

    [SetUp]
    public void Setup()
    {
        _helperClient.Setup();
    }

    [Test]
    [Category("MongoWorking")]
    public void UseMongoAndAuth0Async()
    {
        Assert.DoesNotThrowAsync(async () =>
        {
            await _helperClient.DataFilesClient.GetAllAsync();
        });
    }

    [Test]
    [Category("EngineServerWorking")]
    public void UseEngineServerAsync()
    {
        Assert.DoesNotThrowAsync(async () =>
        {
            string engineId = await _helperClient.CreateNewEngineAsync("SmtTransfer", "es", "en", "SMT3");
            string[] books = ["1JN.txt", "2JN.txt", "3JN.txt"];
            ParallelCorpusConfig corpus = await _helperClient.MakeParallelTextCorpus(books, "es", "en", false);
            await _helperClient.AddParallelTextCorpusToEngineAsync(engineId, corpus, false);
            await _helperClient.BuildEngineAsync(engineId);
        });
    }

    [Test]
    [Category("ClearMLNotWorking")]
    public void UseMissingClearMLAsync()
    {
        Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            string engineId = await _helperClient.CreateNewEngineAsync("Nmt", "es", "en", "NMT1");
            string[] books = ["MAT.txt", "1JN.txt", "2JN.txt"];
            ParallelCorpusConfig trainCorpus = await _helperClient.MakeParallelTextCorpus(books, "es", "en", false);
            await _helperClient.AddParallelTextCorpusToEngineAsync(engineId, trainCorpus, false);
            books = ["3JN.txt"];
            ParallelCorpusConfig pretranslateCorpus = await _helperClient.MakeParallelTextCorpus(
                books,
                "es",
                "en",
                true
            );
            string corpusId = await _helperClient.AddParallelTextCorpusToEngineAsync(
                engineId,
                pretranslateCorpus,
                false
            );
            await _helperClient.BuildEngineAsync(engineId);
            _ = await _helperClient.TranslationEnginesClient.GetAllPretranslationsAsync(engineId, corpusId);
        });
    }

    [Test]
    [Category("AWSNotWorking")]
    public async Task UseMissingAWSAsync()
    {
        string engineId = await _helperClient.CreateNewEngineAsync("Nmt", "es", "en", "NMT1");
        string[] books = ["MAT.txt", "1JN.txt", "2JN.txt"];
        ParallelCorpusConfig trainCorpus = await _helperClient.MakeParallelTextCorpus(books, "es", "es", true);
        await _helperClient.AddParallelTextCorpusToEngineAsync(engineId, trainCorpus, false);
        books = ["3JN.txt"];
        ParallelCorpusConfig pretranslateCorpus = await _helperClient.MakeParallelTextCorpus(books, "es", "es", true);
        await _helperClient.AddParallelTextCorpusToEngineAsync(engineId, pretranslateCorpus, true);

        await _helperClient.BuildEngineAsync(engineId);
        IList<TranslationBuild> builds = await _helperClient.TranslationEnginesClient.GetAllBuildsAsync(engineId);
        Assert.That(builds.First().State, Is.EqualTo(JobState.Faulted));
    }

    [Test]
    [Category("MongoNotWorking")]
    public void UseMissingMongoAsync()
    {
        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            await _helperClient.DataFilesClient.GetAllAsync();
        });
        Assert.That(ex, Is.Not.Null);
        Assert.That(ex.StatusCode, Is.EqualTo(503));
    }

    [Test]
    [Category("EngineServerNotWorking")]
    public void UseMissingEngineServerAsync()
    {
        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            string engineId = await _helperClient.CreateNewEngineAsync("SmtTransfer", "es", "en", "SMT3");
            string[] books = ["1JN.txt", "2JN.txt", "3JN.txt"];
            ParallelCorpusConfig corpus = await _helperClient.MakeParallelTextCorpus(books, "es", "en", false);
            await _helperClient.AddParallelTextCorpusToEngineAsync(engineId, corpus, false);
            await _helperClient.BuildEngineAsync(engineId);
        });
        Assert.That(ex, Is.Not.Null);
        Assert.That(ex.StatusCode, Is.EqualTo(503));
    }

    [TearDown]
    public async Task TearDown()
    {
        try
        {
            await _helperClient.TearDown();
        }
        catch (ServalApiException)
        {
            // An error will be thrown when the services are missing
        }
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _helperClient.DisposeAsync();
    }
}
