namespace Serval.ApiServer;

[TestFixture]
[Category("Integration")]
public class WebhooksTests
{
    const string ID = "000000000000000000000000";
    const string DOES_NOT_EXIST_ID = "000000000000000000000001";

    TestEnvironment? _env;

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
            Events = new List<Webhooks.Contracts.WebhookEvent>
            {
                Webhooks.Contracts.WebhookEvent.TranslationBuildStarted
            }
        };
        await _env.Webhooks.InsertAsync(webhook);
    }

    [Test]
    [TestCase(null, 200)] //null gives all scope privileges
    public void GetAllWebhooksAsync(IEnumerable<string>? scope, int expectedStatusCode)
    {
        WebhooksClient client = _env!.CreateClient(scope);
        switch (expectedStatusCode)
        {
            case 200:
                Serval.Client.Webhook? result = null;
                Assert.DoesNotThrowAsync(async () =>
                {
                    result = (await client.GetAllAsync()).First();
                });
                Assert.NotNull(result);
                Assert.That(result!.Id, Is.EqualTo(ID));
                break;
            default:
                Assert.Fail("Unanticipated expectedStatusCode. Check test case for typo.");
                break;
        }
    }

    [Test]
    [TestCase(null, 200, ID)] //null gives all scope privileges
    [TestCase(null, 404, DOES_NOT_EXIST_ID)]
    public void GetWebhookByIdAsync(IEnumerable<string>? scope, int expectedStatusCode, string webhookId)
    {
        WebhooksClient client = _env!.CreateClient(scope);
        ServalApiException? ex = null;
        switch (expectedStatusCode)
        {
            case 200:
                Serval.Client.Webhook? result = null;
                Assert.DoesNotThrowAsync(async () =>
                {
                    result = await client.GetAsync(webhookId);
                });
                Assert.NotNull(result);
                Assert.That(result!.Id, Is.EqualTo(ID));
                break;
            case 404:
                ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.GetAsync(webhookId);
                });
                Assert.That(ex!.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
            default:
                Assert.Fail("Unanticipated expectedStatusCode. Check test case for typo.");
                break;
        }
    }

    [Test]
    [TestCase(null, 200, ID)] //null gives all scope privileges
    [TestCase(null, 404, DOES_NOT_EXIST_ID)]
    public void DeleteWebhookByIdAsync(IEnumerable<string>? scope, int expectedStatusCode, string webhookId)
    {
        WebhooksClient client = _env!.CreateClient(scope);
        ServalApiException? ex = null;
        switch (expectedStatusCode)
        {
            case 200:
                Assert.DoesNotThrowAsync(async () =>
                {
                    await client.DeleteAsync(webhookId);
                });
                ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.GetAsync(webhookId);
                });
                Assert.That(ex!.StatusCode, Is.EqualTo(404));
                break;
            case 404:
                ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.DeleteAsync(webhookId);
                });
                Assert.That(ex!.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
            default:
                Assert.Fail("Unanticipated expectedStatusCode. Check test case for typo.");
                break;
        }
    }

    [Test]
    [TestCase(null, 201)]
    public void CreateWebhookAsync(IEnumerable<string> scope, int expectedStatusCode)
    {
        WebhooksClient client = _env!.CreateClient(scope);
        switch (expectedStatusCode)
        {
            case 201:
                Serval.Client.Webhook? result = null;
                Assert.DoesNotThrowAsync(async () =>
                {
                    result = await client.CreateAsync(
                        new WebhookConfig
                        {
                            PayloadUrl = "/a/different/url",
                            Secret = "M0rEs3CreTz#",
                            Events = new List<Serval.Client.WebhookEvent>
                            {
                                Serval.Client.WebhookEvent.TranslationBuildStarted
                            }
                        }
                    );
                });
                Serval.Client.Webhook? result_afterCreate = null;
                Assert.DoesNotThrowAsync(async () =>
                {
                    result_afterCreate = await client.GetAsync(result!.Id);
                });
                Assert.NotNull(result_afterCreate);
                Assert.That(result_afterCreate!.PayloadUrl, Is.EqualTo(result!.PayloadUrl));

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
            var clientSettings = new MongoClientSettings();
            clientSettings.LinqProvider = LinqProvider.V2;
            _mongoClient = new MongoClient(clientSettings);
            ResetDatabases();

            Factory = new ServalWebApplicationFactory();
            _scope = Factory.Services.CreateScope();
            Webhooks = _scope.ServiceProvider.GetRequiredService<IRepository<Webhooks.Models.Webhook>>();
        }

        ServalWebApplicationFactory Factory { get; }
        public IRepository<Webhooks.Models.Webhook> Webhooks { get; }

        public WebhooksClient CreateClient(IEnumerable<string>? scope)
        {
            if (scope is null)
            {
                scope = new[] { Scopes.ReadHooks, Scopes.CreateHooks, Scopes.DeleteHooks };
            }

            var httpClient = Factory.WithWebHostBuilder(_ => { }).CreateClient();
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
