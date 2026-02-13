namespace Serval.ApiServer;

[TestFixture]
[Category("Integration")]
public class WebhooksTests
{
    const string ID = "000000000000000000000000";
    const string DOES_NOT_EXIST_ID = "000000000000000000000001";

    TestEnvironment _env;

    [SetUp]
    public async Task Setup()
    {
        _env = new TestEnvironment();
        var webhook = new Webhooks.Models.Webhook
        {
            Id = ID,
            Owner = "client1",
            Url = "/a/url",
            Secret = "s3CreT#",
            Events = [Webhooks.Contracts.WebhookEvent.TranslationBuildStarted],
        };
        await _env.Webhooks.InsertAsync(webhook);
    }

    [Test]
    [TestCase(null, 200)] //null gives all scope privileges
    [TestCase(new string[] { Scopes.ReadFiles }, 403)] //Arbitrary unrelated privilege
    public async Task GetAllWebhooksAsync(IEnumerable<string>? scope, int expectedStatusCode)
    {
        WebhooksClient client = _env.CreateClient(scope);
        switch (expectedStatusCode)
        {
            case 200:
                Webhook result = (await client.GetAllAsync()).First();
                Assert.That(result.Id, Is.EqualTo(ID));
                break;
            case 403:
                ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.GetAllAsync();
                });
                Assert.That(ex?.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
            default:
                Assert.Fail("Unanticipated expectedStatusCode. Check test case for typo.");
                break;
        }
    }

    [Test]
    [TestCase(null, 200, ID)] //null gives all scope privileges
    [TestCase(null, 404, DOES_NOT_EXIST_ID)]
    [TestCase(new string[] { Scopes.ReadFiles }, 403, ID)] //Arbitrary unrelated privilege
    [TestCase(null, 404, "phony_id")]
    public async Task GetWebhookByIdAsync(IEnumerable<string>? scope, int expectedStatusCode, string webhookId)
    {
        WebhooksClient client = _env.CreateClient(scope);
        switch (expectedStatusCode)
        {
            case 200:
                Webhook result = await client.GetAsync(webhookId);
                Assert.That(result.Id, Is.EqualTo(ID));
                break;
            case 403:
            case 404:
                ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.GetAsync(webhookId);
                });
                Assert.That(ex?.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
            default:
                Assert.Fail("Unanticipated expectedStatusCode. Check test case for typo.");
                break;
        }
    }

    [Test]
    [TestCase(null, 200, ID)] //null gives all scope privileges
    [TestCase(null, 404, DOES_NOT_EXIST_ID)]
    [TestCase(new string[] { Scopes.ReadFiles }, 403, ID)] //Arbitrary unrelated privilege
    public async Task DeleteWebhookByIdAsync(IEnumerable<string>? scope, int expectedStatusCode, string webhookId)
    {
        WebhooksClient client = _env.CreateClient(scope);
        switch (expectedStatusCode)
        {
            case 200:
            {
                await client.DeleteAsync(webhookId);
                ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.GetAsync(webhookId);
                });
                Assert.That(ex?.StatusCode, Is.EqualTo(404));
                break;
            }
            case 403:
            case 404:
            {
                ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.DeleteAsync(webhookId);
                });
                Assert.That(ex?.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
            }
            default:
                Assert.Fail("Unanticipated expectedStatusCode. Check test case for typo.");
                break;
        }
    }

    [Test]
    [TestCase(null, 201)]
    [TestCase(new string[] { Scopes.ReadFiles }, 403)] //Arbitrary unrelated privilege
    public async Task CreateWebhookAsync(IEnumerable<string>? scope, int expectedStatusCode)
    {
        WebhooksClient client = _env.CreateClient(scope);
        switch (expectedStatusCode)
        {
            case 201:
                Webhook result = await client.CreateAsync(
                    new WebhookConfig
                    {
                        PayloadUrl = "/a/different/url",
                        Secret = "M0rEs3CreTz#",
                        Events = { WebhookEvent.TranslationBuildStarted },
                    }
                );
                Webhook resultAfterCreate = await client.GetAsync(result.Id);
                Assert.That(resultAfterCreate.PayloadUrl, Is.EqualTo(result.PayloadUrl));

                break;
            case 403:
                ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    Webhook result = await client.CreateAsync(
                        new WebhookConfig
                        {
                            PayloadUrl = "/a/different/url",
                            Secret = "M0rEs3CreTz#",
                            Events = { WebhookEvent.TranslationBuildStarted },
                        }
                    );
                });
                Assert.That(ex?.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
            default:
                Assert.Fail("Unanticipated expectedStatusCode. Check test case for typo.");
                break;
        }
    }

    [TearDown]
    public void TearDown()
    {
        _env.Dispose();
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
            Webhooks = _scope.ServiceProvider.GetRequiredService<IRepository<Webhooks.Models.Webhook>>();
        }

        ServalWebApplicationFactory Factory { get; }
        public IRepository<Webhooks.Models.Webhook> Webhooks { get; }

        public WebhooksClient CreateClient(IEnumerable<string>? scope)
        {
            scope ??= new[] { Scopes.ReadHooks, Scopes.CreateHooks, Scopes.DeleteHooks };

            HttpClient httpClient = Factory.WithWebHostBuilder(_ => { }).CreateClient();
            httpClient.DefaultRequestHeaders.Add("Scope", string.Join(" ", scope));
            return new WebhooksClient(httpClient);
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
