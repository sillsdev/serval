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
    public async Task UseMongoAndAuth0Async()
    {
        await Assert.DoesNotThrowAsync(() => _helperClient.DataFilesClient.GetAllAsync());
    }

    [Test]
    [Category("ClearMLNotWorking")]
    public async Task UseMissingClearMLAsync()
    {
        await Assert.ThrowsAsync<ServalApiException>(async () =>
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
    public async Task UseMissingMongoAsync()
    {
        ServalApiException? ex = await Assert.ThrowsAsync<ServalApiException>(() =>
            _helperClient.DataFilesClient.GetAllAsync()
        );
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
