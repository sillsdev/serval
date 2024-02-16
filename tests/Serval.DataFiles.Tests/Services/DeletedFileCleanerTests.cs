namespace Serval.DataFiles.Services;

[TestFixture]
public class DeletedFileCleanerTests
{
    [Test]
    public async Task Clean()
    {
        var env = new TestEnvironment();
        env.DeletedFiles.Add(
            new DeletedFile
            {
                Id = "file1",
                Filename = "file1.txt",
                DeletedAt = DateTime.UtcNow.AddSeconds(-1)
            }
        );
        env.DeletedFiles.Add(
            new DeletedFile
            {
                Id = "file2",
                Filename = "file2.txt",
                DeletedAt = DateTime.UtcNow.AddSeconds(30)
            }
        );

        var cts = new CancellationTokenSource();
        await env.Service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(2));
        await env.Service.StopAsync(cts.Token);

        Assert.That(env.DeletedFiles.Contains("file1"), Is.False);
        Assert.That(env.DeletedFiles.Contains("file2"), Is.True);
        env.FileSystem.Received().DeleteFile("file1.txt");
        env.FileSystem.DidNotReceive().DeleteFile("file2.txt");
    }

    private class TestEnvironment
    {
        public TestEnvironment()
        {
            IOptionsMonitor<DataFileOptions> options = Substitute.For<IOptionsMonitor<DataFileOptions>>();
            options.CurrentValue.Returns(
                new DataFileOptions
                {
                    DeletedFileTimeout = TimeSpan.FromSeconds(1),
                    DeletedFileCleanerSchedule = "* * * * * *"
                }
            );
            DeletedFiles = new MemoryRepository<DeletedFile>();
            FileSystem = Substitute.For<IFileSystem>();
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddScoped<IRepository<DeletedFile>>(_ => DeletedFiles);
            Service = new DeletedFileCleaner(
                serviceCollection.BuildServiceProvider(),
                options,
                Substitute.For<ILogger<DeletedFileCleaner>>(),
                FileSystem
            );
        }

        public IFileSystem FileSystem { get; }
        public MemoryRepository<DeletedFile> DeletedFiles { get; }
        public DeletedFileCleaner Service { get; }
    }
}
