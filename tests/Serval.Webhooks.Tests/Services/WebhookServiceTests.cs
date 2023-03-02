namespace Serval.Webhooks.Services;

[TestFixture]
public class WebhookServiceTests
{
    [Test]
    public async Task SendEventAsync_NoHooks()
    {
        var env = new TestEnvironment();
        MockedRequest req = env.MockHttp.When("*").Respond(HttpStatusCode.OK);

        var payload = new { BuildId = "build1", EngineId = "engine1" };
        await env.Service.SendEventAsync(WebhookEvent.BuildStarted, "client", payload);

        Assert.That(env.MockHttp.GetMatchCount(req), Is.EqualTo(0));
    }

    [Test]
    public async Task SendEventAsync_MatchingHook()
    {
        var env = new TestEnvironment();
        env.Hooks.Add(
            new Webhook
            {
                Id = "hook1",
                Url = "https://test.client.com/hook",
                Secret = "this is a secret",
                Owner = "client",
                Events = { WebhookEvent.BuildStarted }
            }
        );
        env.MockHttp
            .Expect("https://test.client.com/hook")
            .WithHeaders(
                "X-Hub-Signature-256",
                "sha256=AA472F74F9B51BA61EC4EC79B56193B3A9A21318C146098FF7114070DC8C06FB"
            )
            .Respond(HttpStatusCode.OK);

        var payload = new { BuildId = "build1", EngineId = "engine1" };
        await env.Service.SendEventAsync(WebhookEvent.BuildStarted, "client", payload);

        env.MockHttp.VerifyNoOutstandingExpectation();
    }

    [Test]
    public async Task SendEventAsync_NoMatchingHook()
    {
        var env = new TestEnvironment();
        env.Hooks.Add(
            new Webhook
            {
                Id = "hook1",
                Url = "https://test.client.com/hook",
                Secret = "this is a secret",
                Owner = "client",
                Events = { WebhookEvent.BuildStarted }
            }
        );
        MockedRequest req = env.MockHttp.When("*").Respond(HttpStatusCode.OK);

        var payload = new { BuildId = "build1", EngineId = "engine1" };
        await env.Service.SendEventAsync(WebhookEvent.BuildFinished, "client", payload);

        Assert.That(env.MockHttp.GetMatchCount(req), Is.EqualTo(0));
    }

    [Test]
    public async Task SendEventAsync_RequestTimeout()
    {
        var env = new TestEnvironment();
        env.Hooks.Add(
            new Webhook
            {
                Id = "hook1",
                Url = "https://test.client.com/hook",
                Secret = "this is a secret",
                Owner = "client",
                Events = { WebhookEvent.BuildStarted }
            }
        );
        env.MockHttp
            .Expect("https://test.client.com/hook")
            .WithHeaders(
                "X-Hub-Signature-256",
                "sha256=AA472F74F9B51BA61EC4EC79B56193B3A9A21318C146098FF7114070DC8C06FB"
            )
            .Respond(HttpStatusCode.RequestTimeout);

        var payload = new { BuildId = "build1", EngineId = "engine1" };
        await env.Service.SendEventAsync(WebhookEvent.BuildStarted, "client", payload);

        env.MockHttp.VerifyNoOutstandingExpectation();
    }

    [Test]
    public async Task SendEventAsync_Exception()
    {
        var env = new TestEnvironment();
        env.Hooks.Add(
            new Webhook
            {
                Id = "hook1",
                Url = "https://test.client.com/hook",
                Secret = "this is a secret",
                Owner = "client",
                Events = { WebhookEvent.BuildStarted }
            }
        );
        env.MockHttp
            .Expect("https://test.client.com/hook")
            .WithHeaders(
                "X-Hub-Signature-256",
                "sha256=AA472F74F9B51BA61EC4EC79B56193B3A9A21318C146098FF7114070DC8C06FB"
            )
            .Throw(new HttpRequestException());

        var payload = new { BuildId = "build1", EngineId = "engine1" };
        await env.Service.SendEventAsync(WebhookEvent.BuildStarted, "client", payload);

        env.MockHttp.VerifyNoOutstandingExpectation();
    }

    private class TestEnvironment
    {
        public TestEnvironment()
        {
            var jsonOptions = new JsonOptions();
            jsonOptions.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            jsonOptions.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            Service = new WebhookService(Hooks, new OptionsWrapper<JsonOptions>(jsonOptions), MockHttp.ToHttpClient());
        }

        public IWebhookService Service { get; }
        public MemoryRepository<Webhook> Hooks { get; } = new MemoryRepository<Webhook>();
        public MockHttpMessageHandler MockHttp { get; } = new MockHttpMessageHandler();
    }
}
