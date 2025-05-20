namespace SIL.ServiceToolkit.Services;

[TestFixture]
public class OutboxDeliveryServiceTests
{
    private const string OutboxId = "TestOutbox";
    private const string Method1 = "Method1";
    private const string Method2 = "Method2";

    [Test]
    public async Task ProcessMessagesAsync()
    {
        TestEnvironment env = new();
        env.AddStandardMessages();
        await env.ProcessMessagesAsync();
        Received.InOrder(() =>
        {
            env.Consumer2.HandleMessageAsync("B", null, Arg.Any<CancellationToken>());
            env.Consumer1.HandleMessageAsync("A", null, Arg.Any<CancellationToken>());
            env.Consumer2.HandleMessageAsync("C", null, Arg.Any<CancellationToken>());
        });
        Assert.That(env.Messages.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task ProcessMessagesAsync_Timeout()
    {
        TestEnvironment env = new();
        env.AddStandardMessages();

        // Timeout is long enough where the message attempt will be incremented, but not deleted.
        EnableConsumerFailure(env.Consumer2, StatusCode.Internal);
        await env.ProcessMessagesAsync();
        // Each group should try to send one message
        Assert.That(env.Messages.Get("B").Attempts, Is.EqualTo(1));
        Assert.That(env.Messages.Get("A").Attempts, Is.EqualTo(0));
        Assert.That(env.Messages.Get("C").Attempts, Is.EqualTo(1));

        // with now shorter timeout, the messages will be deleted.
        // 4 start build attempts, and only one build completed attempt
        env.Options.CurrentValue.Returns(new OutboxOptions { MessageExpirationTimeout = TimeSpan.FromMilliseconds(1) });
        await env.ProcessMessagesAsync();
        Assert.That(env.Messages.Count, Is.EqualTo(0));
        _ = env.Consumer1.Received(1)
            .HandleMessageAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        _ = env.Consumer2.Received(4)
            .HandleMessageAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProcessMessagesAsync_UnavailableFailure()
    {
        TestEnvironment env = new();
        env.AddStandardMessages();

        EnableConsumerFailure(env.Consumer2, StatusCode.Unavailable);
        await env.ProcessMessagesAsync();
        // Only the first group should be attempted - but not recorded as attempted
        Assert.That(env.Messages.Get("B").Attempts, Is.EqualTo(0));
        Assert.That(env.Messages.Get("A").Attempts, Is.EqualTo(0));
        Assert.That(env.Messages.Get("C").Attempts, Is.EqualTo(0));
        _ = env.Consumer1.DidNotReceive()
            .HandleMessageAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        _ = env.Consumer2.Received(1)
            .HandleMessageAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>());

        env.Consumer2.ClearReceivedCalls();
        EnableConsumerFailure(env.Consumer2, StatusCode.Internal);
        await env.ProcessMessagesAsync();
        Assert.That(env.Messages.Get("B").Attempts, Is.EqualTo(1));
        Assert.That(env.Messages.Get("A").Attempts, Is.EqualTo(0));
        Assert.That(env.Messages.Get("C").Attempts, Is.EqualTo(1));
        _ = env.Consumer1.DidNotReceive()
            .HandleMessageAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        _ = env.Consumer2.Received(2)
            .HandleMessageAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>());

        env.Consumer2.ClearReceivedCalls();
        DisableConsumerFailure(env.Consumer2);
        await env.ProcessMessagesAsync();
        Assert.That(env.Messages.Count, Is.EqualTo(0));
        _ = env.Consumer1.Received(1)
            .HandleMessageAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        _ = env.Consumer2.Received(2)
            .HandleMessageAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProcessMessagesAsync_File()
    {
        TestEnvironment env = new();
        env.AddContentStreamMessages();

        await env.ProcessMessagesAsync();
        Assert.That(env.Messages.Count, Is.EqualTo(0));
        _ = env.Consumer1.Received(1)
            .HandleMessageAsync("A", Arg.Is<Stream?>(s => s != null), Arg.Any<CancellationToken>());
        env.FileSystem.Received().DeleteFile(Path.Combine("outbox", "A"));
    }

    private static void EnableConsumerFailure(IOutboxConsumer consumer, StatusCode code)
    {
        consumer
            .HandleMessageAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new RpcException(new Status(code, "")));
        consumer
            .HandleMessageAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new RpcException(new Status(code, "")));
    }

    private static void DisableConsumerFailure(IOutboxConsumer consumer)
    {
        consumer
            .HandleMessageAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        consumer
            .HandleMessageAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
    }

    private class TestEnvironment
    {
        private readonly Dictionary<(string, string), IOutboxConsumer> _consumers;

        public TestEnvironment()
        {
            Outboxes = new MemoryRepository<Outbox>();
            Messages = new MemoryRepository<OutboxMessage>();

            Consumer1 = CreateConsumer(OutboxId, Method1);
            Consumer2 = CreateConsumer(OutboxId, Method2);
            _consumers = new Dictionary<(string, string), IOutboxConsumer>
            {
                { (OutboxId, Method1), Consumer1 },
                { (OutboxId, Method2), Consumer2 }
            };

            FileSystem = Substitute.For<IFileSystem>();
            Options = Substitute.For<IOptionsMonitor<OutboxOptions>>();
            Options.CurrentValue.Returns(new OutboxOptions());

            Service = new OutboxDeliveryService(
                Substitute.For<IServiceProvider>(),
                FileSystem,
                Options,
                Substitute.For<ILogger<OutboxDeliveryService>>()
            );
        }

        public MemoryRepository<Outbox> Outboxes { get; }
        public MemoryRepository<OutboxMessage> Messages { get; }
        public OutboxDeliveryService Service { get; }
        public IOutboxConsumer Consumer1 { get; }
        public IOutboxConsumer Consumer2 { get; }
        public IOptionsMonitor<OutboxOptions> Options { get; }
        public IFileSystem FileSystem { get; }

        public Task ProcessMessagesAsync()
        {
            return Service.ProcessMessagesAsync(_consumers, Messages);
        }

        public void AddStandardMessages()
        {
            // messages out of order - will be fixed when retrieved
            Messages.Add(
                new OutboxMessage
                {
                    Id = "A",
                    Index = 2,
                    Method = Method1,
                    GroupId = "A",
                    OutboxRef = OutboxId,
                    Content = "\"A\"",
                    HasContentStream = false
                }
            );
            Messages.Add(
                new OutboxMessage
                {
                    Id = "B",
                    Index = 1,
                    Method = Method2,
                    OutboxRef = OutboxId,
                    GroupId = "A",
                    Content = "\"B\"",
                    HasContentStream = false
                }
            );
            Messages.Add(
                new OutboxMessage
                {
                    Id = "C",
                    Index = 3,
                    Method = Method2,
                    OutboxRef = OutboxId,
                    GroupId = "B",
                    Content = "\"C\"",
                    HasContentStream = false
                }
            );
        }

        public void AddContentStreamMessages()
        {
            // messages out of order - will be fixed when retrieved
            Messages.Add(
                new OutboxMessage
                {
                    Id = "A",
                    Index = 2,
                    Method = Method1,
                    GroupId = "A",
                    OutboxRef = OutboxId,
                    Content = "\"A\"",
                    HasContentStream = true
                }
            );
            FileSystem
                .OpenRead(Path.Combine("outbox", "A"))
                .Returns(ci => new MemoryStream(Encoding.UTF8.GetBytes("Content")));
        }

        private static IOutboxConsumer CreateConsumer(string outboxId, string method)
        {
            var consumer = Substitute.For<IOutboxConsumer>();
            consumer.OutboxId.Returns(outboxId);
            consumer.Method.Returns(method);
            consumer.ContentType.Returns(typeof(string));
            return consumer;
        }
    }
}
