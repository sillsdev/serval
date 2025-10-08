namespace Serval.DataFiles.Services;

[TestFixture]
public class DataFileServiceTests
{
    private const string DataFileId = "df0000000000000000000001";
    private static readonly DataFile DefaultDataFile =
        new()
        {
            Id = DataFileId,
            Owner = "owner1",
            Name = "file1",
            Filename = "file1.txt",
            Format = FileFormat.Text
        };

    [Test]
    public async Task CreateAsync_NoError()
    {
        var env = new TestEnvironment();
        using var fileStream = new MemoryStream();
        env.FileSystem.OpenWrite(Arg.Any<string>()).Returns(fileStream);
        string content = "This is a file.";
        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(content)))
            await env.Service.CreateAsync(DefaultDataFile with { }, stream);

        Assert.That(env.DataFiles.Contains(DataFileId), Is.True);
        Assert.That(Encoding.UTF8.GetString(fileStream.ToArray()), Is.EqualTo(content));
    }

    [Test]
    public void CreateAsync_Error()
    {
        var env = new TestEnvironment();
        env.DataFiles.Add(DefaultDataFile with { });
        using var fileStream = new MemoryStream();
        env.FileSystem.OpenWrite(Arg.Any<string>()).Returns(fileStream);
        string content = "This is a file.";
        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(content)))
            Assert.ThrowsAsync<DuplicateKeyException>(() => env.Service.CreateAsync(DefaultDataFile with { }, stream));

        env.FileSystem.Received().DeleteFile(Arg.Any<string>());
    }

    [Test]
    public async Task DownloadAsync_Exists()
    {
        var env = new TestEnvironment();
        env.DataFiles.Add(DefaultDataFile with { });
        byte[] content = Encoding.UTF8.GetBytes("This is a file.");
        using var fileStream = new MemoryStream(content);
        env.FileSystem.OpenRead(Arg.Any<string>()).Returns(fileStream);
        Stream downloadedStream = await env.Service.ReadAsync(DataFileId);
        Assert.That(new StreamReader(downloadedStream).ReadToEnd(), Is.EqualTo(content));
    }

    [Test]
    public void DownloadAsync_DoesNotExists()
    {
        var env = new TestEnvironment();
        byte[] content = Encoding.UTF8.GetBytes("This is a file.");
        using var fileStream = new MemoryStream(content);
        env.FileSystem.OpenRead(Arg.Any<string>()).Returns(fileStream);
        Assert.ThrowsAsync<EntityNotFoundException>(() => env.Service.ReadAsync(DataFileId));
    }

    [Test]
    public async Task UpdateAsync_Exists()
    {
        var env = new TestEnvironment();
        env.DataFiles.Add(DefaultDataFile with { });
        using var fileStream = new MemoryStream();
        env.FileSystem.OpenWrite(Arg.Any<string>()).Returns(fileStream);
        string content = "This is a file.";
        DataFile dataFile;
        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(content)))
            dataFile = await env.Service.UpdateAsync(DataFileId, stream);

        Assert.That(dataFile.Revision, Is.EqualTo(2));
        Assert.That(Encoding.UTF8.GetString(fileStream.ToArray()), Is.EqualTo(content));
        DeletedFile deletedFile = env.DeletedFiles.Entities.Single();
        Assert.That(deletedFile.Filename, Is.EqualTo("file1.txt"));
    }

    [Test]
    public void UpdateAsync_GetAsyncFails()
    {
        var env = new TestEnvironment();

        // We will use the mediator to cancel the token, which will cause GetAsync() to fail
        // What we are testing for is GetAsync() failing due to network or other connectivity issues, token cancellation being one source
        var cts = new CancellationTokenSource();
        env.Mediator.When(x => x.Publish(Arg.Any<DataFileUpdated>(), Arg.Any<CancellationToken>()))
            .Do(_ => cts.Cancel());

        // Set up a valid existing file
        env.DataFiles.Add(DefaultDataFile with { });
        using var fileStream = new MemoryStream();
        env.FileSystem.OpenWrite(Arg.Any<string>()).Returns(fileStream);
        string content = "This is a file.";
        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(content)))
        {
            Assert.ThrowsAsync<OperationCanceledException>(
                () => env.Service.UpdateAsync(DataFileId, stream, cts.Token)
            );
        }

        // Verify the file was updated
        DataFile dataFile = env.DataFiles.Get(DataFileId);
        Assert.That(dataFile.Revision, Is.EqualTo(2));
        Assert.That(Encoding.UTF8.GetString(fileStream.ToArray()), Is.EqualTo(content));
        DeletedFile deletedFile = env.DeletedFiles.Entities.Single();
        Assert.That(deletedFile.Filename, Is.EqualTo("file1.txt"));

        env.FileSystem.DidNotReceive().DeleteFile(Arg.Any<string>());
    }

    [Test]
    public void UpdateAsync_DoesNotExist()
    {
        var env = new TestEnvironment();
        using var fileStream = new MemoryStream();
        env.FileSystem.OpenWrite(Arg.Any<string>()).Returns(fileStream);
        string content = "This is a file.";
        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(content)))
            Assert.ThrowsAsync<EntityNotFoundException>(() => env.Service.UpdateAsync(DataFileId, stream));

        env.FileSystem.Received().DeleteFile(Arg.Any<string>());
    }

    [Test]
    public async Task DeleteAsync_Exists()
    {
        var env = new TestEnvironment();
        env.DataFiles.Add(DefaultDataFile with { });
        await env.Service.DeleteAsync(DataFileId);

        Assert.That(env.DataFiles.Contains(DataFileId), Is.False);
        DeletedFile deletedFile = env.DeletedFiles.Entities.Single();
        Assert.That(deletedFile.Filename, Is.EqualTo("file1.txt"));
        await env.Mediator.Received().Publish(Arg.Any<DataFileDeleted>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public void DeleteAsync_DoesNotExist()
    {
        var env = new TestEnvironment();
        Assert.ThrowsAsync<EntityNotFoundException>(() => env.Service.DeleteAsync(DataFileId));
    }

    private class TestEnvironment
    {
        public TestEnvironment()
        {
            DataFiles = new MemoryRepository<DataFile>();
            IOptionsMonitor<DataFileOptions> options = Substitute.For<IOptionsMonitor<DataFileOptions>>();
            options.CurrentValue.Returns(new DataFileOptions());
            Mediator = Substitute.For<IScopedMediator>();
            DeletedFiles = new MemoryRepository<DeletedFile>();
            FileSystem = Substitute.For<IFileSystem>();
            Service = new DataFileService(
                DataFiles,
                new MemoryDataAccessContext(),
                options,
                Mediator,
                DeletedFiles,
                FileSystem
            );
        }

        public IFileSystem FileSystem { get; }
        public MemoryRepository<DeletedFile> DeletedFiles { get; }
        public IScopedMediator Mediator { get; }
        public MemoryRepository<DataFile> DataFiles { get; }
        public DataFileService Service { get; }
    }
}
