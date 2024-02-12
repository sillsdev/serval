namespace Serval.DataFiles.Services;

[TestFixture]
public class DataFileServiceTests
{
    const string DATA_FILE_ID = "df0000000000000000000001";

    [Test]
    public async Task CreateAsync_NoError()
    {
        var env = new TestEnvironment();
        using var fileStream = new MemoryStream();
        env.FileSystem.OpenWrite(Arg.Any<string>()).Returns(fileStream);
        var dataFile = new DataFile { Id = DATA_FILE_ID, Name = "file2" };
        string content = "This is a file.";
        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(content)))
            await env.Service.CreateAsync(dataFile, stream);

        Assert.That(env.DataFiles.Contains(dataFile.Id), Is.True);
        Assert.That(Encoding.UTF8.GetString(fileStream.ToArray()), Is.EqualTo(content));
    }

    [Test]
    public void CreateAsync_Error()
    {
        var env = new TestEnvironment();
        env.DataFiles.Add(
            new DataFile
            {
                Id = DATA_FILE_ID,
                Name = "file1",
                Filename = "file1.txt"
            }
        );
        using var fileStream = new MemoryStream();
        env.FileSystem.OpenWrite(Arg.Any<string>()).Returns(fileStream);
        var dataFile = new DataFile { Id = DATA_FILE_ID, Name = "file1" };
        string content = "This is a file.";
        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(content)))
            Assert.ThrowsAsync<DuplicateKeyException>(() => env.Service.CreateAsync(dataFile, stream));

        env.FileSystem.Received().DeleteFile(Arg.Any<string>());
    }

    [Test]
    public async Task DownloadAsync_Exists()
    {
        var env = new TestEnvironment();
        env.DataFiles.Add(
            new DataFile
            {
                Id = DATA_FILE_ID,
                Name = "file1",
                Filename = "file1.txt"
            }
        );
        byte[] content = Encoding.UTF8.GetBytes("This is a file.");
        using var fileStream = new MemoryStream(content);
        env.FileSystem.OpenRead(Arg.Any<string>()).Returns(fileStream);
        Stream downloadedStream = await env.Service.ReadAsync(DATA_FILE_ID);
        Assert.That(new StreamReader(downloadedStream).ReadToEnd(), Is.EqualTo(content));
    }

    [Test]
    public void DownloadAsync_DoesNotExists()
    {
        var env = new TestEnvironment();
        byte[] content = Encoding.UTF8.GetBytes("This is a file.");
        using var fileStream = new MemoryStream(content);
        env.FileSystem.OpenRead(Arg.Any<string>()).Returns(fileStream);
        Assert.ThrowsAsync<EntityNotFoundException>(() => env.Service.ReadAsync(DATA_FILE_ID));
    }

    [Test]
    public async Task UpdateAsync_Exists()
    {
        var env = new TestEnvironment();
        env.DataFiles.Add(
            new DataFile
            {
                Id = DATA_FILE_ID,
                Name = "file1",
                Filename = "file1.txt"
            }
        );
        using var fileStream = new MemoryStream();
        env.FileSystem.OpenWrite(Arg.Any<string>()).Returns(fileStream);
        string content = "This is a file.";
        DataFile dataFile;
        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(content)))
            dataFile = await env.Service.UpdateAsync(DATA_FILE_ID, stream);

        Assert.That(dataFile.Revision, Is.EqualTo(2));
        Assert.That(Encoding.UTF8.GetString(fileStream.ToArray()), Is.EqualTo(content));
        DeletedFile deletedFile = env.DeletedFiles.Entities.Single();
        Assert.That(deletedFile.Filename, Is.EqualTo("file1.txt"));
    }

    [Test]
    public void UpdateAsync_DoesNotExist()
    {
        var env = new TestEnvironment();
        using var fileStream = new MemoryStream();
        env.FileSystem.OpenWrite(Arg.Any<string>()).Returns(fileStream);
        string content = "This is a file.";
        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(content)))
            Assert.ThrowsAsync<EntityNotFoundException>(() => env.Service.UpdateAsync(DATA_FILE_ID, stream));

        env.FileSystem.Received().DeleteFile(Arg.Any<string>());
    }

    [Test]
    public async Task DeleteAsync_Exists()
    {
        var env = new TestEnvironment();
        env.DataFiles.Add(
            new DataFile
            {
                Id = DATA_FILE_ID,
                Name = "file1",
                Filename = "file1.txt"
            }
        );
        await env.Service.DeleteAsync(DATA_FILE_ID);

        Assert.That(env.DataFiles.Contains(DATA_FILE_ID), Is.False);
        DeletedFile deletedFile = env.DeletedFiles.Entities.Single();
        Assert.That(deletedFile.Filename, Is.EqualTo("file1.txt"));
        await env.Mediator.Received().Publish(Arg.Any<DataFileDeleted>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public void DeleteAsync_DoesNotExist()
    {
        var env = new TestEnvironment();
        Assert.ThrowsAsync<EntityNotFoundException>(() => env.Service.DeleteAsync(DATA_FILE_ID));
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
