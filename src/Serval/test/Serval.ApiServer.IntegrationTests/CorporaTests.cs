namespace Serval.ApiServer;

[TestFixture]
[Category("Integration")]
public class CorporaTests
{
    TestEnvironment _env;

    const string FILE_ID1 = "000000000000000000000001";
    const string FILE_NAME1 = "sample1.txt";

    const string FILE_ID2 = "000000000000000000000002";
    const string FILE_NAME2 = "sample2.txt";

    const string FILE_ID3 = "000000000000000000000003";
    const string FILE_NAME3 = "sample3.txt";
    const string DOES_NOT_EXIST_ID = "000000000000000000000004";

    // add corpora ID's and names
    const string CORPUS_ID1 = "100000000000000000000001";
    const string CORPUS_NAME1 = "sample1";

    const string CORPUS_ID2 = "100000000000000000000002";
    const string CORPUS_NAME2 = "sample2";

    const string CORPUS_ID3 = "100000000000000000000003";
    const string CORPUS_NAME3 = "sample3";

    [SetUp]
    public async Task SetUp()
    {
        _env = new TestEnvironment();
        // Insert some data files for testing
        var file1 = new DataFiles.Models.DataFile
        {
            Id = FILE_ID1,
            Owner = "client1",
            Name = FILE_NAME1,
            Filename = FILE_NAME1,
            Format = Shared.Contracts.FileFormat.Text
        };
        var file2 = new DataFiles.Models.DataFile
        {
            Id = FILE_ID2,
            Owner = "client1",
            Name = FILE_NAME2,
            Filename = FILE_NAME2,
            Format = Shared.Contracts.FileFormat.Text
        };
        var file3 = new DataFiles.Models.DataFile
        {
            Id = FILE_ID3,
            Owner = "client2",
            Name = FILE_NAME3,
            Filename = FILE_NAME3,
            Format = Shared.Contracts.FileFormat.Text
        };
        await _env.DataFiles.InsertAllAsync([file1, file2, file3]);
        // Insert some corpora for testing
        var corpus1 = new DataFiles.Models.Corpus
        {
            Id = CORPUS_ID1,
            Owner = "client1",
            Name = CORPUS_NAME1,
            Language = "en",
            Files = [new DataFiles.Models.CorpusFile { FileRef = FILE_ID1 }]
        };
        var corpus2 = new DataFiles.Models.Corpus
        {
            Id = CORPUS_ID2,
            Owner = "client1",
            Name = CORPUS_NAME2,
            Language = "fr",
            Files = [new DataFiles.Models.CorpusFile { FileRef = FILE_ID2 }]
        };
        var corpus3 = new DataFiles.Models.Corpus
        {
            Id = CORPUS_ID3,
            Owner = "client2",
            Name = CORPUS_NAME3,
            Language = "de",
            Files = [new DataFiles.Models.CorpusFile { FileRef = FILE_ID3 }]
        };
        await _env.Corpora.InsertAllAsync([corpus1, corpus2, corpus3]);
    }

    [Test]
    // [TestCase(new[] { Scopes.ReadFiles }, 401)] // TODO Potentially test 401 if needed
    [TestCase(new[] { Scopes.ReadFiles }, 200)]
    [TestCase(new[] { Scopes.CreateTranslationEngines }, 403)]
    public async Task GetAllAsync(IEnumerable<string> scope, int expectedStatusCode)
    {
        var corporaClient = _env.CreateCorporaClient(scope);
        switch (expectedStatusCode)
        {
            case 200:
                ICollection<Corpus> results = await corporaClient.GetAllAsync();
                Assert.That(results, Is.Not.Null);
                Assert.That(results.Count, Is.EqualTo(2));
                Assert.That(results.All(c => c.Revision == 1), Is.True);
                break;
            case 401:
                // goto case 403; // If you choose to handle 401 as 403
                break;
            case 403:
            default:
                ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await corporaClient.GetAllAsync();
                });
                Assert.That(ex, Is.Not.Null);
                Assert.That(ex?.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
        }
    }

    [Test]
    // [TestCase(new[] { Scopes.ReadFiles }, 401, "corpus_id_1")] // 401 scenario if desired
    [TestCase(new[] { Scopes.ReadFiles }, 200, CORPUS_ID1)]
    [TestCase(new[] { Scopes.ReadFiles }, 403, CORPUS_ID3)]
    [TestCase(new[] { Scopes.CreateTranslationEngines }, 403, CORPUS_ID1)]
    [TestCase(new[] { Scopes.ReadFiles }, 404, DOES_NOT_EXIST_ID)]
    [TestCase(new[] { Scopes.ReadFiles }, 404, "phony_corpus_id")]
    public async Task GetByIdAsync(IEnumerable<string> scope, int expectedStatusCode, string corpusId)
    {
        var corporaClient = _env.CreateCorporaClient(scope);
        switch (expectedStatusCode)
        {
            case 200:
                var corpus = await corporaClient.GetAsync(corpusId);
                Assert.That(corpus, Is.Not.Null);
                Assert.That(corpus.Id, Is.EqualTo(corpusId));
                break;
            case 403:
            case 404:
                ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await corporaClient.GetAsync(corpusId);
                });
                Assert.That(ex?.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
            default:
                Assert.Fail("Unanticipated expectedStatusCode. Check test case for typo.");
                break;
        }
    }

    [Test]
    [TestCase(new[] { Scopes.CreateFiles, Scopes.ReadFiles }, 201)]
    [TestCase(new[] { Scopes.ReadFiles }, 403)]
    public async Task CreateAsync(IEnumerable<string> scope, int expectedStatusCode)
    {
        var corporaClient = _env.CreateCorporaClient(scope);
        switch (expectedStatusCode)
        {
            case 201:
                var newCorpus = new CorpusConfig { Language = "es", Files = new List<CorpusFileConfig>() };
                var created = await corporaClient.CreateAsync(newCorpus);
                Assert.That(created, Is.Not.Null);
                var allCorpora = await corporaClient.GetAllAsync();
                Assert.That(allCorpora.Count, Is.EqualTo(3));
                break;
            case 403:
            default:
                ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    var newCorpus = new CorpusConfig { Language = "es", Files = new List<CorpusFileConfig>() };
                    await corporaClient.CreateAsync(newCorpus);
                });
                Assert.That(ex?.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
        }
    }

    [Test]
    [TestCase(new[] { Scopes.UpdateFiles, Scopes.ReadFiles }, 200, CORPUS_ID1)]
    [TestCase(new[] { Scopes.UpdateFiles, Scopes.ReadFiles }, 403, CORPUS_ID3)]
    [TestCase(new[] { Scopes.ReadFiles }, 403, CORPUS_ID1)]
    [TestCase(new[] { Scopes.UpdateFiles, Scopes.ReadFiles }, 404, DOES_NOT_EXIST_ID)]
    public async Task UpdateAsync(IEnumerable<string> scope, int expectedStatusCode, string corpusId)
    {
        var corporaClient = _env.CreateCorporaClient(scope);
        switch (expectedStatusCode)
        {
            case 200:
                var updateFiles = new List<CorpusFileConfig>
                {
                    new() { FileId = FILE_ID1, TextId = "myText" }
                };
                var updatedCorpus = await corporaClient.UpdateAsync(corpusId, updateFiles);
                Assert.That(updatedCorpus, Is.Not.Null);
                Assert.That(updatedCorpus.Files.Any(f => f.TextId == "myText"), Is.True);
                break;
            case 403:
            case 404:
                ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await corporaClient.UpdateAsync(corpusId, new List<CorpusFileConfig>());
                });
                Assert.That(ex?.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
            default:
                Assert.Fail("Unanticipated expectedStatusCode. Check test case for typo.");
                break;
        }
    }

    [Test]
    [TestCase(new[] { Scopes.DeleteFiles, Scopes.ReadFiles }, 200, CORPUS_ID1)]
    [TestCase(new[] { Scopes.DeleteFiles, Scopes.ReadFiles }, 403, CORPUS_ID3)]
    [TestCase(new[] { Scopes.ReadFiles }, 403, CORPUS_ID1)]
    [TestCase(new[] { Scopes.DeleteFiles, Scopes.ReadFiles }, 404, DOES_NOT_EXIST_ID)]
    public async Task DeleteAsync(IEnumerable<string> scope, int expectedStatusCode, string corpusId)
    {
        var corporaClient = _env.CreateCorporaClient(scope);
        switch (expectedStatusCode)
        {
            case 200:
                await corporaClient.DeleteAsync(corpusId);
                ServalApiException? exCheck = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await corporaClient.GetAsync(corpusId);
                });
                Assert.That(exCheck?.StatusCode, Is.EqualTo(404));
                break;
            case 403:
            case 404:
                ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await corporaClient.DeleteAsync(corpusId);
                });
                Assert.That(ex?.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
            default:
                Assert.Fail("Unanticipated expectedStatusCode. Check test case for typo.");
                break;
        }
    }

    [Test]
    public async Task PropagateFileDeletedToCorpus()
    {
        var scope = new[] { Scopes.DeleteFiles, Scopes.ReadFiles };
        var corporaClient = _env.CreateCorporaClient(scope);
        var dataFilesClient = _env.CreateDataFilesClient(scope);
        var originalCorpus1 = await corporaClient.GetAsync(CORPUS_ID1);
        Assert.That(originalCorpus1.Files.Count, Is.EqualTo(1));
        await dataFilesClient.DeleteAsync(FILE_ID1);
        var updatedCorpus1 = await corporaClient.GetAsync(CORPUS_ID1);
        Assert.That(updatedCorpus1.Files.Count, Is.EqualTo(0));
        var updatedCorpus2 = await corporaClient.GetAsync(CORPUS_ID2);
        Assert.That(updatedCorpus2.Files.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task AddCorpusGetAllCorpora()
    {
        var scope = new[] { Scopes.CreateFiles, Scopes.ReadFiles };
        var corporaClient = _env.CreateCorporaClient(scope);
        var newCorpus = new CorpusConfig { Language = "es", Files = new List<CorpusFileConfig>() };
        var created = await corporaClient.CreateAsync(newCorpus);
        Assert.That(created, Is.Not.Null);
        var allCorpora = await corporaClient.GetAllAsync();
        Assert.That(allCorpora.Count, Is.EqualTo(3));
    }

    [TearDown]
    public void TearDown()
    {
        _env.Dispose();
    }

    private class TestEnvironment : DisposableBase
    {
        private readonly MongoClient _mongoClient;
        private readonly IServiceScope _scope;

        public TestEnvironment()
        {
            _mongoClient = new MongoClient();
            ResetDatabases();

            Factory = new ServalWebApplicationFactory();
            _scope = Factory.Services.CreateScope();
            DataFiles = _scope.ServiceProvider.GetRequiredService<IRepository<DataFiles.Models.DataFile>>();
            Corpora = _scope.ServiceProvider.GetRequiredService<IRepository<DataFiles.Models.Corpus>>();
        }

        ServalWebApplicationFactory Factory { get; }
        public IRepository<DataFiles.Models.DataFile> DataFiles { get; }
        public IRepository<DataFiles.Models.Corpus> Corpora { get; }

        public DataFilesClient CreateDataFilesClient(IEnumerable<string> scope)
        {
            HttpClient httpClient = Factory.WithWebHostBuilder(_ => { }).CreateClient();
            if (scope is not null)
                httpClient.DefaultRequestHeaders.Add("Scope", string.Join(" ", scope));
            return new DataFilesClient(httpClient);
        }

        public CorporaClient CreateCorporaClient(IEnumerable<string> scope)
        {
            HttpClient httpClient = Factory.WithWebHostBuilder(_ => { }).CreateClient();
            if (scope is not null)
                httpClient.DefaultRequestHeaders.Add("Scope", string.Join(" ", scope));
            return new CorporaClient(httpClient);
        }

        public void ResetDatabases()
        {
            _mongoClient.DropDatabase("serval_test");
            _mongoClient.DropDatabase("serval_test_jobs");
        }

        protected override void DisposeManagedResources()
        {
            _scope.Dispose();
            Factory.Dispose();
            ResetDatabases();
        }
    }
}
