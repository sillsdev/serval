﻿namespace Serval.Webhooks.Services;

[TestFixture]
public class WebhookJobTests
{
    [Test]
    public async Task RunAsync_NoHooks()
    {
        var env = new TestEnvironment();
        MockedRequest req = env.MockHttp.When("*").Respond(HttpStatusCode.OK);

        var payload = new { BuildId = "build1", EngineId = "engine1" };
        await env.Job.RunAsync(WebhookEvent.TranslationBuildStarted, "client", payload, CancellationToken.None);

        Assert.That(env.MockHttp.GetMatchCount(req), Is.EqualTo(0));
    }

    [Test]
    public async Task RunAsync_MatchingHook()
    {
        var env = new TestEnvironment();
        env.Hooks.Add(
            new Webhook
            {
                Id = "hook1",
                Url = "https://test.client.com/hook",
                Secret = "this is a secret",
                Owner = "client",
                Events = new List<WebhookEvent> { WebhookEvent.TranslationBuildStarted }
            }
        );
        env.MockHttp
            .Expect("https://test.client.com/hook")
            .WithHeaders(
                "X-Hub-Signature-256",
                "sha256=8EC2360A34811845884D8FCE03866EA8FAD9429AAA9E6247D7A817AD2E170B8F"
            )
            .Respond(HttpStatusCode.OK);

        var payload = new { BuildId = "build1", EngineId = "engine1" };
        await env.Job.RunAsync(WebhookEvent.TranslationBuildStarted, "client", payload, CancellationToken.None);

        env.MockHttp.VerifyNoOutstandingExpectation();
    }

    [Test]
    public async Task RunAsync_NoMatchingHook()
    {
        var env = new TestEnvironment();
        env.Hooks.Add(
            new Webhook
            {
                Id = "hook1",
                Url = "https://test.client.com/hook",
                Secret = "this is a secret",
                Owner = "client",
                Events = new List<WebhookEvent> { WebhookEvent.TranslationBuildStarted }
            }
        );
        MockedRequest req = env.MockHttp.When("*").Respond(HttpStatusCode.OK);

        var payload = new { BuildId = "build1", EngineId = "engine1" };
        await env.Job.RunAsync(WebhookEvent.TranslationBuildFinished, "client", payload, CancellationToken.None);

        Assert.That(env.MockHttp.GetMatchCount(req), Is.EqualTo(0));
    }

    [Test]
    public void RunAsync_RequestTimeout()
    {
        var env = new TestEnvironment();
        env.Hooks.Add(
            new Webhook
            {
                Id = "hook1",
                Url = "https://test.client.com/hook",
                Secret = "this is a secret",
                Owner = "client",
                Events = new List<WebhookEvent> { WebhookEvent.TranslationBuildStarted }
            }
        );
        env.MockHttp
            .Expect("https://test.client.com/hook")
            .WithHeaders(
                "X-Hub-Signature-256",
                "sha256=8EC2360A34811845884D8FCE03866EA8FAD9429AAA9E6247D7A817AD2E170B8F"
            )
            .Respond(HttpStatusCode.RequestTimeout);

        var payload = new { BuildId = "build1", EngineId = "engine1" };
        Assert.ThrowsAsync<HttpRequestException>(
            () => env.Job.RunAsync(WebhookEvent.TranslationBuildStarted, "client", payload, CancellationToken.None)
        );

        env.MockHttp.VerifyNoOutstandingExpectation();
    }

    [Test]
    public void RunAsync_Exception()
    {
        var env = new TestEnvironment();
        env.Hooks.Add(
            new Webhook
            {
                Id = "hook1",
                Url = "https://test.client.com/hook",
                Secret = "this is a secret",
                Owner = "client",
                Events = new List<WebhookEvent> { WebhookEvent.TranslationBuildStarted }
            }
        );
        env.MockHttp
            .Expect("https://test.client.com/hook")
            .WithHeaders(
                "X-Hub-Signature-256",
                "sha256=8EC2360A34811845884D8FCE03866EA8FAD9429AAA9E6247D7A817AD2E170B8F"
            )
            .Throw(new HttpRequestException());

        var payload = new { BuildId = "build1", EngineId = "engine1" };
        Assert.ThrowsAsync<HttpRequestException>(
            () => env.Job.RunAsync(WebhookEvent.TranslationBuildStarted, "client", payload, CancellationToken.None)
        );

        env.MockHttp.VerifyNoOutstandingExpectation();
    }

    private class TestEnvironment
    {
        public TestEnvironment()
        {
            var jsonOptions = new JsonOptions();
            jsonOptions.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            jsonOptions.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            Job = new WebhookJob(Hooks, MockHttp.ToHttpClient(), new OptionsWrapper<JsonOptions>(jsonOptions));
        }

        public WebhookJob Job { get; }
        public MemoryRepository<Webhook> Hooks { get; } = new MemoryRepository<Webhook>();
        public MockHttpMessageHandler MockHttp { get; } = new MockHttpMessageHandler();
    }
}