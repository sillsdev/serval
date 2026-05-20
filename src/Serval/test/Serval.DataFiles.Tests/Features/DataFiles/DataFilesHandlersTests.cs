namespace Serval.DataFiles.Features.DataFiles;

[TestFixture]
public class DataFilesHandlersTests
{
    private const string Owner = "owner1";

    [Test]
    public async Task GetAllDataFiles()
    {
        var env = new TestEnvironment();
        await env.CreateDataFileAsync("df0000000000000000000001");
        await env.CreateDataFileAsync("df0000000000000000000002");
        await env.DataFiles.InsertAsync(
            new DataFile
            {
                Id = "df0000000000000000000003",
                Owner = "owner2",
                Name = "file3",
                Filename = "file3.txt",
                Format = FileFormat.Text,
            }
        );
        GetAllDataFilesHandler handler = new(env.DataFiles, env.Mapper);
        GetAllDataFilesResponse response = await handler.HandleAsync(new(Owner), CancellationToken.None);
        Assert.That(response.DataFiles.Count(), Is.EqualTo(2));
    }

    [Test]
    public async Task GetDataFile_FileExists()
    {
        var env = new TestEnvironment();
        DataFile file = await env.CreateDataFileAsync();
        GetDataFileHandler handler = new(env.DataFiles, env.Mapper);
        GetDataFileResponse response = await handler.HandleAsync(new(Owner, file.Id), CancellationToken.None);
        Assert.That(response.DataFile.Id, Is.EqualTo(file.Id));
    }

    [Test]
    public void GetDataFile_FileDoesNotExist()
    {
        var env = new TestEnvironment();
        GetDataFileHandler handler = new(env.DataFiles, env.Mapper);
        Assert.ThrowsAsync<EntityNotFoundException>(() =>
            handler.HandleAsync(new(Owner, "df0000000000000000000001"), CancellationToken.None)
        );
    }

    [Test]
    public async Task GetDataFile_WrongOwner()
    {
        var env = new TestEnvironment();
        DataFile file = await env.CreateDataFileAsync();
        GetDataFileHandler handler = new(env.DataFiles, env.Mapper);
        Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.HandleAsync(new("owner2", file.Id), CancellationToken.None)
        );
    }

    [Test]
    public async Task CreateDataFile()
    {
        var env = new TestEnvironment();
        using var fileStream = new MemoryStream();
        env.FileSystem.OpenWrite(Arg.Any<string>()).Returns(fileStream);
        string content = "This is a file.";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        CreateDataFileHandler handler = new(env.DataFiles, env.IdGenerator, env.Options, env.FileSystem, env.Mapper);
        CreateDataFileResponse response = await handler.HandleAsync(
            new(Owner, "file1", "file1.txt", FileFormat.Text, stream),
            CancellationToken.None
        );
        DataFile? file = await env.DataFiles.GetAsync(response.DataFile.Id, CancellationToken.None);
        Assert.That(file, Is.Not.Null);
        Assert.That(Encoding.UTF8.GetString(fileStream.ToArray()), Is.EqualTo(content));
    }

    [Test]
    public async Task CreateDataFile_Error()
    {
        var env = new TestEnvironment();
        await env.CreateDataFileAsync();
        using var fileStream = new MemoryStream();
        env.FileSystem.OpenWrite(Arg.Any<string>()).Returns(fileStream);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("This is a file."));
        CreateDataFileHandler handler = new(env.DataFiles, env.IdGenerator, env.Options, env.FileSystem, env.Mapper);
        Assert.ThrowsAsync<DuplicateKeyException>(() =>
            handler.HandleAsync(new(Owner, "file1", "file1.txt", FileFormat.Text, stream), CancellationToken.None)
        );
        env.FileSystem.Received().DeleteFile(Arg.Any<string>());
    }

    [Test]
    public async Task CreateDataFile_Paratext()
    {
        var env = new TestEnvironment();
        env.FileSystem.OpenWrite(Arg.Any<string>())
            .Returns(callInfo => new FileStream(callInfo.Arg<string>(), FileMode.Create, FileAccess.Write));
        string paratextZipPath = ZipParatextProject();
        CreateDataFileHandler handler = new(env.DataFiles, env.IdGenerator, env.Options, env.FileSystem, env.Mapper);
        using FileStream stream = File.OpenRead(paratextZipPath);
        CreateDataFileResponse response = await handler.HandleAsync(
            new(Owner, "file1", "file1.txt", FileFormat.Paratext, stream),
            CancellationToken.None
        );
        DataFile? dataFile = await env.DataFiles.GetAsync(response.DataFile.Id, CancellationToken.None);
        Assert.That(dataFile, Is.Not.Null);
        Assert.That(dataFile.FileMetadata, Is.Not.Null);
        ParatextMetadata metadata = dataFile.FileMetadata;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(metadata.ProjectGuid, Is.EqualTo("a7e0b3ce0200736062f9f810a444dbfbe64aca35"));
            Assert.That(metadata.Name, Is.EqualTo("Te1"));
            Assert.That(metadata.FullName, Is.EqualTo("Test1"));
            Assert.That(metadata.TranslationType, Is.EqualTo("Standard"));
            Assert.That(metadata.Versification, Does.StartWith("English"));
            Assert.That(metadata.LanguageCode, Is.EqualTo("en"));
            Assert.That(metadata.Visibility, Is.EqualTo("Public"));
        }
    }

    [Test]
    public async Task DownloadDataFile_FileExists()
    {
        var env = new TestEnvironment();
        DataFile file = await env.CreateDataFileAsync();
        string content = "This is a file.";
        env.FileSystem.OpenRead(Arg.Any<string>()).Returns(new MemoryStream(Encoding.UTF8.GetBytes(content)));
        DownloadDataFileHandler handler = new(env.DataFiles, env.Options, env.FileSystem);
        DownloadDataFileResponse response = await handler.HandleAsync(new(Owner, file.Id), CancellationToken.None);
        Assert.That(new StreamReader(response.FileStream).ReadToEnd(), Is.EqualTo(content));
    }

    [Test]
    public void DownloadDataFile_FileDoesNotExist()
    {
        var env = new TestEnvironment();
        DownloadDataFileHandler handler = new(env.DataFiles, env.Options, env.FileSystem);
        Assert.ThrowsAsync<EntityNotFoundException>(() =>
            handler.HandleAsync(new(Owner, "df0000000000000000000001"), CancellationToken.None)
        );
    }

    [Test]
    public async Task UpdateDataFile_FileExists()
    {
        var env = new TestEnvironment();
        DataFile file = await env.CreateDataFileAsync();
        using var fileStream = new MemoryStream();
        env.FileSystem.OpenWrite(Arg.Any<string>()).Returns(fileStream);
        string content = "This is a file.";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        UpdateDataFileHandler handler = new(
            env.DataFiles,
            env.DeletedFiles,
            env.DataAccessContext,
            env.Options,
            env.EventRouter,
            env.FileSystem,
            env.Mapper
        );
        UpdateDataFileResponse response = await handler.HandleAsync(
            new(Owner, file.Id, stream),
            CancellationToken.None
        );
        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.DataFile.Revision, Is.EqualTo(2));
            Assert.That(Encoding.UTF8.GetString(fileStream.ToArray()), Is.EqualTo(content));
            Assert.That(env.DeletedFiles.Entities.Single().Filename, Is.EqualTo("file1.txt"));
        }
    }

    [Test]
    public async Task UpdateDataFile_GetAsyncFails()
    {
        var env = new TestEnvironment();
        DataFile file = await env.CreateDataFileAsync();
        using var fileStream = new MemoryStream();
        env.FileSystem.OpenWrite(Arg.Any<string>()).Returns(fileStream);
        var cts = new CancellationTokenSource();
        env.EventRouter.When(x => x.PublishAsync(Arg.Any<DataFileUpdated>(), Arg.Any<CancellationToken>()))
            .Do(_ => cts.Cancel());
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("This is a file."));
        UpdateDataFileHandler handler = new(
            env.DataFiles,
            env.DeletedFiles,
            env.DataAccessContext,
            env.Options,
            env.EventRouter,
            env.FileSystem,
            env.Mapper
        );
        Assert.ThrowsAsync<OperationCanceledException>(() =>
            handler.HandleAsync(new(Owner, file.Id, stream), cts.Token)
        );
        DataFile? updated = await env.DataFiles.GetAsync(file.Id, CancellationToken.None);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(updated!.Revision, Is.EqualTo(2));
            Assert.That(env.DeletedFiles.Entities.Single().Filename, Is.EqualTo("file1.txt"));
        }
        env.FileSystem.DidNotReceive().DeleteFile(Arg.Any<string>());
    }

    [Test]
    public async Task UpdateDataFile_FileDoesNotExist()
    {
        var env = new TestEnvironment();
        using var fileStream = new MemoryStream();
        env.FileSystem.OpenWrite(Arg.Any<string>()).Returns(fileStream);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("This is a file."));
        UpdateDataFileHandler handler = new(
            env.DataFiles,
            env.DeletedFiles,
            env.DataAccessContext,
            env.Options,
            env.EventRouter,
            env.FileSystem,
            env.Mapper
        );
        Assert.ThrowsAsync<EntityNotFoundException>(() =>
            handler.HandleAsync(new(Owner, "df0000000000000000000001", stream), CancellationToken.None)
        );
    }

    [Test]
    public async Task DeleteDataFile_FileExists()
    {
        var env = new TestEnvironment();
        DataFile file = await env.CreateDataFileAsync();
        DeleteDataFileHandler handler = new(env.DataFiles, env.Deleter);
        await handler.HandleAsync(new(Owner, file.Id), CancellationToken.None);
        Assert.That(await env.DataFiles.GetAsync(file.Id, CancellationToken.None), Is.Null);
        Assert.That(env.DeletedFiles.Entities.Single().Filename, Is.EqualTo("file1.txt"));
        await env.EventRouter.Received().PublishAsync(Arg.Any<DataFileDeleted>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public void DeleteDataFile_FileDoesNotExist()
    {
        var env = new TestEnvironment();
        DeleteDataFileHandler handler = new(env.DataFiles, env.Deleter);
        Assert.ThrowsAsync<EntityNotFoundException>(() =>
            handler.HandleAsync(new(Owner, "df0000000000000000000001"), CancellationToken.None)
        );
    }

    [Test]
    public async Task DeleteDataFile_WrongOwner()
    {
        var env = new TestEnvironment();
        DataFile file = await env.CreateDataFileAsync();
        DeleteDataFileHandler handler = new(env.DataFiles, env.Deleter);
        Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.HandleAsync(new("owner2", file.Id), CancellationToken.None)
        );
    }

    private class TestEnvironment
    {
        private static readonly DataFile DefaultDataFile = new()
        {
            Id = "df0000000000000000000001",
            Owner = Owner,
            Name = "file1",
            Filename = "file1.txt",
            Format = FileFormat.Text,
        };

        public TestEnvironment()
        {
            DataFiles = new MemoryRepository<DataFile>();
            DeletedFiles = new MemoryRepository<DeletedFile>();
            Corpora = new MemoryRepository<Corpus>();
            EventRouter = Substitute.For<IEventRouter>();
            DataAccessContext = new MemoryDataAccessContext();
            FileSystem = Substitute.For<IFileSystem>();
            Options = Substitute.For<IOptionsMonitor<DataFileOptions>>();
            Options.CurrentValue.Returns(new DataFileOptions());
            Mapper = new DtoMapper(Substitute.For<IUrlService>());
            IdGenerator = Substitute.For<IIdGenerator>();
            IdGenerator.GenerateId().Returns("df0000000000000000000001");
            Deleter = new DataFileDeleter(DataFiles, DeletedFiles, Corpora, DataAccessContext, EventRouter);
        }

        public MemoryRepository<DataFile> DataFiles { get; }
        public MemoryRepository<DeletedFile> DeletedFiles { get; }
        public MemoryRepository<Corpus> Corpora { get; }
        public IEventRouter EventRouter { get; }
        public IDataAccessContext DataAccessContext { get; }
        public IFileSystem FileSystem { get; }
        public IOptionsMonitor<DataFileOptions> Options { get; }
        public DtoMapper Mapper { get; }
        public IIdGenerator IdGenerator { get; }
        public DataFileDeleter Deleter { get; }

        public async Task<DataFile> CreateDataFileAsync(string id = "df0000000000000000000001")
        {
            var file = DefaultDataFile with { Id = id };
            await DataFiles.InsertAsync(file);
            return file;
        }
    }

    private static string ZipParatextProject()
    {
        string path = Path.Combine(Path.GetTempPath(), "pt-project.zip");
        if (File.Exists(path))
            File.Delete(path);
        ZipFile.CreateFromDirectory(Path.Combine("..", "..", "..", "data", "pt-project"), path);
        return path;
    }
}
