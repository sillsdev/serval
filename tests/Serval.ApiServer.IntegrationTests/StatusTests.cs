namespace Serval.ApiServer;

[TestFixture]
[Category("Integration")]
public class StatusTests
{
    TestEnvironment? _env;

    [SetUp]
    public void SetUp()
    {
        _env = new TestEnvironment();
    }

    [Test]
    [TestCase(new[] { Scopes.ReadStatus }, 200)]
    // [TestCase(new[] { Scopes.ReadStatus }, 401)]
    [TestCase(new[] { Scopes.CreateTranslationEngines }, 403)]
    public async Task GetHealthAsync(IEnumerable<string> scope, int expectedStatusCode)
    {
        StatusClient client = _env!.CreateClient(scope);
        ServalApiException ex;
        switch (expectedStatusCode)
        {
            case 200:
                // the grpc services are not running, so the health check will fail
                HealthReport healthReport = await client.GetHealthAsync();
                Assert.That(healthReport, Is.Not.Null);
                Assert.That(healthReport.Status.ToString(), Is.Not.EqualTo("Healthy"));
                Assert.That(healthReport.Results, Has.Count.EqualTo(5));
                break;
            case 403:
                ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.GetHealthAsync();
                });
                Assert.That(ex, Is.Not.Null);
                Assert.That(ex!.StatusCode, Is.EqualTo(expectedStatusCode));
                break;

            default:
                Assert.Fail("Unanticipated expectedStatusCode. Check test case for typo.");
                break;
        }
    }

    [Test]
    [TestCase(new[] { Scopes.ReadStatus }, 200)]
    // [TestCase(new[] { Scopes.ReadStatus }, 401)]
    [TestCase(new[] { Scopes.CreateTranslationEngines }, 403)]
    public async Task GetDeploymentAsync(IEnumerable<string> scope, int expectedStatusCode)
    {
        StatusClient client = _env!.CreateClient(scope);
        switch (expectedStatusCode)
        {
            case 200:
                DeploymentInfo result = await client.GetDeploymentInfoAsync();
                Assert.That(result, Is.Not.Null);
                Assert.That(result.DeploymentVersion, Is.Not.EqualTo("Unknown"));
                Assert.That(result.AspNetCoreEnvironment, Is.Not.EqualTo("Unknown"));
                break;
            case 403:
                var ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.GetHealthAsync();
                });
                Assert.That(ex, Is.Not.Null);
                Assert.That(ex!.StatusCode, Is.EqualTo(expectedStatusCode));
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
            var clientSettings = new MongoClientSettings { LinqProvider = LinqProvider.V2 };
            _mongoClient = new MongoClient(clientSettings);
            ResetDatabases();

            Factory = new ServalWebApplicationFactory();
            _scope = Factory.Services.CreateScope();
        }

        ServalWebApplicationFactory Factory { get; }

        public StatusClient CreateClient(IEnumerable<string> scope)
        {
            var httpClient = Factory.WithWebHostBuilder(_ => { }).CreateClient();
            if (scope is not null)
                httpClient.DefaultRequestHeaders.Add("Scope", string.Join(" ", scope));
            return new StatusClient(httpClient);
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
