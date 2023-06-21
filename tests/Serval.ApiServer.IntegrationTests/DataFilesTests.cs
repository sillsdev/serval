namespace Serval.ApiServer;
using Serval.DataFiles.Models;
using Shared.Contracts;
using Microsoft.AspNetCore.Mvc;

[TestFixture]
[Category("Integration")]
public class DataFilesTests
{
    TestEnvironment env;

    [SetUp]
    public async Task SetUp()
    {
        env = new TestEnvironment();
        DataFile file1,
            file2,
            file3;

        file1 = new DataFile
        {
            Id = "000000000000000000000000",
            Owner = "client1",
            Name = "sample1.txt",
            Filename = "sample1.txt",
            Format = FileFormat.Text
        };
        file2 = new DataFile
        {
            Id = "000000000000000000000001",
            Owner = "client1",
            Name = "sample2.txt",
            Filename = "sample2.txt",
            Format = FileFormat.Text
        };
        file3 = new DataFile
        {
            Id = "000000000000000000000002",
            Owner = "client2",
            Name = "sample3.txt",
            Filename = "sample3.txt",
            Format = FileFormat.Text
        };
        await env.DataFiles.InsertAllAsync(new[] { file1, file2, file3 });
    }

    [Test]
    [TestCase(new[] { Scopes.ReadFiles }, 200)]
    // [TestCase(new[] { Scopes.ReadFiles }, 401)]
    [TestCase(new[] { Scopes.CreateTranslationEngines }, 403)]
    public async Task GetAllAsync(IEnumerable<string> scope, int expectedStatusCode)
    {
        DataFilesClient client = env.CreateClient(scope);
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
                Assert.That(ex.StatusCode, Is.EqualTo(expectedStatusCode));
                break;

            default:
                Assert.Fail("Unanticipated expectedStatusCode. Check test case for typo.");
                break;
        }
    }

    [Test]
    [TestCase(new[] { Scopes.ReadFiles }, 200, "000000000000000000000000")]
    // [TestCase(new[] { Scopes.ReadFiles }, 401, "000000000000000000000000")]
    [TestCase(new[] { Scopes.ReadFiles }, 403, "000000000000000000000002")]
    [TestCase(new[] { Scopes.CreateTranslationEngines }, 403, "000000000000000000000000")]
    [TestCase(new[] { Scopes.ReadFiles }, 404, "000000000000000000000005")]
    public async Task GetByIDAsync(IEnumerable<string> scope, int expectedStatusCode, string fileId)
    {
        DataFilesClient client = env.CreateClient(scope);
        ServalApiException? ex;
        switch (expectedStatusCode)
        {
            case 200:
                Serval.Client.DataFile result = await client.GetAsync(fileId);
                Assert.That(result.Name, Is.EqualTo("sample1.txt"));
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
                Assert.That(ex.StatusCode, Is.EqualTo(expectedStatusCode));
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
    public async Task CreateAsync(IEnumerable<string> scope, int expectedStatusCode)
    {
        DataFilesClient client = env.CreateClient(scope);
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
                Assert.That(ex.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
            case 403:
                fs = new MemoryStream();
                fp = new FileParameter(fs);
                ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.CreateAsync(fp, Client.FileFormat.Text);
                });
                fs.Dispose();
                Assert.That(ex.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
            default:
                Assert.Fail("Unanticipated expectedStatusCode. Check test case for typo.");
                break;
        }
    }

    [Test]
    [TestCase(new[] { Scopes.UpdateFiles, Scopes.ReadFiles }, 200, "000000000000000000000000")]
    [TestCase(new[] { Scopes.UpdateFiles, Scopes.ReadFiles }, 400, "000000000000000000000000")]
    [TestCase(new[] { Scopes.UpdateFiles, Scopes.ReadFiles }, 403, "000000000000000000000002")]
    [TestCase(new[] { Scopes.ReadFiles }, 403, "000000000000000000000000")]
    [TestCase(new[] { Scopes.UpdateFiles, Scopes.ReadFiles }, 404, "000000000000000000000005")]
    public async Task UpdateAsync(IEnumerable<string> scope, int expectedStatusCode, string fileId)
    {
        DataFilesClient client = env.CreateClient(scope);
        ServalApiException? ex;
        switch (expectedStatusCode)
        {
            case 200:
                Serval.Client.DataFile result = await client.GetAsync(fileId);
                Assert.That(result.Name, Is.EqualTo("sample1.txt"));
                Assert.DoesNotThrowAsync(async () =>
                {
                    await client.UpdateAsync(fileId, new FileParameter(new MemoryStream()));
                });
                Serval.Client.DataFile resultAfterUpdate = await client.GetAsync(fileId);
                Assert.That(resultAfterUpdate.Id, Is.EqualTo("000000000000000000000000"));
                break;
            case 400:
                ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.UpdateAsync(fileId, new FileParameter(new MemoryStream(new byte[2_000_000_000])));
                });
                Assert.That(ex.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
            case 403:
            case 404:
                ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.UpdateAsync(fileId, new FileParameter(new MemoryStream()));
                });
                Assert.That(ex.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
            default:
                Assert.Fail("Unanticipated expectedStatusCode. Check test case for typo.");
                break;
        }
    }

    [Test]
    [TestCase(new[] { Scopes.DeleteFiles, Scopes.ReadFiles }, 200, "000000000000000000000000")]
    // [TestCase(new[] { Scopes.ReadFiles }, 401, "000000000000000000000000")]
    [TestCase(new[] { Scopes.DeleteFiles, Scopes.ReadFiles }, 403, "000000000000000000000002")]
    [TestCase(new[] { Scopes.ReadFiles }, 403, "000000000000000000000000")]
    [TestCase(new[] { Scopes.DeleteFiles, Scopes.ReadFiles }, 404, "000000000000000000000005")]
    public async Task DeleteAsync(IEnumerable<string> scope, int expectedStatusCode, string fileId)
    {
        DataFilesClient client = env.CreateClient(scope);
        ServalApiException? ex;
        switch (expectedStatusCode)
        {
            case 200:
                Serval.Client.DataFile result = await client.GetAsync(fileId);
                Assert.That(result.Name, Is.EqualTo("sample1.txt"));
                Assert.DoesNotThrowAsync(async () =>
                {
                    await client.DeleteAsync(fileId);
                });
                ICollection<Serval.Client.DataFile> results = await client.GetAllAsync();
                Assert.That(results, Has.Count.EqualTo(1));
                Assert.That(results.First().Id, Is.EqualTo("000000000000000000000001"));
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
                Assert.That(ex.StatusCode, Is.EqualTo(expectedStatusCode));
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
        env.Dispose();
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
