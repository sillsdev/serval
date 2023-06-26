namespace Serval.ApiServer;
using Serval.DataFiles.Models;
using Shared.Contracts;
using Microsoft.AspNetCore.Mvc;

[TestFixture]
[Category("Integration")]
public class DataFilesTests
{
    TestEnvironment? _env;

    const string ID1 = "000000000000000000000000";
    const string NAME1 = "sample1.txt";

    const string ID2 = "000000000000000000000001";
    const string NAME2 = "sample2.txt";

    const string ID3 = "000000000000000000000002";
    const string NAME3 = "sample3.txt";
    const string DOES_NOT_EXIST_ID = "00000000000000000000003";

    [SetUp]
    public async Task SetUp()
    {
        _env = new TestEnvironment();
        DataFile file1,
            file2,
            file3;

        file1 = new DataFile
        {
            Id = ID1,
            Owner = "client1",
            Name = NAME1,
            Filename = NAME1,
            Format = FileFormat.Text
        };
        file2 = new DataFile
        {
            Id = ID2,
            Owner = "client1",
            Name = NAME2,
            Filename = NAME2,
            Format = FileFormat.Text
        };
        file3 = new DataFile
        {
            Id = ID3,
            Owner = "client2",
            Name = NAME3,
            Filename = NAME3,
            Format = FileFormat.Text
        };
        await _env.DataFiles.InsertAllAsync(new[] { file1, file2, file3 });
    }

    [Test]
    [TestCase(new[] { Scopes.ReadFiles }, 200)]
    // [TestCase(new[] { Scopes.ReadFiles }, 401)]
    [TestCase(new[] { Scopes.CreateTranslationEngines }, 403)]
    public async Task GetAllAsync(IEnumerable<string> scope, int expectedStatusCode)
    {
        DataFilesClient client = _env!.CreateClient(scope);
        ServalApiException? ex;
        switch (expectedStatusCode)
        {
            case 200:
                ICollection<Serval.Client.DataFile> results = await client.GetAllAsync();

                Assert.That(results, Has.Count.EqualTo(2));
                Assert.That(results.All(dataFile => dataFile.Revision == 1));
                break;
            case 401:
                //TODO setup unauthorized client (verfiy possibility of 401 - see DFController)
                expectedStatusCode = 403;
                goto case 403;
            case 403:
                ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.GetAllAsync();
                });
                Assert.NotNull(ex);
                Assert.That(ex!.StatusCode, Is.EqualTo(expectedStatusCode));
                break;

            default:
                Assert.Fail("Unanticipated expectedStatusCode. Check test case for typo.");
                break;
        }
    }

    [Test]
    [TestCase(new[] { Scopes.ReadFiles }, 200, ID1)]
    // [TestCase(new[] { Scopes.ReadFiles }, 401, ID1)]
    [TestCase(new[] { Scopes.ReadFiles }, 403, ID3)]
    [TestCase(new[] { Scopes.CreateTranslationEngines }, 403, ID1)]
    [TestCase(new[] { Scopes.ReadFiles }, 404, DOES_NOT_EXIST_ID)]
    public async Task GetByIDAsync(IEnumerable<string> scope, int expectedStatusCode, string fileId)
    {
        DataFilesClient client = _env!.CreateClient(scope);
        ServalApiException? ex;
        switch (expectedStatusCode)
        {
            case 200:
                Serval.Client.DataFile result = await client.GetAsync(fileId);
                Assert.That(result.Name, Is.EqualTo(NAME1));
                break;
            case 401:
                //NOTE Covered in end-to-end tests
                goto case 403;
            case 403:
            case 404:
                ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.GetAsync(fileId);
                });
                Assert.That(ex!.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
            default:
                Assert.Fail("Invalid expectedStatusCode. Check test case for typo.");
                break;
        }
    }

    [Test]
    [TestCase(new[] { Scopes.CreateFiles, Scopes.ReadFiles }, 201)]
    // [TestCase(new[] { Scopes.CreateFiles, Scopes.ReadFiles }, 401)]
    [TestCase(new[] { Scopes.CreateFiles, Scopes.ReadFiles }, 400)]
    [TestCase(new[] { Scopes.ReadFiles }, 403)]
    public void CreateAsync(IEnumerable<string> scope, int expectedStatusCode)
    {
        DataFilesClient client = _env!.CreateClient(scope);
        ServalApiException? ex;
        FileParameter fp;
        Stream fs;
        switch (expectedStatusCode)
        {
            case 201:
                Assert.DoesNotThrowAsync(async () =>
                {
                    fs = new MemoryStream();
                    fp = new FileParameter(fs);
                    await client.CreateAsync(fp, Client.FileFormat.Text);
                    ICollection<Serval.Client.DataFile> results = await client.GetAllAsync();

                    Assert.That(results, Has.Count.EqualTo(3)); //2 from set-up + 1 added above = 3
                    Assert.That(results.All(dataFile => dataFile.Revision == 1));
                    fs.Dispose();
                });
                break;
            case 400:
                byte[] bytes = new byte[2_000_000_000];
                fs = new MemoryStream(bytes);
                fp = new FileParameter(fs);
                fp = new FileParameter(fs);
                ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.CreateAsync(fp, Client.FileFormat.Text);
                });
                fs.Dispose();
                Assert.That(ex!.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
            case 403:
                fs = new MemoryStream();
                fp = new FileParameter(fs);
                ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.CreateAsync(fp, Client.FileFormat.Text);
                });
                fs.Dispose();
                Assert.That(ex!.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
            default:
                Assert.Fail("Unanticipated expectedStatusCode. Check test case for typo.");
                break;
        }
    }

    [Test]
    [TestCase(new[] { Scopes.UpdateFiles, Scopes.ReadFiles }, 200, ID1)]
    [TestCase(new[] { Scopes.UpdateFiles, Scopes.ReadFiles }, 400, ID1)]
    [TestCase(new[] { Scopes.UpdateFiles, Scopes.ReadFiles }, 403, ID3)]
    [TestCase(new[] { Scopes.ReadFiles }, 403, ID1)]
    [TestCase(new[] { Scopes.UpdateFiles, Scopes.ReadFiles }, 404, DOES_NOT_EXIST_ID)]
    public async Task UpdateAsync(IEnumerable<string> scope, int expectedStatusCode, string fileId)
    {
        DataFilesClient client = _env!.CreateClient(scope);
        ServalApiException? ex;
        switch (expectedStatusCode)
        {
            case 200:
                Serval.Client.DataFile result = await client.GetAsync(fileId);
                Assert.That(result.Name, Is.EqualTo(NAME1));
                Assert.DoesNotThrowAsync(async () =>
                {
                    await client.UpdateAsync(fileId, new FileParameter(new MemoryStream()));
                });
                Serval.Client.DataFile resultAfterUpdate = await client.GetAsync(fileId);
                Assert.That(resultAfterUpdate.Id, Is.EqualTo(ID1));
                break;
            case 400:
                ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.UpdateAsync(fileId, new FileParameter(new MemoryStream(new byte[2_000_000_000])));
                });
                Assert.That(ex!.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
            case 403:
            case 404:
                ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.UpdateAsync(fileId, new FileParameter(new MemoryStream()));
                });
                Assert.That(ex!.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
            default:
                Assert.Fail("Unanticipated expectedStatusCode. Check test case for typo.");
                break;
        }
    }

    [Test]
    [TestCase(new[] { Scopes.DeleteFiles, Scopes.ReadFiles }, 200, ID1)]
    // [TestCase(new[] { Scopes.ReadFiles }, 401, ID1)]
    [TestCase(new[] { Scopes.DeleteFiles, Scopes.ReadFiles }, 403, ID3)]
    [TestCase(new[] { Scopes.ReadFiles }, 403, ID1)]
    [TestCase(new[] { Scopes.DeleteFiles, Scopes.ReadFiles }, 404, DOES_NOT_EXIST_ID)]
    public async Task DeleteAsync(IEnumerable<string> scope, int expectedStatusCode, string fileId)
    {
        DataFilesClient client = _env!.CreateClient(scope);
        ServalApiException? ex;
        switch (expectedStatusCode)
        {
            case 200:
                Serval.Client.DataFile result = await client.GetAsync(fileId);
                Assert.That(result.Name, Is.EqualTo(NAME1));
                Assert.DoesNotThrowAsync(async () =>
                {
                    await client.DeleteAsync(fileId);
                });
                ICollection<Serval.Client.DataFile> results = await client.GetAllAsync();
                Assert.That(results, Has.Count.EqualTo(1));
                Assert.That(results.First().Id, Is.EqualTo(ID2));
                break;
            case 401:
                //NOTE Covered in end-to-end tests
                goto case 403;
            case 403:
            case 404:
                ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.DeleteAsync(fileId);
                });
                Assert.That(ex!.StatusCode, Is.EqualTo(expectedStatusCode));
                ICollection<Serval.Client.DataFile> resultsAfterDelete = await client.GetAllAsync();
                Assert.That(resultsAfterDelete, Has.Count.EqualTo(2));
                break;
            default:
                Assert.Fail("Unanticipated expectedStatusCode. Check test case for typo.");
                break;
        }
    }

    [TearDown]
    public void TearDown()
    {
        _env!.Dispose();
    }

    private class TestEnvironment : DisposableBase
    {
        private readonly IMongoClient _mongoClient;
        private readonly IServiceScope _scope;

        public TestEnvironment()
        {
            _mongoClient = new MongoClient();
            ResetDatabases();

            Factory = new ServalWebApplicationFactory();
            _scope = Factory.Services.CreateScope();
            DataFiles = _scope.ServiceProvider.GetRequiredService<IRepository<DataFiles.Models.DataFile>>();
        }

        ServalWebApplicationFactory Factory { get; }
        public IRepository<DataFiles.Models.DataFile> DataFiles { get; }

        public DataFilesClient CreateClient(IEnumerable<string> scope)
        {
            var httpClient = Factory.WithWebHostBuilder(_ => { }).CreateClient();
            if (scope is not null)
                httpClient.DefaultRequestHeaders.Add("Scope", string.Join(" ", scope));
            return new DataFilesClient(httpClient);
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
