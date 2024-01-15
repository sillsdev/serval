namespace Serval.E2ETests;

[TestFixture]
[Category("E2EMissingServices")]
public class MissingServicesTests
{
    private ServalClientHelper _helperClient;

    [SetUp]
    public async Task Setup()
    {
        _helperClient = new ServalClientHelper("https://serval-api.org/", ignoreSSLErrors: true);
        await _helperClient.InitAsync();
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
            await _helperClient.AddTextCorpusToEngineAsync(engineId, books, "es", "en", false);
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
            await _helperClient.AddTextCorpusToEngineAsync(engineId, books, "es", "en", false);
            var cId = await _helperClient.AddTextCorpusToEngineAsync(engineId, ["3JN.txt"], "es", "en", true);
            await _helperClient.BuildEngineAsync(engineId);
            IList<Pretranslation> lTrans = await _helperClient.TranslationEnginesClient.GetAllPretranslationsAsync(
                engineId,
                cId
            );
        });
    }

    [Test]
    [Category("AWSNotWorking")]
    public async Task UseMissingAWSAsync()
    {
        string engineId = await _helperClient.CreateNewEngineAsync("Nmt", "es", "en", "NMT1");
        string[] books = ["MAT.txt", "1JN.txt", "2JN.txt"];
        await _helperClient.AddTextCorpusToEngineAsync(engineId, books, "es", "en", false);
        await _helperClient.AddTextCorpusToEngineAsync(engineId, ["3JN.txt"], "es", "en", true);
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
            await _helperClient.AddTextCorpusToEngineAsync(engineId, books, "es", "en", false);
            await _helperClient.BuildEngineAsync(engineId);
        });
        Assert.That(ex, Is.Not.Null);
        Assert.That(ex.StatusCode, Is.EqualTo(503));
    }

    [TearDown]
    public async Task TearDown()
    {
        await _helperClient.DisposeAsync();
    }
}
